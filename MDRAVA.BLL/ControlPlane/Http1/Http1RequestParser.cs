using MDRAVA.BLL.ControlPlane.Headers;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace MDRAVA.BLL.ControlPlane.Http1;

public static class Http1RequestParser
{
    public static bool TryParse(
        ReadOnlySpan<byte> requestHeadBytes,
        [NotNullWhen(true)]
        out Http1RequestHead? requestHead,
        out Http1ParseError error)
    {
        return TryParse(
            requestHeadBytes,
            new Http1RequestParseLimits(int.MaxValue, int.MaxValue, int.MaxValue),
            out requestHead,
            out error);
    }

    public static bool TryParse(
        ReadOnlySpan<byte> requestHeadBytes,
        Http1RequestParseLimits limits,
        [NotNullWhen(true)]
        out Http1RequestHead? requestHead,
        out Http1ParseError error)
    {
        requestHead = null;
        error = Http1ParseError.None;

        if (requestHeadBytes.Length == 0)
        {
            error = Http1ParseError.EmptyRequest;
            return false;
        }

        var requestLineLength = IndexOfCrlf(requestHeadBytes);
        if (requestLineLength <= 0)
        {
            error = Http1ParseError.InvalidRequestLine;
            return false;
        }

        var requestLine = requestHeadBytes[..requestLineLength];
        if (requestLine.Length > limits.MaxHeaderLineBytes)
        {
            error = Http1ParseError.HeaderLineTooLarge;
            return false;
        }

        var firstSpace = requestLine.IndexOf((byte)' ');
        if (firstSpace <= 0)
        {
            error = Http1ParseError.InvalidRequestLine;
            return false;
        }

        var secondSpace = requestLine[(firstSpace + 1)..].IndexOf((byte)' ');
        if (secondSpace <= 0)
        {
            error = Http1ParseError.InvalidRequestLine;
            return false;
        }

        secondSpace += firstSpace + 1;
        var methodBytes = requestLine[..firstSpace];
        var targetBytes = requestLine[(firstSpace + 1)..secondSpace];
        var versionBytes = requestLine[(secondSpace + 1)..];

        if (!AsciiEquals(versionBytes, "HTTP/1.1") && !AsciiEquals(versionBytes, "HTTP/1.0"))
        {
            error = Http1ParseError.UnsupportedVersion;
            return false;
        }

        if (targetBytes.Length == 0 || targetBytes[0] != (byte)'/')
        {
            error = Http1ParseError.InvalidTarget;
            return false;
        }

        if (targetBytes.Length > limits.MaxPathBytes)
        {
            error = Http1ParseError.TargetTooLarge;
            return false;
        }

        string? host = null;
        List<ProxyHeaderField> headers = [];
        List<string> contentLengthValues = [];
        List<string> transferEncodingValues = [];
        var nextLineStart = requestLineLength + 2;

        while (nextLineStart < requestHeadBytes.Length)
        {
            var remaining = requestHeadBytes[nextLineStart..];
            var lineLength = IndexOfCrlf(remaining);
            if (lineLength < 0)
            {
                error = Http1ParseError.InvalidHeaderLine;
                return false;
            }

            if (lineLength == 0)
            {
                break;
            }

            if (lineLength > limits.MaxHeaderLineBytes)
            {
                error = Http1ParseError.HeaderLineTooLarge;
                return false;
            }

            if (headers.Count >= limits.MaxHeaderCount)
            {
                error = Http1ParseError.HeaderCountExceeded;
                return false;
            }

            var headerLine = remaining[..lineLength];
            var colon = headerLine.IndexOf((byte)':');
            if (colon <= 0)
            {
                error = Http1ParseError.InvalidHeaderLine;
                return false;
            }

            var header = new Http1Header(
                Trim(headerLine[..colon]),
                Trim(headerLine[(colon + 1)..]));

            var headerName = Encoding.ASCII.GetString(header.Name);
            var headerValue = Encoding.ASCII.GetString(header.Value);
            headers.Add(new ProxyHeaderField(headerName, headerValue));

            if (AsciiEqualsIgnoreCase(header.Name, "Host"))
            {
                host = headerValue;
            }
            else if (AsciiEqualsIgnoreCase(header.Name, "Content-Length"))
            {
                contentLengthValues.Add(headerValue);
            }
            else if (AsciiEqualsIgnoreCase(header.Name, "Transfer-Encoding"))
            {
                transferEncodingValues.Add(headerValue);
            }

            nextLineStart += lineLength + 2;
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            error = Http1ParseError.MissingHost;
            return false;
        }

        var method = Encoding.ASCII.GetString(methodBytes);
        var target = Encoding.ASCII.GetString(targetBytes);
        var version = Encoding.ASCII.GetString(versionBytes);
        var path = ExtractPath(target);

        if (!TryAnalyzeRequestFraming(contentLengthValues, transferEncodingValues, out var framing, out error))
        {
            return false;
        }

        requestHead = new Http1RequestHead(
            method,
            target,
            path,
            version,
            host,
            framing,
            headers);

        return true;
    }

    private static bool TryAnalyzeRequestFraming(
        IReadOnlyList<string> contentLengthValues,
        IReadOnlyList<string> transferEncodingValues,
        out Http1RequestFraming framing,
        out Http1ParseError error)
    {
        framing = Http1RequestFraming.None;
        error = Http1ParseError.None;

        if (transferEncodingValues.Count > 0 && contentLengthValues.Count > 0)
        {
            error = Http1ParseError.ContentLengthWithTransferEncoding;
            return false;
        }

        if (transferEncodingValues.Count > 0)
        {
            if (!TryAnalyzeTransferEncoding(transferEncodingValues, out error))
            {
                return false;
            }

            framing = Http1RequestFraming.Chunked;
            return true;
        }

        if (contentLengthValues.Count == 0)
        {
            return true;
        }

        if (!TryAnalyzeContentLength(contentLengthValues, out var contentLength, out error))
        {
            return false;
        }

        framing = Http1RequestFraming.FromContentLength(contentLength);
        return true;
    }

    internal static bool TryAnalyzeContentLength(
        IReadOnlyList<string> contentLengthValues,
        out long contentLength,
        out Http1ParseError error)
    {
        contentLength = 0;
        error = Http1ParseError.None;
        long? observed = null;

        foreach (var headerValue in contentLengthValues)
        {
            var parts = headerValue.Split(',', StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                if (!TryParseNonNegativeInt64(Encoding.ASCII.GetBytes(part), out var parsed))
                {
                    error = Http1ParseError.InvalidContentLength;
                    return false;
                }

                if (observed.HasValue && observed.Value != parsed)
                {
                    error = Http1ParseError.ConflictingContentLength;
                    return false;
                }

                observed = parsed;
            }
        }

        if (!observed.HasValue)
        {
            error = Http1ParseError.InvalidContentLength;
            return false;
        }

        contentLength = observed.Value;
        return true;
    }

    internal static bool TryAnalyzeTransferEncoding(
        IReadOnlyList<string> transferEncodingValues,
        out Http1ParseError error)
    {
        error = Http1ParseError.None;
        List<string> codings = [];

        foreach (var headerValue in transferEncodingValues)
        {
            var parts = headerValue.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            codings.AddRange(parts);
        }

        if (codings.Count == 0)
        {
            error = Http1ParseError.InvalidTransferEncoding;
            return false;
        }

        if (codings.Count != 1 || !string.Equals(codings[0], "chunked", StringComparison.OrdinalIgnoreCase))
        {
            error = Http1ParseError.UnsupportedTransferEncoding;
            return false;
        }

        return true;
    }

    private static string ExtractPath(string target)
    {
        var queryIndex = target.IndexOf('?');
        return queryIndex < 0 ? target : target[..queryIndex];
    }

    private static int IndexOfCrlf(ReadOnlySpan<byte> bytes)
    {
        for (var index = 1; index < bytes.Length; index++)
        {
            if (bytes[index - 1] == (byte)'\r' && bytes[index] == (byte)'\n')
            {
                return index - 1;
            }
        }

        return -1;
    }

    private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> bytes)
    {
        while (bytes.Length > 0 && IsOptionalWhitespace(bytes[0]))
        {
            bytes = bytes[1..];
        }

        while (bytes.Length > 0 && IsOptionalWhitespace(bytes[^1]))
        {
            bytes = bytes[..^1];
        }

        return bytes;
    }

    private static bool IsOptionalWhitespace(byte value)
    {
        return value is (byte)' ' or (byte)'\t';
    }

    private static bool TryParseNonNegativeInt64(ReadOnlySpan<byte> bytes, out long value)
    {
        value = 0;

        if (bytes.Length == 0)
        {
            return false;
        }

        foreach (var digit in bytes)
        {
            if (digit is < (byte)'0' or > (byte)'9')
            {
                return false;
            }

            var next = value * 10 + digit - (byte)'0';
            if (next < value)
            {
                return false;
            }

            value = next;
        }

        return true;
    }

    private static bool AsciiEquals(ReadOnlySpan<byte> bytes, string text)
    {
        if (bytes.Length != text.Length)
        {
            return false;
        }

        for (var index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != text[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool AsciiEqualsIgnoreCase(ReadOnlySpan<byte> bytes, string text)
    {
        if (bytes.Length != text.Length)
        {
            return false;
        }

        for (var index = 0; index < bytes.Length; index++)
        {
            var left = ToLowerAscii(bytes[index]);
            var right = ToLowerAscii((byte)text[index]);
            if (left != right)
            {
                return false;
            }
        }

        return true;
    }

    private static byte ToLowerAscii(byte value)
    {
        return value is >= (byte)'A' and <= (byte)'Z'
            ? (byte)(value + 32)
            : value;
    }
}

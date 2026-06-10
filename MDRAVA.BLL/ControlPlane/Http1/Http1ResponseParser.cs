using MDRAVA.BLL.ControlPlane.Headers;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace MDRAVA.BLL.ControlPlane.Http1;

public static class Http1ResponseParser
{
    public static bool TryParse(
        ReadOnlySpan<byte> responseHeadBytes,
        string requestMethod,
        [NotNullWhen(true)] out Http1ResponseHead? responseHead,
        out Http1ParseError error)
    {
        responseHead = null;
        error = Http1ParseError.None;

        var statusLineLength = IndexOfCrlf(responseHeadBytes);
        if (statusLineLength <= 0)
        {
            error = Http1ParseError.InvalidStatusLine;
            return false;
        }

        var statusLine = responseHeadBytes[..statusLineLength];
        var firstSpace = statusLine.IndexOf((byte)' ');
        if (firstSpace <= 0)
        {
            error = Http1ParseError.InvalidStatusLine;
            return false;
        }

        var versionBytes = statusLine[..firstSpace];
        if (!AsciiEquals(versionBytes, "HTTP/1.1"))
        {
            error = Http1ParseError.UnsupportedVersion;
            return false;
        }

        var statusAndReason = statusLine[(firstSpace + 1)..];
        if (statusAndReason.Length < 3 || !TryParseStatusCode(statusAndReason[..3], out var statusCode))
        {
            error = Http1ParseError.InvalidStatusLine;
            return false;
        }

        var reasonPhrase = statusAndReason.Length > 3 && statusAndReason[3] == (byte)' '
            ? Encoding.ASCII.GetString(statusAndReason[4..])
            : "";

        List<ProxyHeaderField> headers = [];
        List<string> contentLengthValues = [];
        List<string> transferEncodingValues = [];
        var nextLineStart = statusLineLength + 2;

        while (nextLineStart < responseHeadBytes.Length)
        {
            var remaining = responseHeadBytes[nextLineStart..];
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

            var headerLine = remaining[..lineLength];
            var colon = headerLine.IndexOf((byte)':');
            if (colon <= 0)
            {
                error = Http1ParseError.InvalidHeaderLine;
                return false;
            }

            var name = Encoding.ASCII.GetString(Trim(headerLine[..colon]));
            var value = Encoding.ASCII.GetString(Trim(headerLine[(colon + 1)..]));
            headers.Add(new ProxyHeaderField(name, value));

            if (string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                contentLengthValues.Add(value);
            }
            else if (string.Equals(name, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
            {
                transferEncodingValues.Add(value);
            }

            nextLineStart += lineLength + 2;
        }

        if (!TryAnalyzeResponseFraming(
                requestMethod,
                statusCode,
                contentLengthValues,
                transferEncodingValues,
                out var framing,
                out error))
        {
            return false;
        }

        responseHead = new Http1ResponseHead(
            "HTTP/1.1",
            statusCode,
            reasonPhrase,
            framing,
            headers);
        return true;
    }

    public static bool IsInformational(Http1ResponseHead responseHead)
    {
        return responseHead.StatusCode is >= 100 and <= 199;
    }

    public static bool IsNoBodyResponse(string requestMethod, int statusCode)
    {
        return string.Equals(requestMethod, "HEAD", StringComparison.Ordinal)
            || statusCode is >= 100 and <= 199 or 204 or 304;
    }

    private static bool TryAnalyzeResponseFraming(
        string requestMethod,
        int statusCode,
        IReadOnlyList<string> contentLengthValues,
        IReadOnlyList<string> transferEncodingValues,
        out Http1ResponseFraming framing,
        out Http1ParseError error)
    {
        framing = Http1ResponseFraming.None;
        error = Http1ParseError.None;

        if (IsNoBodyResponse(requestMethod, statusCode))
        {
            return true;
        }

        if (transferEncodingValues.Count > 0)
        {
            if (!Http1RequestParser.TryAnalyzeTransferEncoding(transferEncodingValues, out error))
            {
                return false;
            }

            framing = Http1ResponseFraming.Chunked;
            return true;
        }

        if (contentLengthValues.Count > 0)
        {
            if (!Http1RequestParser.TryAnalyzeContentLength(contentLengthValues, out var contentLength, out error))
            {
                return false;
            }

            framing = Http1ResponseFraming.FromContentLength(contentLength);
            return true;
        }

        framing = Http1ResponseFraming.CloseDelimited;
        return true;
    }

    private static bool TryParseStatusCode(ReadOnlySpan<byte> bytes, out int statusCode)
    {
        statusCode = 0;
        if (bytes.Length != 3)
        {
            return false;
        }

        foreach (var digit in bytes)
        {
            if (digit is < (byte)'0' or > (byte)'9')
            {
                return false;
            }

            statusCode = statusCode * 10 + digit - (byte)'0';
        }

        return true;
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
}

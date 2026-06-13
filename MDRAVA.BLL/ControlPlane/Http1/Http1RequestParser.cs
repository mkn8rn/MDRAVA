using MDRAVA.BLL.Http;
using MDRAVA.BLL.ControlPlane.Headers;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace MDRAVA.BLL.ControlPlane.Http1;

public static partial class Http1RequestParser
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

        var framingAnalysis = AnalyzeRequestFraming(contentLengthValues, transferEncodingValues);
        if (framingAnalysis is Http1RequestFramingAnalysisResult.Rejected rejectedFraming)
        {
            error = rejectedFraming.Error;
            return false;
        }

        var framing = ((Http1RequestFramingAnalysisResult.Accepted)framingAnalysis).Framing;
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

    private static string ExtractPath(string target)
    {
        var queryIndex = target.IndexOf('?');
        return queryIndex < 0 ? target : target[..queryIndex];
    }
}

using MDRAVA.BLL.Http;
using MDRAVA.BLL.ControlPlane.Headers;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace MDRAVA.BLL.ControlPlane.Http1;

public static partial class Http1ResponseParser
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

        var framingAnalysis = AnalyzeResponseFraming(
            requestMethod,
            statusCode,
            contentLengthValues,
            transferEncodingValues);
        if (framingAnalysis is Http1ResponseFramingAnalysisResult.Rejected rejectedFraming)
        {
            error = rejectedFraming.Error;
            return false;
        }

        var framing = ((Http1ResponseFramingAnalysisResult.Accepted)framingAnalysis).Framing;
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

}

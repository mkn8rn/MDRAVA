using System.Diagnostics.CodeAnalysis;
using MDRAVA.BLL.ControlPlane.Routing;

namespace MDRAVA.BLL.ControlPlane.Http1;

public sealed record FramedUpstreamResponseTranslationInput(
    int StatusCode,
    IReadOnlyList<Http1HeaderField> Headers,
    bool ResponseEndedWithHead);

public static class FramedUpstreamResponsePolicy
{
    public static bool TryBuildHttp1ResponseHead(
        Http1RequestHead requestHead,
        FramedUpstreamResponseTranslationInput upstreamResponse,
        [NotNullWhen(true)] out Http1ResponseHead? responseHead,
        out string rejectionReason)
    {
        ArgumentNullException.ThrowIfNull(requestHead);
        ArgumentNullException.ThrowIfNull(upstreamResponse);

        responseHead = null;
        if (!TryDetermineFraming(requestHead, upstreamResponse, out var framing, out rejectionReason))
        {
            return false;
        }

        responseHead = new Http1ResponseHead(
            "HTTP/1.1",
            upstreamResponse.StatusCode,
            ProxyRouteActionPolicy.ReasonPhrase(upstreamResponse.StatusCode),
            framing,
            upstreamResponse.Headers);
        return true;
    }

    private static bool TryDetermineFraming(
        Http1RequestHead requestHead,
        FramedUpstreamResponseTranslationInput upstreamResponse,
        out Http1ResponseFraming framing,
        out string rejectionReason)
    {
        rejectionReason = "";
        if (upstreamResponse.ResponseEndedWithHead
            || string.Equals(requestHead.Method, "HEAD", StringComparison.OrdinalIgnoreCase)
            || upstreamResponse.StatusCode is 204 or 304)
        {
            framing = Http1ResponseFraming.None;
            return true;
        }

        var contentLengthValues = upstreamResponse.Headers
            .Where(header => string.Equals(header.Name, "content-length", StringComparison.OrdinalIgnoreCase))
            .Select(header => header.Value)
            .ToArray();
        if (contentLengthValues.Length > 0)
        {
            if (!Http1RequestParser.TryAnalyzeContentLength(contentLengthValues, out var contentLength, out var error))
            {
                framing = Http1ResponseFraming.None;
                rejectionReason = error.ToString();
                return false;
            }

            framing = Http1ResponseFraming.FromContentLength(contentLength);
            return true;
        }

        framing = Http1ResponseFraming.Chunked;
        return true;
    }
}

using System.Globalization;

namespace MDRAVA.BLL.ControlPlane;

public sealed record FramedUpstreamResponseTranslationInput(
    int StatusCode,
    IReadOnlyList<Http1HeaderField> Headers,
    bool ResponseEndedWithHead);

public static class FramedUpstreamResponsePolicy
{
    public static Http1ResponseHead BuildHttp1ResponseHead(
        Http1RequestHead requestHead,
        FramedUpstreamResponseTranslationInput upstreamResponse)
    {
        ArgumentNullException.ThrowIfNull(requestHead);
        ArgumentNullException.ThrowIfNull(upstreamResponse);

        var framing = DetermineFraming(requestHead, upstreamResponse);
        return new Http1ResponseHead(
            "HTTP/1.1",
            upstreamResponse.StatusCode,
            ProxyRouteActionPolicy.ReasonPhrase(upstreamResponse.StatusCode),
            framing,
            upstreamResponse.Headers);
    }

    private static Http1ResponseFraming DetermineFraming(
        Http1RequestHead requestHead,
        FramedUpstreamResponseTranslationInput upstreamResponse)
    {
        if (upstreamResponse.ResponseEndedWithHead
            || string.Equals(requestHead.Method, "HEAD", StringComparison.OrdinalIgnoreCase)
            || upstreamResponse.StatusCode is 204 or 304)
        {
            return Http1ResponseFraming.None;
        }

        return TryGetContentLength(upstreamResponse.Headers, out var contentLength)
            ? Http1ResponseFraming.FromContentLength(contentLength)
            : Http1ResponseFraming.Chunked;
    }

    private static bool TryGetContentLength(
        IReadOnlyList<Http1HeaderField> headers,
        out long contentLength)
    {
        contentLength = 0;
        foreach (var header in headers)
        {
            if (!string.Equals(header.Name, "content-length", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return long.TryParse(header.Value, NumberStyles.None, CultureInfo.InvariantCulture, out contentLength)
                && contentLength >= 0;
        }

        return false;
    }
}

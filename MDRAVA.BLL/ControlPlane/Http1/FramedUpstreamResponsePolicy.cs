using MDRAVA.BLL.Http;
using MDRAVA.BLL.ControlPlane.Headers;
using System.Diagnostics.CodeAnalysis;
using MDRAVA.BLL.ControlPlane.Routing;

namespace MDRAVA.BLL.ControlPlane.Http1;

public sealed record FramedUpstreamResponseTranslationInput(
    int StatusCode,
    IReadOnlyList<ProxyHeaderField> Headers,
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
        var framingDecision = DetermineFraming(requestHead, upstreamResponse);
        if (framingDecision is not UpstreamResponseFramingDecision.Accepted acceptedFraming)
        {
            rejectionReason = ((UpstreamResponseFramingDecision.Rejected)framingDecision).Reason;
            return false;
        }

        rejectionReason = "";
        responseHead = new Http1ResponseHead(
            "HTTP/1.1",
            upstreamResponse.StatusCode,
            ProxyRouteActionPolicy.ReasonPhrase(upstreamResponse.StatusCode),
            acceptedFraming.Framing,
            upstreamResponse.Headers);
        return true;
    }

    private static UpstreamResponseFramingDecision DetermineFraming(
        Http1RequestHead requestHead,
        FramedUpstreamResponseTranslationInput upstreamResponse)
    {
        if (upstreamResponse.ResponseEndedWithHead
            || string.Equals(requestHead.Method, "HEAD", StringComparison.OrdinalIgnoreCase)
            || upstreamResponse.StatusCode is 204 or 304)
        {
            return UpstreamResponseFramingDecision.Accept(Http1ResponseFraming.None);
        }

        var contentLengthValues = upstreamResponse.Headers
            .Where(header => string.Equals(header.Name, "content-length", StringComparison.OrdinalIgnoreCase))
            .Select(header => header.Value)
            .ToArray();
        if (contentLengthValues.Length > 0)
        {
            var contentLengthAnalysis = Http1RequestParser.AnalyzeContentLength(contentLengthValues);
            if (contentLengthAnalysis is Http1ContentLengthAnalysisResult.Rejected rejectedContentLength)
            {
                return UpstreamResponseFramingDecision.Reject(rejectedContentLength.Error.ToString());
            }

            var contentLength = ((Http1ContentLengthAnalysisResult.Accepted)contentLengthAnalysis).ContentLength;
            return UpstreamResponseFramingDecision.Accept(Http1ResponseFraming.FromContentLength(contentLength));
        }

        return UpstreamResponseFramingDecision.Accept(Http1ResponseFraming.Chunked);
    }

    private abstract record UpstreamResponseFramingDecision
    {
        private UpstreamResponseFramingDecision()
        {
        }

        public static UpstreamResponseFramingDecision Accept(Http1ResponseFraming framing)
        {
            return new Accepted(framing);
        }

        public static UpstreamResponseFramingDecision Reject(string reason)
        {
            return new Rejected(reason);
        }

        public sealed record Accepted : UpstreamResponseFramingDecision
        {
            public Accepted(Http1ResponseFraming framing)
            {
                Framing = framing;
            }

            public Http1ResponseFraming Framing { get; }
        }

        public sealed record Rejected : UpstreamResponseFramingDecision
        {
            public Rejected(string reason)
            {
                if (string.IsNullOrWhiteSpace(reason))
                {
                    throw new ArgumentException("Upstream response framing rejection reason is required.", nameof(reason));
                }

                Reason = reason;
            }

            public string Reason { get; }
        }
    }
}

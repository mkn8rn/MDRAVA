using MDRAVA.BLL.Http;
using MDRAVA.BLL.ControlPlane.Headers;
using MDRAVA.BLL.ControlPlane.Routing;

namespace MDRAVA.BLL.ControlPlane.Http1;

public sealed record FramedUpstreamResponseTranslationInput
{
    public FramedUpstreamResponseTranslationInput(
        int StatusCode,
        IReadOnlyList<ProxyHeaderField> Headers,
        bool ResponseEndedWithHead)
    {
        this.StatusCode = StatusCode;
        this.Headers = ProxyHeaderFieldList.Copy(Headers);
        this.ResponseEndedWithHead = ResponseEndedWithHead;
    }

    public int StatusCode { get; }

    public IReadOnlyList<ProxyHeaderField> Headers { get; }

    public bool ResponseEndedWithHead { get; }
}

public static class FramedUpstreamResponsePolicy
{
    public static FramedUpstreamResponseTranslationResult BuildHttp1ResponseHead(
        Http1RequestHead requestHead,
        FramedUpstreamResponseTranslationInput upstreamResponse)
    {
        ArgumentNullException.ThrowIfNull(requestHead);
        ArgumentNullException.ThrowIfNull(upstreamResponse);

        var framingDecision = DetermineFraming(requestHead, upstreamResponse);
        if (framingDecision is not UpstreamResponseFramingDecision.Accepted acceptedFraming)
        {
            return FramedUpstreamResponseTranslationResult.Rejected(
                ((UpstreamResponseFramingDecision.Rejected)framingDecision).Reason);
        }

        return FramedUpstreamResponseTranslationResult.Accepted(
            new Http1ResponseHead(
                "HTTP/1.1",
                upstreamResponse.StatusCode,
                ProxyRouteActionPolicy.ReasonPhrase(upstreamResponse.StatusCode),
                acceptedFraming.Framing,
                upstreamResponse.Headers));
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
                return UpstreamResponseFramingDecision.Reject(
                    Http1ParseErrorText.FromError(rejectedContentLength.Error));
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

public abstract record FramedUpstreamResponseTranslationResult
{
    private FramedUpstreamResponseTranslationResult()
    {
    }

    public static FramedUpstreamResponseTranslationResult Accepted(Http1ResponseHead responseHead)
    {
        ArgumentNullException.ThrowIfNull(responseHead);
        return new AcceptedResult(responseHead);
    }

    public static FramedUpstreamResponseTranslationResult Rejected(string reason)
    {
        return new RejectedResult(reason);
    }

    public sealed record AcceptedResult : FramedUpstreamResponseTranslationResult
    {
        public AcceptedResult(Http1ResponseHead responseHead)
        {
            ArgumentNullException.ThrowIfNull(responseHead);
            ResponseHead = responseHead;
        }

        public Http1ResponseHead ResponseHead { get; }
    }

    public sealed record RejectedResult : FramedUpstreamResponseTranslationResult
    {
        public RejectedResult(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Upstream response translation rejection reason is required.", nameof(reason));
            }

            Reason = reason;
        }

        public string Reason { get; }
    }
}

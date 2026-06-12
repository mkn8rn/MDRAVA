using MDRAVA.BLL.ControlPlane.Http1;

namespace MDRAVA.BLL.ControlPlane.Http3;

public abstract record Http3RequestTranslationResult
{
    private Http3RequestTranslationResult()
    {
    }

    public static Http3RequestTranslationResult Accepted(Http1RequestHead requestHead)
    {
        return new AcceptedResult(requestHead);
    }

    public static Http3RequestTranslationResult Rejected(string reason)
    {
        return new RejectedResult(reason);
    }

    public sealed record AcceptedResult : Http3RequestTranslationResult
    {
        public AcceptedResult(Http1RequestHead requestHead)
        {
            ArgumentNullException.ThrowIfNull(requestHead);

            RequestHead = requestHead;
        }

        public Http1RequestHead RequestHead { get; }
    }

    public sealed record RejectedResult : Http3RequestTranslationResult
    {
        public RejectedResult(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("HTTP/3 request rejection reason is required.", nameof(reason));
            }

            Reason = reason;
        }

        public string Reason { get; }
    }
}

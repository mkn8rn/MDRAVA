namespace MDRAVA.BLL.ControlPlane.Http1;

public static partial class Http1RequestParser
{
    private abstract record Http1RequestFramingAnalysisResult
    {
        private Http1RequestFramingAnalysisResult()
        {
        }

        public static Http1RequestFramingAnalysisResult Accept(Http1RequestFraming framing)
        {
            ArgumentNullException.ThrowIfNull(framing);
            return new Accepted(framing);
        }

        public static Http1RequestFramingAnalysisResult Reject(Http1ParseError error)
        {
            if (error == Http1ParseError.None)
            {
                throw new ArgumentException("Request framing rejection requires a parse error.", nameof(error));
            }

            return new Rejected(error);
        }

        public sealed record Accepted(Http1RequestFraming Framing) : Http1RequestFramingAnalysisResult;

        public sealed record Rejected(Http1ParseError Error) : Http1RequestFramingAnalysisResult;
    }
}

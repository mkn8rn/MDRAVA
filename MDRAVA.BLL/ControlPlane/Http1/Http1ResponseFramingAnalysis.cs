namespace MDRAVA.BLL.ControlPlane.Http1;

public static partial class Http1ResponseParser
{
    private static Http1ResponseFramingAnalysisResult AnalyzeResponseFraming(
        string requestMethod,
        int statusCode,
        IReadOnlyList<string> contentLengthValues,
        IReadOnlyList<string> transferEncodingValues)
    {
        if (IsNoBodyResponse(requestMethod, statusCode))
        {
            return Http1ResponseFramingAnalysisResult.Accept(Http1ResponseFraming.None);
        }

        if (transferEncodingValues.Count > 0)
        {
            var transferEncodingAnalysis = Http1RequestParser.AnalyzeTransferEncoding(transferEncodingValues);
            if (transferEncodingAnalysis is Http1TransferEncodingAnalysisResult.Rejected rejectedTransferEncoding)
            {
                return Http1ResponseFramingAnalysisResult.Reject(rejectedTransferEncoding.Error);
            }

            return Http1ResponseFramingAnalysisResult.Accept(Http1ResponseFraming.Chunked);
        }

        if (contentLengthValues.Count > 0)
        {
            var contentLengthAnalysis = Http1RequestParser.AnalyzeContentLength(contentLengthValues);
            if (contentLengthAnalysis is Http1ContentLengthAnalysisResult.Rejected rejectedContentLength)
            {
                return Http1ResponseFramingAnalysisResult.Reject(rejectedContentLength.Error);
            }

            var contentLength = ((Http1ContentLengthAnalysisResult.Accepted)contentLengthAnalysis).ContentLength;
            return Http1ResponseFramingAnalysisResult.Accept(Http1ResponseFraming.FromContentLength(contentLength));
        }

        return Http1ResponseFramingAnalysisResult.Accept(Http1ResponseFraming.CloseDelimited);
    }

    private abstract record Http1ResponseFramingAnalysisResult
    {
        private Http1ResponseFramingAnalysisResult()
        {
        }

        public static Http1ResponseFramingAnalysisResult Accept(Http1ResponseFraming framing)
        {
            ArgumentNullException.ThrowIfNull(framing);
            return new Accepted(framing);
        }

        public static Http1ResponseFramingAnalysisResult Reject(Http1ParseError error)
        {
            if (error == Http1ParseError.None)
            {
                throw new ArgumentException("Response framing rejection requires a parse error.", nameof(error));
            }

            return new Rejected(error);
        }

        public sealed record Accepted(Http1ResponseFraming Framing) : Http1ResponseFramingAnalysisResult;

        public sealed record Rejected(Http1ParseError Error) : Http1ResponseFramingAnalysisResult;
    }
}

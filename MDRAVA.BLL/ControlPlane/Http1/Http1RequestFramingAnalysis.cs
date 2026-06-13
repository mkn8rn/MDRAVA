namespace MDRAVA.BLL.ControlPlane.Http1;

public static partial class Http1RequestParser
{
    private static Http1RequestFramingAnalysisResult AnalyzeRequestFraming(
        IReadOnlyList<string> contentLengthValues,
        IReadOnlyList<string> transferEncodingValues)
    {
        if (transferEncodingValues.Count > 0 && contentLengthValues.Count > 0)
        {
            return Http1RequestFramingAnalysisResult.Reject(Http1ParseError.ContentLengthWithTransferEncoding);
        }

        if (transferEncodingValues.Count > 0)
        {
            var transferEncodingAnalysis = AnalyzeTransferEncoding(transferEncodingValues);
            if (transferEncodingAnalysis is Http1TransferEncodingAnalysisResult.Rejected rejectedTransferEncoding)
            {
                return Http1RequestFramingAnalysisResult.Reject(rejectedTransferEncoding.Error);
            }

            return Http1RequestFramingAnalysisResult.Accept(Http1RequestFraming.Chunked);
        }

        if (contentLengthValues.Count == 0)
        {
            return Http1RequestFramingAnalysisResult.Accept(Http1RequestFraming.None);
        }

        var contentLengthAnalysis = AnalyzeContentLength(contentLengthValues);
        if (contentLengthAnalysis is Http1ContentLengthAnalysisResult.Rejected rejectedContentLength)
        {
            return Http1RequestFramingAnalysisResult.Reject(rejectedContentLength.Error);
        }

        var contentLength = ((Http1ContentLengthAnalysisResult.Accepted)contentLengthAnalysis).ContentLength;
        return Http1RequestFramingAnalysisResult.Accept(Http1RequestFraming.FromContentLength(contentLength));
    }
}

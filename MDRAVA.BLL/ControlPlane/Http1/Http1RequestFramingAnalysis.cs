using System.Text;

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

    public static Http1ContentLengthAnalysisResult AnalyzeContentLength(
        IReadOnlyList<string> contentLengthValues)
    {
        long? observed = null;

        foreach (var headerValue in contentLengthValues)
        {
            var parts = headerValue.Split(',', StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                if (!TryParseNonNegativeInt64(Encoding.ASCII.GetBytes(part), out var parsed))
                {
                    return Http1ContentLengthAnalysisResult.Reject(Http1ParseError.InvalidContentLength);
                }

                if (observed.HasValue && observed.Value != parsed)
                {
                    return Http1ContentLengthAnalysisResult.Reject(Http1ParseError.ConflictingContentLength);
                }

                observed = parsed;
            }
        }

        if (!observed.HasValue)
        {
            return Http1ContentLengthAnalysisResult.Reject(Http1ParseError.InvalidContentLength);
        }

        return Http1ContentLengthAnalysisResult.Accept(observed.Value);
    }

    public static Http1TransferEncodingAnalysisResult AnalyzeTransferEncoding(
        IReadOnlyList<string> transferEncodingValues)
    {
        List<string> codings = [];

        foreach (var headerValue in transferEncodingValues)
        {
            var parts = headerValue.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            codings.AddRange(parts);
        }

        if (codings.Count == 0)
        {
            return Http1TransferEncodingAnalysisResult.Reject(Http1ParseError.InvalidTransferEncoding);
        }

        if (codings.Count != 1 || !string.Equals(codings[0], "chunked", StringComparison.OrdinalIgnoreCase))
        {
            return Http1TransferEncodingAnalysisResult.Reject(Http1ParseError.UnsupportedTransferEncoding);
        }

        return Http1TransferEncodingAnalysisResult.Accepted;
    }
}

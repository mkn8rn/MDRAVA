namespace MDRAVA.BLL.ControlPlane.Http1;

public static partial class Http1RequestParser
{
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

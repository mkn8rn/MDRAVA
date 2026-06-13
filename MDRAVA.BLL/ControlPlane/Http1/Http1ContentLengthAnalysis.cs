using System.Text;

namespace MDRAVA.BLL.ControlPlane.Http1;

public static partial class Http1RequestParser
{
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
}

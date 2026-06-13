namespace MDRAVA.BLL.ControlPlane.Http1;

public abstract record Http1TransferEncodingAnalysisResult
{
    private Http1TransferEncodingAnalysisResult()
    {
    }

    public static Http1TransferEncodingAnalysisResult Accepted { get; } = new AcceptedResult();

    public static Http1TransferEncodingAnalysisResult Reject(Http1ParseError error)
    {
        if (error == Http1ParseError.None)
        {
            throw new ArgumentException("Transfer-Encoding rejection requires a parse error.", nameof(error));
        }

        return new Rejected(error);
    }

    public sealed record Rejected(Http1ParseError Error) : Http1TransferEncodingAnalysisResult;

    private sealed record AcceptedResult : Http1TransferEncodingAnalysisResult;
}

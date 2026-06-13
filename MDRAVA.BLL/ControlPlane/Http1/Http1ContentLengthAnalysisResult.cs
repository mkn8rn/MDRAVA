namespace MDRAVA.BLL.ControlPlane.Http1;

public abstract record Http1ContentLengthAnalysisResult
{
    private Http1ContentLengthAnalysisResult()
    {
    }

    public static Http1ContentLengthAnalysisResult Accept(long contentLength)
    {
        if (contentLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(contentLength), "Content-Length cannot be negative.");
        }

        return new Accepted(contentLength);
    }

    public static Http1ContentLengthAnalysisResult Reject(Http1ParseError error)
    {
        if (error == Http1ParseError.None)
        {
            throw new ArgumentException("Content-Length rejection requires a parse error.", nameof(error));
        }

        return new Rejected(error);
    }

    public sealed record Accepted(long ContentLength) : Http1ContentLengthAnalysisResult;

    public sealed record Rejected(Http1ParseError Error) : Http1ContentLengthAnalysisResult;
}

namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed record AcmeCertificateIssueResult
{
    private AcmeCertificateIssueResult(bool succeeded, byte[]? pfxBytes, string? errorSummary)
    {
        Succeeded = succeeded;
        PfxBytes = pfxBytes;
        ErrorSummary = errorSummary;
    }

    public bool Succeeded { get; }

    public byte[]? PfxBytes { get; }

    public string? ErrorSummary { get; }

    public static AcmeCertificateIssueResult Success(byte[] pfxBytes)
    {
        return new AcmeCertificateIssueResult(
            succeeded: true,
            pfxBytes: pfxBytes,
            errorSummary: null);
    }

    public static AcmeCertificateIssueResult Failure(string errorSummary)
    {
        return new AcmeCertificateIssueResult(
            succeeded: false,
            pfxBytes: null,
            errorSummary: errorSummary);
    }
}

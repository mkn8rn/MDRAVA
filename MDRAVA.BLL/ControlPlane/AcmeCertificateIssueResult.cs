namespace MDRAVA.BLL.ControlPlane;

public sealed record AcmeCertificateIssueResult(
    bool Succeeded,
    byte[]? PfxBytes,
    string? ErrorSummary)
{
    public static AcmeCertificateIssueResult Success(byte[] pfxBytes)
    {
        return new AcmeCertificateIssueResult(true, pfxBytes, null);
    }

    public static AcmeCertificateIssueResult Failure(string errorSummary)
    {
        return new AcmeCertificateIssueResult(false, null, errorSummary);
    }
}

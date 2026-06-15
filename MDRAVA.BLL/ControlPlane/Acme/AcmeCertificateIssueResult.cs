namespace MDRAVA.BLL.ControlPlane.Acme;

public abstract record AcmeCertificateIssueResult
{
    private AcmeCertificateIssueResult()
    {
    }

    public static AcmeCertificateIssueResult Issued(byte[] pfxBytes)
    {
        ArgumentNullException.ThrowIfNull(pfxBytes);

        return new IssuedResult(pfxBytes);
    }

    public static AcmeCertificateIssueResult Failed(string errorSummary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorSummary);

        return new FailedResult(errorSummary);
    }

    public sealed record IssuedResult : AcmeCertificateIssueResult
    {
        private readonly byte[] _pfxBytes;

        public IssuedResult(byte[] pfxBytes)
        {
            ArgumentNullException.ThrowIfNull(pfxBytes);

            _pfxBytes = pfxBytes.ToArray();
        }

        public byte[] PfxBytes => _pfxBytes.ToArray();
    }

    public sealed record FailedResult : AcmeCertificateIssueResult
    {
        public FailedResult(string errorSummary)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(errorSummary);

            ErrorSummary = errorSummary;
        }

        public string ErrorSummary { get; }
    }
}

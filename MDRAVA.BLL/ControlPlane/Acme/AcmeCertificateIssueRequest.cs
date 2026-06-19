namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed record AcmeCertificateIssueRequest
{
    public AcmeCertificateIssueRequest(
        string CertificateId,
        IReadOnlyList<string> Domains,
        string DirectoryUrl,
        IReadOnlyList<string> ContactEmails,
        bool TermsAccepted)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(CertificateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(DirectoryUrl);

        this.CertificateId = CertificateId;
        this.Domains = CopyRequiredStrings(Domains, nameof(Domains));
        this.DirectoryUrl = DirectoryUrl;
        this.ContactEmails = CopyStrings(ContactEmails, nameof(ContactEmails));
        this.TermsAccepted = TermsAccepted;
    }

    public string CertificateId { get; }

    public IReadOnlyList<string> Domains { get; }

    public string DirectoryUrl { get; }

    public IReadOnlyList<string> ContactEmails { get; }

    public bool TermsAccepted { get; }

    private static IReadOnlyList<string> CopyRequiredStrings(
        IReadOnlyList<string> values,
        string parameterName)
    {
        var copy = CopyStrings(values, parameterName);
        if (copy.Count == 0)
        {
            throw new ArgumentException("At least one value is required.", parameterName);
        }

        return copy;
    }

    private static IReadOnlyList<string> CopyStrings(
        IReadOnlyList<string> values,
        string parameterName)
    {
        var copy = AcmeList.Copy(values);
        foreach (var value in copy)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Values cannot be empty.", parameterName);
            }
        }

        return copy;
    }
}

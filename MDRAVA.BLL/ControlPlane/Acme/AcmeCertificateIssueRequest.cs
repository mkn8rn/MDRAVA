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
        ArgumentNullException.ThrowIfNull(Domains);
        ArgumentNullException.ThrowIfNull(ContactEmails);

        this.CertificateId = CertificateId;
        this.Domains = AcmeList.Copy(Domains);
        this.DirectoryUrl = DirectoryUrl;
        this.ContactEmails = AcmeList.Copy(ContactEmails);
        this.TermsAccepted = TermsAccepted;
    }

    public string CertificateId { get; }

    public IReadOnlyList<string> Domains { get; }

    public string DirectoryUrl { get; }

    public IReadOnlyList<string> ContactEmails { get; }

    public bool TermsAccepted { get; }
}

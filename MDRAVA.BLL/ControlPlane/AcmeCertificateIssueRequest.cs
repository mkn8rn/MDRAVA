namespace MDRAVA.BLL.ControlPlane;

public sealed record AcmeCertificateIssueRequest(
    string CertificateId,
    IReadOnlyList<string> Domains,
    string DirectoryUrl,
    IReadOnlyList<string> ContactEmails,
    bool TermsAccepted);

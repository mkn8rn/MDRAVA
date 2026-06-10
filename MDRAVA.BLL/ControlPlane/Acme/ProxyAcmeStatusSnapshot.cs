namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed record ProxyAcmeStatusSnapshot(
    bool Enabled,
    string DirectoryUrl,
    bool UseStaging,
    IReadOnlyList<ProxyAcmeConfiguredCertificateStatus> Certificates,
    IReadOnlyDictionary<string, ProxyAcmeRuntimeCertificateStatus> RuntimeCertificates);

public sealed record ProxyAcmeConfiguredCertificateStatus(
    string Id,
    bool Enabled,
    IReadOnlyList<string> Domains,
    int RenewBeforeDays);

public sealed record ProxyAcmeRuntimeCertificateStatus(
    string Id,
    string Source,
    DateTimeOffset NotBeforeUtc,
    DateTimeOffset NotAfterUtc);

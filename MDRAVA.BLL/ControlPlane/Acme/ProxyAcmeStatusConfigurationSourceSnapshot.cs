namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed record ProxyAcmeStatusConfigurationSourceSnapshot(
    bool Enabled,
    string DirectoryUrl,
    bool UseStaging,
    IReadOnlyList<ProxyAcmeConfiguredCertificateSource> Certificates,
    IReadOnlyList<ProxyAcmeRuntimeCertificateSource> RuntimeCertificates);

public sealed record ProxyAcmeConfiguredCertificateSource(
    string Id,
    bool Enabled,
    IReadOnlyList<string> Domains,
    int RenewBeforeDays);

public sealed record ProxyAcmeRuntimeCertificateSource(
    string Key,
    string Id,
    string Source,
    DateTimeOffset NotBeforeUtc,
    DateTimeOffset NotAfterUtc);

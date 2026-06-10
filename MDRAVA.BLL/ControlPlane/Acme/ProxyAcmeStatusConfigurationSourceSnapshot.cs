namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed record ProxyAcmeStatusConfigurationSourceSnapshot(
    bool Enabled,
    string DirectoryUrl,
    bool UseStaging,
    IReadOnlyList<ProxyAcmeConfiguredCertificateStatus> Certificates,
    IReadOnlyList<ProxyAcmeRuntimeCertificateSource> RuntimeCertificates);

public sealed record ProxyAcmeRuntimeCertificateSource(
    string Key,
    string Id,
    string Source,
    DateTimeOffset NotBeforeUtc,
    DateTimeOffset NotAfterUtc);

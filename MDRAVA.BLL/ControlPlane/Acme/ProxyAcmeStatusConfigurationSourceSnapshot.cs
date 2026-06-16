using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed record ProxyAcmeStatusConfigurationSourceSnapshot
{
    public ProxyAcmeStatusConfigurationSourceSnapshot(
        bool Enabled,
        string DirectoryUrl,
        bool UseStaging,
        IEnumerable<ProxyAcmeConfiguredCertificateStatus> Certificates,
        IEnumerable<ProxyAcmeRuntimeCertificateSource> RuntimeCertificates)
    {
        ArgumentNullException.ThrowIfNull(Certificates);
        ArgumentNullException.ThrowIfNull(RuntimeCertificates);

        this.Enabled = Enabled;
        this.DirectoryUrl = DirectoryUrl;
        this.UseStaging = UseStaging;
        this.Certificates = AcmeList.Copy(Certificates.Select(RequireConfiguredCertificate));
        this.RuntimeCertificates = AcmeList.Copy(RuntimeCertificates.Select(RequireRuntimeCertificateSource));
    }

    public bool Enabled { get; }

    public string DirectoryUrl { get; }

    public bool UseStaging { get; }

    public IReadOnlyList<ProxyAcmeConfiguredCertificateStatus> Certificates { get; }

    public IReadOnlyList<ProxyAcmeRuntimeCertificateSource> RuntimeCertificates { get; }

    private static ProxyAcmeConfiguredCertificateStatus RequireConfiguredCertificate(
        ProxyAcmeConfiguredCertificateStatus certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        return certificate;
    }

    private static ProxyAcmeRuntimeCertificateSource RequireRuntimeCertificateSource(
        ProxyAcmeRuntimeCertificateSource certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        return certificate;
    }
}

public sealed record ProxyAcmeRuntimeCertificateSource(
    string Key,
    string Id,
    string Source,
    DateTimeOffset NotBeforeUtc,
    DateTimeOffset NotAfterUtc);

public static class ProxyAcmeStatusConfigurationSourceMapper
{
    public static ProxyAcmeStatusConfigurationSourceSnapshot FromSources(
        RuntimeAcmeOptions acme,
        IEnumerable<KeyValuePair<string, RuntimeCertificate>> runtimeCertificates)
    {
        ArgumentNullException.ThrowIfNull(acme);
        ArgumentNullException.ThrowIfNull(runtimeCertificates);

        return new ProxyAcmeStatusConfigurationSourceSnapshot(
            acme.Enabled,
            acme.DirectoryUrl,
            acme.UseStaging,
            acme.Certificates.Select(ToConfiguredCertificateStatus),
            runtimeCertificates.Select(ToRuntimeCertificateSource));
    }

    private static ProxyAcmeConfiguredCertificateStatus ToConfiguredCertificateStatus(
        RuntimeAcmeCertificateOptions certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        return new ProxyAcmeConfiguredCertificateStatus(
            certificate.Id,
            certificate.Enabled,
            certificate.Domains,
            certificate.RenewBeforeDays);
    }

    private static ProxyAcmeRuntimeCertificateSource ToRuntimeCertificateSource(
        KeyValuePair<string, RuntimeCertificate> certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate.Value);

        return new ProxyAcmeRuntimeCertificateSource(
            certificate.Key,
            certificate.Value.Id,
            certificate.Value.Source,
            new DateTimeOffset(certificate.Value.Certificate.NotBefore.ToUniversalTime()),
            new DateTimeOffset(certificate.Value.Certificate.NotAfter.ToUniversalTime()));
    }
}

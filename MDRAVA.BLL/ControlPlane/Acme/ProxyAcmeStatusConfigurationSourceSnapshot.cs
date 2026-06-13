namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed record ProxyAcmeStatusConfigurationSourceSnapshot
{
    public ProxyAcmeStatusConfigurationSourceSnapshot(
        bool Enabled,
        string DirectoryUrl,
        bool UseStaging,
        IReadOnlyList<ProxyAcmeConfiguredCertificateStatus> Certificates,
        IReadOnlyList<ProxyAcmeRuntimeCertificateSource> RuntimeCertificates)
    {
        ArgumentNullException.ThrowIfNull(Certificates);
        ArgumentNullException.ThrowIfNull(RuntimeCertificates);

        this.Enabled = Enabled;
        this.DirectoryUrl = DirectoryUrl;
        this.UseStaging = UseStaging;
        this.Certificates = AcmeList.Copy(Certificates);
        this.RuntimeCertificates = AcmeList.Copy(RuntimeCertificates);
    }

    public bool Enabled { get; }

    public string DirectoryUrl { get; }

    public bool UseStaging { get; }

    public IReadOnlyList<ProxyAcmeConfiguredCertificateStatus> Certificates { get; }

    public IReadOnlyList<ProxyAcmeRuntimeCertificateSource> RuntimeCertificates { get; }
}

public sealed record ProxyAcmeRuntimeCertificateSource(
    string Key,
    string Id,
    string Source,
    DateTimeOffset NotBeforeUtc,
    DateTimeOffset NotAfterUtc);

namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed record ProxyAcmeStatusSnapshot
{
    public ProxyAcmeStatusSnapshot(
        bool Enabled,
        string DirectoryUrl,
        bool UseStaging,
        IEnumerable<ProxyAcmeConfiguredCertificateStatus> Certificates,
        IReadOnlyDictionary<string, ProxyAcmeRuntimeCertificateStatus> RuntimeCertificates)
    {
        ArgumentNullException.ThrowIfNull(Certificates);
        ArgumentNullException.ThrowIfNull(RuntimeCertificates);

        this.Enabled = Enabled;
        this.DirectoryUrl = DirectoryUrl;
        this.UseStaging = UseStaging;
        this.Certificates = AcmeList.Copy(Certificates);
        this.RuntimeCertificates = new Dictionary<string, ProxyAcmeRuntimeCertificateStatus>(
            RuntimeCertificates,
            StringComparer.OrdinalIgnoreCase);
    }

    public bool Enabled { get; }

    public string DirectoryUrl { get; }

    public bool UseStaging { get; }

    public IReadOnlyList<ProxyAcmeConfiguredCertificateStatus> Certificates { get; }

    public IReadOnlyDictionary<string, ProxyAcmeRuntimeCertificateStatus> RuntimeCertificates { get; }
}

public sealed record ProxyAcmeConfiguredCertificateStatus
{
    public ProxyAcmeConfiguredCertificateStatus(
        string Id,
        bool Enabled,
        IEnumerable<string> Domains,
        int RenewBeforeDays)
    {
        ArgumentNullException.ThrowIfNull(Domains);

        this.Id = Id;
        this.Enabled = Enabled;
        this.Domains = AcmeList.Copy(Domains);
        this.RenewBeforeDays = RenewBeforeDays;
    }

    public string Id { get; }

    public bool Enabled { get; }

    public IReadOnlyList<string> Domains { get; }

    public int RenewBeforeDays { get; }
}

public sealed record ProxyAcmeRuntimeCertificateStatus(
    string Id,
    string Source,
    DateTimeOffset NotBeforeUtc,
    DateTimeOffset NotAfterUtc);

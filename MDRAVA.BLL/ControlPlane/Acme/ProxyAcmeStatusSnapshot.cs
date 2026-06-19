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
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);

        this.Id = Id;
        this.Enabled = Enabled;
        this.Domains = AcmeCommandFacts.CopyRequiredStrings(Domains, nameof(Domains));
        this.RenewBeforeDays = RenewBeforeDays;
    }

    public string Id { get; }

    public bool Enabled { get; }

    public IReadOnlyList<string> Domains { get; }

    public int RenewBeforeDays { get; }
}

public sealed record ProxyAcmeRuntimeCertificateStatus
{
    public ProxyAcmeRuntimeCertificateStatus(
        string Id,
        string Source,
        DateTimeOffset NotBeforeUtc,
        DateTimeOffset NotAfterUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(Source);

        this.Id = Id;
        this.Source = Source;
        this.NotBeforeUtc = NotBeforeUtc;
        this.NotAfterUtc = NotAfterUtc;
    }

    public string Id { get; }

    public string Source { get; }

    public DateTimeOffset NotBeforeUtc { get; }

    public DateTimeOffset NotAfterUtc { get; }
}

using System.Security.Cryptography.X509Certificates;

namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeCertificate
{
    public RuntimeCertificate(
        string Id,
        string Path,
        string Format,
        bool HasConfiguredPassword,
        X509Certificate2 Certificate,
        string Source,
        IReadOnlyList<string> Domains)
    {
        this.Id = Id;
        this.Path = Path;
        this.Format = Format;
        this.HasConfiguredPassword = HasConfiguredPassword;
        this.Certificate = Certificate;
        this.Source = Source;
        this.Domains = RuntimeList.Copy(Domains);
    }

    public string Id { get; init; }

    public string Path { get; init; }

    public string Format { get; init; }

    public bool HasConfiguredPassword { get; init; }

    public X509Certificate2 Certificate { get; init; }

    public string Source { get; init; }

    public IReadOnlyList<string> Domains { get; }
}

public static class RuntimeCertificateFactory
{
    public const string ManualPfxSource = "manualPfx";
    public const string AcmeSource = "acme";
    public const string PfxFormat = "pfx";

    public static RuntimeCertificate ManualPfx(
        string id,
        string path,
        bool hasConfiguredPassword,
        X509Certificate2 certificate)
    {
        return new RuntimeCertificate(
            id,
            path,
            PfxFormat,
            hasConfiguredPassword,
            certificate,
            ManualPfxSource,
            []);
    }

    public static RuntimeCertificate Acme(
        string id,
        X509Certificate2 certificate,
        IReadOnlyList<string> domains)
    {
        return new RuntimeCertificate(
            id,
            $"acme://{id}",
            PfxFormat,
            HasConfiguredPassword: false,
            certificate,
            AcmeSource,
            domains);
    }
}

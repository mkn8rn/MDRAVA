namespace MDRAVA.API.Proxy.Configuration;

public sealed class SniCertificateOptions
{
    public string HostName { get; init; } = "";

    public string CertificateId { get; init; } = "";
}

namespace MDRAVA.API.Models.Configuration;

public sealed class SniCertificateOptions
{
    public string HostName { get; init; } = "";

    public string CertificateId { get; init; } = "";
}

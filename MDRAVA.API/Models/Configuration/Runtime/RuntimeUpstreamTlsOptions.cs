namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeUpstreamTlsOptions(
    bool ValidateCertificate,
    string? SniHost)
{
    public static RuntimeUpstreamTlsOptions Default { get; } = new(true, null);
}

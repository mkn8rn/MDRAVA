namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeUpstreamTlsOptions(
    bool ValidateCertificate,
    string? SniHost)
{
    public static RuntimeUpstreamTlsOptions Default { get; } = new(true, null);
}

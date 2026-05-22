namespace MDRAVA.BLL.Configuration;

public sealed class UpstreamTlsOptions
{
    public bool ValidateCertificate { get; init; } = true;

    public string? SniHost { get; init; }
}

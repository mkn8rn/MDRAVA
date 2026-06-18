namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeUpstreamTlsProjection
{
    public RuntimeUpstreamTlsProjection(bool ValidateCertificate, string? SniHost)
    {
        RuntimeUpstreamTlsFacts.Validate(SniHost);

        this.ValidateCertificate = ValidateCertificate;
        this.SniHost = SniHost;
    }

    public bool ValidateCertificate { get; }

    public string? SniHost { get; }
}

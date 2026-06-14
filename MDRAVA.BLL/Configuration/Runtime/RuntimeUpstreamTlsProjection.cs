namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeUpstreamTlsProjection(
    bool ValidateCertificate,
    string? SniHost);

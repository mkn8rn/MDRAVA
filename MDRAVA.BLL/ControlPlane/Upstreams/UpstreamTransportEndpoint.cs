using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Upstreams;

public sealed record UpstreamTransportEndpoint(
    string Name,
    string Scheme,
    string Protocol,
    string Address,
    int Port,
    bool ValidateCertificate,
    string? SniHost)
{
    public string Endpoint => $"{Address}:{Port}";

    public string EffectiveSniHost => string.IsNullOrWhiteSpace(SniHost) ? Address : SniHost!;

    public string PoolKey => $"{Protocol}|{Scheme}|{Address}|{Port}|sni={EffectiveSniHost}|validate={ValidateCertificate}";
}

public static class UpstreamTransportEndpointMapper
{
    public static UpstreamTransportEndpoint FromUpstream(RuntimeUpstream upstream)
    {
        ArgumentNullException.ThrowIfNull(upstream);

        return new UpstreamTransportEndpoint(
            upstream.Name,
            upstream.Scheme,
            upstream.Protocol,
            upstream.Address,
            upstream.Port,
            upstream.Tls.ValidateCertificate,
            upstream.Tls.SniHost);
    }
}

namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeUpstream(
    string RouteName,
    string Name,
    string Scheme,
    string Address,
    int Port,
    int Weight,
    RuntimeUpstreamTlsOptions Tls)
{
    public string Endpoint => $"{Address}:{Port}";

    public string UriEndpoint => $"{Scheme}://{Address}:{Port}";

    public string EffectiveSniHost => string.IsNullOrWhiteSpace(Tls.SniHost) ? Address : Tls.SniHost!;

    public string Identity => $"{RouteName}|{Name}|{Scheme}|{Address}|{Port}|{EffectiveSniHost}|{Tls.ValidateCertificate}";
}

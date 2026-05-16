namespace MDRAVA.API.Models.Configuration;

public sealed class ProxyHttpsRedirectOptions
{
    public bool? Enabled { get; init; }

    public int? StatusCode { get; init; }

    public int? HttpsPort { get; init; }
}

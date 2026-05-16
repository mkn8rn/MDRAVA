namespace MDRAVA.API.Models.Configuration;

public sealed class ProxyCanonicalHostOptions
{
    public bool? Enabled { get; init; }

    public string TargetHost { get; init; } = "";

    public int? StatusCode { get; init; }
}

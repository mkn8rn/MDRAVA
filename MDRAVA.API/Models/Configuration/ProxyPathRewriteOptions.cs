namespace MDRAVA.API.Models.Configuration;

public sealed class ProxyPathRewriteOptions
{
    public string StripPrefix { get; init; } = "";

    public string ReplacePrefix { get; init; } = "";

    public string Replacement { get; init; } = "";
}

namespace MDRAVA.BLL.Configuration;

public sealed class ProxyPathRewriteOptions
{
    public string StripPrefix { get; init; } = "";

    public string ReplacePrefix { get; init; } = "";

    public string Replacement { get; init; } = "";
}

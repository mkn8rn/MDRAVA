namespace MDRAVA.API.Models.Configuration;

public sealed class ProxyRedirectOptions
{
    public int? StatusCode { get; init; }

    public string TargetUrl { get; init; } = "";

    public string TargetPath { get; init; } = "";

    public bool PreserveQuery { get; init; } = true;
}

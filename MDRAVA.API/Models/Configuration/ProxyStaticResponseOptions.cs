namespace MDRAVA.API.Models.Configuration;

public sealed class ProxyStaticResponseOptions
{
    public int StatusCode { get; init; } = 200;

    public string ContentType { get; init; } = "text/plain; charset=utf-8";

    public string Body { get; init; } = "";
}

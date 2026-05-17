namespace MDRAVA.API.Models.Hosting;

public sealed record ProxyListenerReloadChange(
    string Action,
    string Name,
    string Identity,
    string BindKey,
    string State,
    string? Error = null);

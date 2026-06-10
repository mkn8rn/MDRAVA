namespace MDRAVA.BLL.ControlPlane.Listeners;

public sealed record ProxyListenerReloadChange(
    string Action,
    string Name,
    string Identity,
    string BindKey,
    string State,
    string? Error = null);

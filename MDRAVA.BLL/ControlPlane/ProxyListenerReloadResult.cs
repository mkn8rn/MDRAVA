namespace MDRAVA.BLL.ControlPlane;

public sealed record ProxyListenerReloadResult(
    bool Succeeded,
    DateTimeOffset AttemptedAtUtc,
    int Added,
    int Removed,
    int Changed,
    int Unchanged,
    IReadOnlyList<ProxyListenerReloadChange> Changes,
    IReadOnlyList<string> Errors);

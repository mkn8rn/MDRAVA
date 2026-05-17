namespace MDRAVA.API.Models.Hosting;

public sealed record ProxyListenerReloadResult(
    bool Succeeded,
    DateTimeOffset AttemptedAtUtc,
    int Added,
    int Removed,
    int Changed,
    int Unchanged,
    IReadOnlyList<ProxyListenerReloadChange> Changes,
    IReadOnlyList<string> Errors);

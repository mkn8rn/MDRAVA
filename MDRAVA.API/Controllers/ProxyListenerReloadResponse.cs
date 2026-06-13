using MDRAVA.BLL.ControlPlane.Listeners;

namespace MDRAVA.API.Controllers;

public sealed record ProxyListenerReloadResponse(
    bool Succeeded,
    DateTimeOffset AttemptedAtUtc,
    int Added,
    int Removed,
    int Changed,
    int Unchanged,
    IReadOnlyList<ProxyListenerReloadChange> Changes,
    IReadOnlyList<string> Errors)
{
    public static ProxyListenerReloadResponse FromResult(ProxyListenerReloadResult result)
    {
        return result switch
        {
            ProxyListenerReloadResult.AppliedResult applied => FromResult(applied, succeeded: true),
            ProxyListenerReloadResult.FailedResult failed => FromResult(failed, succeeded: false),
            _ => throw new InvalidOperationException($"Unknown listener reload result '{result.GetType().Name}'.")
        };
    }

    private static ProxyListenerReloadResponse FromResult(
        ProxyListenerReloadResult result,
        bool succeeded)
    {
        return new ProxyListenerReloadResponse(
            Succeeded: succeeded,
            AttemptedAtUtc: result.AttemptedAtUtc,
            Added: result.Added,
            Removed: result.Removed,
            Changed: result.Changed,
            Unchanged: result.Unchanged,
            Changes: result.Changes,
            Errors: result.Errors);
    }
}

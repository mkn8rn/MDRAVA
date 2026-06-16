using BusinessProxyListenerReloadChange = MDRAVA.BLL.ControlPlane.Listeners.ProxyListenerReloadChange;
using BusinessProxyListenerReloadResult = MDRAVA.BLL.ControlPlane.Listeners.ProxyListenerReloadResult;

namespace MDRAVA.API.Controllers;

public sealed record ProxyListenerReloadResponse(
    bool Succeeded,
    DateTimeOffset AttemptedAtUtc,
    int Added,
    int Removed,
    int Changed,
    int Unchanged,
    IReadOnlyList<ProxyListenerReloadChangeResponse> Changes,
    IReadOnlyList<string> Errors)
{
    public static ProxyListenerReloadResponse FromResult(BusinessProxyListenerReloadResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result switch
        {
            BusinessProxyListenerReloadResult.AppliedResult applied => FromResult(applied, succeeded: true),
            BusinessProxyListenerReloadResult.FailedResult failed => FromResult(failed, succeeded: false),
            _ => throw new InvalidOperationException($"Unknown listener reload result '{result.GetType().Name}'.")
        };
    }

    private static ProxyListenerReloadResponse FromResult(
        BusinessProxyListenerReloadResult result,
        bool succeeded)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new ProxyListenerReloadResponse(
            Succeeded: succeeded,
            AttemptedAtUtc: result.AttemptedAtUtc,
            Added: result.Added,
            Removed: result.Removed,
            Changed: result.Changed,
            Unchanged: result.Unchanged,
            Changes: ProxyListenerReloadChangeResponse.FromChanges(result.Changes),
            Errors: ApiResponseList.Copy(result.Errors));
    }
}

public sealed record ProxyListenerReloadChangeResponse(
    string Action,
    string Name,
    string Identity,
    string BindKey,
    string State,
    string? Error)
{
    public static IReadOnlyList<ProxyListenerReloadChangeResponse> FromChanges(
        IReadOnlyList<BusinessProxyListenerReloadChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        return ApiResponseList.Copy(changes.Select(FromChange));
    }

    private static ProxyListenerReloadChangeResponse FromChange(BusinessProxyListenerReloadChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        return new ProxyListenerReloadChangeResponse(
            change.Action,
            change.Name,
            change.Identity,
            change.BindKey,
            change.State,
            change.Error);
    }
}

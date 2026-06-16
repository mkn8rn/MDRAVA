using BusinessProxyListenerReloadChange = MDRAVA.BLL.ControlPlane.Listeners.ProxyListenerReloadChange;
using BusinessProxyListenerReloadResult = MDRAVA.BLL.ControlPlane.Listeners.ProxyListenerReloadResult;

namespace MDRAVA.API.Controllers;

public sealed record ProxyListenerReloadResponse
{
    public ProxyListenerReloadResponse(
        bool succeeded,
        DateTimeOffset attemptedAtUtc,
        int added,
        int removed,
        int changed,
        int unchanged,
        IReadOnlyList<ProxyListenerReloadChangeResponse> changes,
        IReadOnlyList<string> errors)
    {
        Succeeded = succeeded;
        AttemptedAtUtc = attemptedAtUtc;
        Added = added;
        Removed = removed;
        Changed = changed;
        Unchanged = unchanged;
        Changes = ApiResponseList.Copy(changes);
        Errors = ApiResponseList.Copy(errors);
    }

    public bool Succeeded { get; }

    public DateTimeOffset AttemptedAtUtc { get; }

    public int Added { get; }

    public int Removed { get; }

    public int Changed { get; }

    public int Unchanged { get; }

    public IReadOnlyList<ProxyListenerReloadChangeResponse> Changes { get; }

    public IReadOnlyList<string> Errors { get; }

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
            succeeded: succeeded,
            attemptedAtUtc: result.AttemptedAtUtc,
            added: result.Added,
            removed: result.Removed,
            changed: result.Changed,
            unchanged: result.Unchanged,
            changes: ProxyListenerReloadChangeResponse.FromChanges(result.Changes),
            errors: result.Errors);
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

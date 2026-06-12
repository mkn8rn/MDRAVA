namespace MDRAVA.BLL.ControlPlane.Listeners;

public sealed record ProxyListenerReloadResult
{
    private ProxyListenerReloadResult(
        bool succeeded,
        DateTimeOffset attemptedAtUtc,
        int added,
        int removed,
        int changed,
        int unchanged,
        IReadOnlyList<ProxyListenerReloadChange> changes,
        IReadOnlyList<string> errors)
    {
        Succeeded = succeeded;
        AttemptedAtUtc = attemptedAtUtc;
        Added = added;
        Removed = removed;
        Changed = changed;
        Unchanged = unchanged;
        Changes = changes;
        Errors = errors;
    }

    public bool Succeeded { get; }

    public DateTimeOffset AttemptedAtUtc { get; }

    public int Added { get; }

    public int Removed { get; }

    public int Changed { get; }

    public int Unchanged { get; }

    public IReadOnlyList<ProxyListenerReloadChange> Changes { get; }

    public IReadOnlyList<string> Errors { get; }

    public static ProxyListenerReloadResult Applied(
        DateTimeOffset attemptedAtUtc,
        int added,
        int removed,
        int changed,
        int unchanged,
        IReadOnlyList<ProxyListenerReloadChange> changes,
        IReadOnlyList<string> errors)
    {
        return Create(
            succeeded: true,
            attemptedAtUtc,
            added,
            removed,
            changed,
            unchanged,
            changes,
            errors);
    }

    public static ProxyListenerReloadResult Failed(
        DateTimeOffset attemptedAtUtc,
        int added,
        int removed,
        int changed,
        int unchanged,
        IReadOnlyList<ProxyListenerReloadChange> changes,
        IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0)
        {
            throw new ArgumentException("A failed listener reload requires at least one error.", nameof(errors));
        }

        return Create(
            succeeded: false,
            attemptedAtUtc,
            added,
            removed,
            changed,
            unchanged,
            changes,
            errors);
    }

    private static ProxyListenerReloadResult Create(
        bool succeeded,
        DateTimeOffset attemptedAtUtc,
        int added,
        int removed,
        int changed,
        int unchanged,
        IReadOnlyList<ProxyListenerReloadChange> changes,
        IReadOnlyList<string> errors)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(added);
        ArgumentOutOfRangeException.ThrowIfNegative(removed);
        ArgumentOutOfRangeException.ThrowIfNegative(changed);
        ArgumentOutOfRangeException.ThrowIfNegative(unchanged);
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(errors);

        return new ProxyListenerReloadResult(
            succeeded,
            attemptedAtUtc,
            added,
            removed,
            changed,
            unchanged,
            changes,
            errors);
    }
}

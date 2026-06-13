namespace MDRAVA.BLL.ControlPlane.Listeners;

public abstract partial record ProxyListenerReloadResult
{
    public sealed record AppliedResult : ProxyListenerReloadResult
    {
        internal AppliedResult(
            DateTimeOffset attemptedAtUtc,
            int added,
            int removed,
            int changed,
            int unchanged,
            IReadOnlyList<ProxyListenerReloadChange> changes,
            IReadOnlyList<string> errors)
            : base(
                attemptedAtUtc,
                added,
                removed,
                changed,
                unchanged,
                changes,
                errors)
        {
        }
    }

    public sealed record FailedResult : ProxyListenerReloadResult
    {
        internal FailedResult(
            DateTimeOffset attemptedAtUtc,
            int added,
            int removed,
            int changed,
            int unchanged,
            IReadOnlyList<ProxyListenerReloadChange> changes,
            IReadOnlyList<string> errors)
            : base(
                attemptedAtUtc,
                added,
                removed,
                changed,
                unchanged,
                changes,
                errors)
        {
        }
    }
}

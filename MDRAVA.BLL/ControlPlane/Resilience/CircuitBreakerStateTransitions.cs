using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Resilience;

public sealed partial class CircuitBreakerStore
{
    private void RefreshOpenState(RuntimeCircuitBreakerPolicy policy, MutableCircuitState state, DateTimeOffset now)
    {
        if (state.State != CircuitBreakerRuntimeState.Open || state.OpenedAtUtc is null)
        {
            return;
        }

        if (now - state.OpenedAtUtc.Value >= policy.OpenDuration)
        {
            state.State = CircuitBreakerRuntimeState.HalfOpen;
            state.HalfOpenInFlight = 0;
            _metrics.CircuitHalfOpened();
        }
    }

    private void Open(MutableCircuitState state, DateTimeOffset now)
    {
        state.State = CircuitBreakerRuntimeState.Open;
        state.OpenedAtUtc = now;
        state.HalfOpenInFlight = 0;
        state.WindowStartedAtUtc = now;
        state.FailureCount = 0;
        _metrics.CircuitOpened();
    }

    private void Close(MutableCircuitState state)
    {
        state.State = CircuitBreakerRuntimeState.Closed;
        state.OpenedAtUtc = null;
        state.WindowStartedAtUtc = null;
        state.FailureCount = 0;
        state.HalfOpenInFlight = 0;
        state.LastFailureReason = null;
        _metrics.CircuitClosed();
    }

    private void ReleaseHalfOpenProbe(CircuitBreakerLease lease)
    {
        if (!lease.Enabled || !lease.HalfOpenProbe)
        {
            return;
        }

        var state = GetOrCreate(lease.Source.UpstreamIdentity);
        lock (state.Gate)
        {
            state.HalfOpenInFlight = Math.Max(0, state.HalfOpenInFlight - 1);
        }
    }

    private static bool ContainsStatus(IReadOnlyList<int> statusCodes, int statusCode)
    {
        return statusCodes.Any(code => code == statusCode);
    }

    private static string NormalizeReason(string reason)
    {
        return string.IsNullOrWhiteSpace(reason)
            ? "unknown"
            : reason.Trim().ToLowerInvariant().Replace(' ', '_');
    }

    private sealed class MutableCircuitState
    {
        public object Gate { get; } = new();

        public CircuitBreakerRuntimeState State { get; set; } = CircuitBreakerRuntimeState.Closed;

        public DateTimeOffset? WindowStartedAtUtc { get; set; }

        public DateTimeOffset? OpenedAtUtc { get; set; }

        public int FailureCount { get; set; }

        public int HalfOpenInFlight { get; set; }

        public long RejectedRequests { get; private set; }

        public string? LastFailureReason { get; set; }

        public void RecordRejectedRequest()
        {
            RejectedRequests++;
        }
    }
}

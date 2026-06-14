namespace MDRAVA.BLL.ControlPlane.Resilience;

public sealed partial class CircuitBreakerStore
{
    private void RefreshOpenState(CircuitBreakerPolicyInput policy, MutableCircuitState state, DateTimeOffset now)
    {
        if (state.State != CircuitBreakerRuntimeState.Open || state.OpenedAtUtc is null)
        {
            return;
        }

        if (now - state.OpenedAtUtc.Value >= policy.OpenDuration)
        {
            state.MoveToHalfOpen();
            _metrics.CircuitHalfOpened();
        }
    }

    private void Open(MutableCircuitState state, DateTimeOffset now)
    {
        state.MoveToOpen(now);
        _metrics.CircuitOpened();
    }

    private void Close(MutableCircuitState state)
    {
        state.MoveToClosed();
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
            state.RecordHalfOpenProbeCompleted();
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

        public CircuitBreakerRuntimeState State { get; private set; } = CircuitBreakerRuntimeState.Closed;

        public DateTimeOffset? WindowStartedAtUtc { get; private set; }

        public DateTimeOffset? OpenedAtUtc { get; private set; }

        public int FailureCount { get; private set; }

        public int HalfOpenInFlight { get; private set; }

        public long RejectedRequests { get; private set; }

        public string? LastFailureReason { get; private set; }

        public void MoveToHalfOpen()
        {
            State = CircuitBreakerRuntimeState.HalfOpen;
            ResetHalfOpenProbes();
        }

        public void MoveToOpen(DateTimeOffset openedAtUtc)
        {
            State = CircuitBreakerRuntimeState.Open;
            OpenedAtUtc = openedAtUtc;
            ResetHalfOpenProbes();
            ResetFailureWindow(openedAtUtc);
        }

        public void MoveToClosed()
        {
            State = CircuitBreakerRuntimeState.Closed;
            OpenedAtUtc = null;
            ClearFailureTracking();
            ResetHalfOpenProbes();
        }

        public void RecordRejectedRequest()
        {
            RejectedRequests++;
        }

        public int RecordFailure(
            string reason,
            DateTimeOffset now,
            TimeSpan samplingWindow)
        {
            RecordFailureReason(reason);
            if (WindowStartedAtUtc is null || now - WindowStartedAtUtc > samplingWindow)
            {
                WindowStartedAtUtc = now;
                FailureCount = 0;
            }

            FailureCount++;
            return FailureCount;
        }

        public void RecordFailureReason(string reason)
        {
            LastFailureReason = reason;
        }

        public void ResetFailureWindow(DateTimeOffset? windowStartedAtUtc)
        {
            WindowStartedAtUtc = windowStartedAtUtc;
            FailureCount = 0;
        }

        public void ClearFailureTracking()
        {
            ResetFailureWindow(windowStartedAtUtc: null);
            LastFailureReason = null;
        }

        public void RecordHalfOpenProbeStarted()
        {
            HalfOpenInFlight++;
        }

        public void RecordHalfOpenProbeCompleted()
        {
            HalfOpenInFlight = Math.Max(0, HalfOpenInFlight - 1);
        }

        public void ResetHalfOpenProbes()
        {
            HalfOpenInFlight = 0;
        }
    }
}

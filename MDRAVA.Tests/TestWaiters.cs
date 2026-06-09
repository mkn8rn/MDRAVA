using MDRAVA.API.Proxy.Hosting;

namespace MDRAVA.Tests;

internal static class TestWaiters
{
    public static async Task UntilAsync(
        Func<bool> condition,
        Func<string> timeoutMessage,
        CancellationToken cancellationToken,
        TimeSpan? pollInterval = null)
    {
        while (!condition())
        {
            try
            {
                await Task.Delay(pollInterval ?? TimeSpan.FromMilliseconds(25), cancellationToken);
            }
            catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(timeoutMessage(), exception);
            }
        }
    }

    public static async Task<ProxyListenerStatus> WaitForListenerAsync(
        ProxyRuntimeState runtimeState,
        string name,
        string kind,
        ProxyListenerState state,
        CancellationToken cancellationToken)
    {
        ProxyListenerStatus? matched = null;
        var observed = "";
        await UntilAsync(
            () =>
            {
                var snapshot = runtimeState.Snapshot();
                observed = FormatListeners(snapshot.Listeners);
                matched = snapshot.Listeners.FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(candidate.Kind, kind, StringComparison.OrdinalIgnoreCase)
                    && candidate.State == state);
                return matched is not null;
            },
            () => $"Timed out waiting for listener {name}/{kind}/{state}. Observed listeners: {observed}",
            cancellationToken);
        return matched!;
    }

    public static Task WaitForNoListenerAsync(
        ProxyRuntimeState runtimeState,
        string name,
        string kind,
        CancellationToken cancellationToken)
    {
        var observed = "";
        return UntilAsync(
            () =>
            {
                var snapshot = runtimeState.Snapshot();
                observed = FormatListeners(snapshot.Listeners);
                return !snapshot.Listeners.Any(candidate =>
                    string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(candidate.Kind, kind, StringComparison.OrdinalIgnoreCase));
            },
            () => $"Timed out waiting for listener {name}/{kind} removal. Observed listeners: {observed}",
            cancellationToken);
    }

    public static Task WaitForHttp2StreamsToDrainAsync(ProxyMetrics metrics, CancellationToken cancellationToken)
    {
        long observed = -1;
        return UntilAsync(
            () =>
            {
                observed = metrics.Snapshot().ActiveHttp2Streams;
                return observed == 0;
            },
            () => $"Timed out waiting for HTTP/2 stream gauges to drain. ActiveHttp2Streams={observed}.",
            cancellationToken,
            TimeSpan.FromMilliseconds(10));
    }

    public static Task WaitForHttp3StreamsToDrainAsync(ProxyMetrics metrics, CancellationToken cancellationToken)
    {
        long observed = -1;
        return UntilAsync(
            () =>
            {
                observed = metrics.Snapshot().ActiveHttp3Streams;
                return observed == 0;
            },
            () => $"Timed out waiting for HTTP/3 stream gauges to drain. ActiveHttp3Streams={observed}.",
            cancellationToken,
            TimeSpan.FromMilliseconds(10));
    }

    private static string FormatListeners(IReadOnlyList<ProxyListenerStatus> listeners)
    {
        return string.Join(
            ", ",
            listeners.Select(static listener =>
                $"{listener.Name}/{listener.Kind}/{listener.State}/{listener.Address}:{listener.Port}/{listener.LastError ?? "no-error"}"));
    }
}

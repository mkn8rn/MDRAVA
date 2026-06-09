using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane;

public static class ProxyTimeoutPolicy
{
    public static async ValueTask RunAsync(
        Func<CancellationToken, ValueTask> operation,
        TimeSpan timeout,
        ProxyTimeoutKind kind,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await operation(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            throw new ProxyTimeoutException(kind, timeout);
        }
    }

    public static async ValueTask<T> RunAsync<T>(
        Func<CancellationToken, ValueTask<T>> operation,
        TimeSpan timeout,
        ProxyTimeoutKind kind,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            return await operation(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            throw new ProxyTimeoutException(kind, timeout);
        }
    }

    public static RuntimeTimeouts ApplyRouteTimeouts(RuntimeRoute route, RuntimeTimeouts timeouts)
    {
        return timeouts with
        {
            UpstreamResponseHeadTimeout = route.ResolvedOptions.UpstreamResponseHeadTimeout
        };
    }

    public static RuntimeTimeouts ApplyRetryAttemptTimeout(RuntimeRoute route, RuntimeTimeouts timeouts)
    {
        if (route.Retry.PerAttemptTimeout is not { } perAttemptTimeout)
        {
            return timeouts;
        }

        return timeouts with
        {
            UpstreamConnectTimeout = perAttemptTimeout,
            UpstreamResponseHeadTimeout = perAttemptTimeout
        };
    }
}

using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Timeouts;

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

    public static RuntimeTimeouts ApplyRouteTimeouts(ProxyRouteTimeoutPolicyInput input, RuntimeTimeouts timeouts)
    {
        return WithUpstreamTimeouts(
            timeouts,
            timeouts.UpstreamConnectTimeout,
            input.UpstreamResponseHeadTimeout);
    }

    public static RuntimeTimeouts ApplyRetryAttemptTimeout(ProxyRouteTimeoutPolicyInput input, RuntimeTimeouts timeouts)
    {
        if (input.RetryPerAttemptTimeout is not { } perAttemptTimeout)
        {
            return timeouts;
        }

        return WithUpstreamTimeouts(timeouts, perAttemptTimeout, perAttemptTimeout);
    }

    private static RuntimeTimeouts WithUpstreamTimeouts(
        RuntimeTimeouts source,
        TimeSpan upstreamConnectTimeout,
        TimeSpan upstreamResponseHeadTimeout)
    {
        return new RuntimeTimeouts(
            source.ClientRequestHeadTimeout,
            source.ClientRequestBodyIdleTimeout,
            upstreamConnectTimeout,
            upstreamResponseHeadTimeout,
            source.UpstreamResponseBodyIdleTimeout,
            source.DownstreamWriteTimeout,
            source.TlsHandshakeTimeout,
            source.ClientKeepAliveIdleTimeout,
            source.UpstreamIdleConnectionLifetime,
            source.TunnelIdleTimeout);
    }
}

public sealed record ProxyRouteTimeoutPolicyInput(
    TimeSpan UpstreamResponseHeadTimeout,
    TimeSpan? RetryPerAttemptTimeout);

using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Forwarding;
using MDRAVA.BLL.ControlPlane.Metrics;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Net.Sockets;

namespace MDRAVA.INF.Proxy.Forwarding;

public sealed class TunnelRelay
{
    private readonly ProxyMetrics _metrics;
    private readonly ILogger<TunnelRelay> _logger;
    private readonly TimeProvider _timeProvider;

    public TunnelRelay(
        ProxyMetrics metrics,
        ILogger<TunnelRelay> logger,
        TimeProvider timeProvider)
    {
        _metrics = metrics;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async ValueTask<TunnelRelayResult> RelayAsync(
        Stream clientStream,
        Stream upstreamStream,
        RuntimeListener listener,
        RuntimeTimeouts timeouts,
        CancellationToken cancellationToken)
    {
        using var tunnelCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = tunnelCancellation.Token;
        var lastActivity = _timeProvider.GetTimestamp();
        var idleTimedOut = false;
        var relayFailed = false;
        var bytesClientToUpstream = 0L;
        var bytesUpstreamToClient = 0L;
        var started = _timeProvider.GetTimestamp();

        var clientToUpstream = RelayDirectionAsync(
            clientStream,
            upstreamStream,
            listener.ForwardingBufferBytes,
            bytes =>
            {
                Volatile.Write(ref lastActivity, _timeProvider.GetTimestamp());
                Interlocked.Add(ref bytesClientToUpstream, bytes);
                _metrics.AddTunnelBytesClientToUpstream(bytes);
            },
            () => relayFailed = true,
            token).AsTask();
        var upstreamToClient = RelayDirectionAsync(
            upstreamStream,
            clientStream,
            listener.ForwardingBufferBytes,
            bytes =>
            {
                Volatile.Write(ref lastActivity, _timeProvider.GetTimestamp());
                Interlocked.Add(ref bytesUpstreamToClient, bytes);
                _metrics.AddTunnelBytesUpstreamToClient(bytes);
            },
            () => relayFailed = true,
            token).AsTask();
        var idleMonitor = MonitorIdleAsync(
            timeouts.TunnelIdleTimeout,
            () => Volatile.Read(ref lastActivity),
            _timeProvider,
            () =>
            {
                idleTimedOut = true;
                tunnelCancellation.Cancel();
            },
            token);

        try
        {
            var completed = await Task.WhenAny(clientToUpstream, upstreamToClient, idleMonitor);
            if (completed == idleMonitor && idleTimedOut)
            {
                _metrics.TunnelIdleTimedOut();
                _logger.LogDebug("Upgraded tunnel idle timeout elapsed.");
            }
        }
        finally
        {
            await tunnelCancellation.CancelAsync();

            try
            {
                await clientToUpstream;
            }
            catch (Exception exception) when (IsExpectedTunnelEnd(exception, cancellationToken))
            {
            }

            try
            {
                await upstreamToClient;
            }
            catch (Exception exception) when (IsExpectedTunnelEnd(exception, cancellationToken))
            {
            }

            try
            {
                await idleMonitor;
            }
            catch (OperationCanceledException)
            {
            }
        }

        var closeReason = idleTimedOut
            ? "IdleTimeout"
            : relayFailed
                ? "RelayFailure"
                : cancellationToken.IsCancellationRequested
                    ? "Shutdown"
                    : "Closed";

        return new TunnelRelayResult(
            closeReason,
            Interlocked.Read(ref bytesClientToUpstream),
            Interlocked.Read(ref bytesUpstreamToClient),
            _timeProvider.GetElapsedTime(started));
    }

    private async ValueTask RelayDirectionAsync(
        Stream source,
        Stream destination,
        int bufferSize,
        Action<int> onBytesRelayed,
        Action onRelayFailure,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var bytesRead = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (bytesRead == 0)
                {
                    return;
                }

                _metrics.AddBytesRead(bytesRead);
                onBytesRelayed(bytesRead);
                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                await destination.FlushAsync(cancellationToken);
                _metrics.AddBytesWritten(bytesRead);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is IOException or SocketException)
        {
            _metrics.TunnelRelayFailed();
            onRelayFailure();
            _logger.LogDebug(exception, "Upgraded tunnel relay ended with an I/O failure.");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task MonitorIdleAsync(
        TimeSpan idleTimeout,
        Func<long> getLastActivityTimestamp,
        TimeProvider timeProvider,
        Action onIdleTimeout,
        CancellationToken cancellationToken)
    {
        var pollInterval = TimeSpan.FromMilliseconds(Math.Min(250, Math.Max(25, idleTimeout.TotalMilliseconds / 4)));
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(pollInterval, timeProvider, cancellationToken);
            var elapsed = timeProvider.GetElapsedTime(getLastActivityTimestamp());
            if (elapsed >= idleTimeout)
            {
                onIdleTimeout();
                return;
            }
        }
    }

    private static bool IsExpectedTunnelEnd(Exception exception, CancellationToken outerToken)
    {
        return exception is OperationCanceledException
            || exception is IOException
            || exception is SocketException
            || outerToken.IsCancellationRequested;
    }
}

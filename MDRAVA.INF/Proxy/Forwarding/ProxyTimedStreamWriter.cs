using MDRAVA.BLL.ControlPlane.Timeouts;

namespace MDRAVA.INF.Proxy.Forwarding;

internal static class ProxyTimedStreamWriter
{
    public static async ValueTask WriteAsync(
        Stream destination,
        ReadOnlyMemory<byte> bytes,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await ProxyTimeoutPolicy.RunAsync(
            async timeoutToken =>
            {
                await destination.WriteAsync(bytes, timeoutToken);
            },
            timeout,
            ProxyTimeoutKind.DownstreamWrite,
            cancellationToken);
    }
}

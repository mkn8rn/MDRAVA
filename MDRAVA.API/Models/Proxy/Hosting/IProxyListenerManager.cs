
namespace MDRAVA.API.Proxy.Hosting;

public interface IProxyListenerManager
{
    ValueTask<ProxyListenerReloadResult> ApplyReloadAsync(
        ProxyConfigurationSnapshot snapshot,
        Func<ProxyConfigurationSnapshot, ProxyConfigurationSnapshot> activateSnapshot,
        CancellationToken cancellationToken);

    IReadOnlyList<ProxyListenerStatus> Snapshot();
}

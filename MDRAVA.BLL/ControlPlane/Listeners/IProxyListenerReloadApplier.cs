using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Listeners;

public interface IProxyListenerReloadApplier
{
    ValueTask<ProxyListenerReloadResult> ApplyReloadAsync(
        ProxyConfigurationSnapshot snapshot,
        Func<ProxyConfigurationSnapshot, ProxyConfigurationSnapshot> activateSnapshot,
        CancellationToken cancellationToken);
}

using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Listeners;

namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public abstract partial record ProxyConfigurationReloadResult<TProjection>
    where TProjection : class
{
    public sealed record ReloadedResult : ProxyConfigurationReloadResult<TProjection>
    {
        internal ReloadedResult(
            string sourceDirectory,
            DateTimeOffset attemptedAtUtc,
            int activeVersion,
            DateTimeOffset loadedAtUtc,
            ProxyConfigurationDiscovery discovery,
            ProxyListenerReloadResult listenerReload,
            TProjection activeConfiguration)
            : base(
                sourceDirectory,
                attemptedAtUtc,
                activeVersion,
                loadedAtUtc,
                loadedAtUtc,
                discovery,
                [],
                [])
        {
            if (listenerReload is not ProxyListenerReloadResult.AppliedResult)
            {
                throw new ArgumentException("A successful reload result requires a successful listener reload.", nameof(listenerReload));
            }

            ArgumentNullException.ThrowIfNull(activeConfiguration);
            ListenerReload = listenerReload;
            ActiveConfiguration = activeConfiguration;
        }

        public ProxyListenerReloadResult ListenerReload { get; }

        public TProjection ActiveConfiguration { get; }
    }
}

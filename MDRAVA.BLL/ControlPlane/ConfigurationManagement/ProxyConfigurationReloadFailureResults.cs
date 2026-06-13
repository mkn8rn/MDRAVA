using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Listeners;

namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public abstract partial record ProxyConfigurationReloadResult<TProjection>
    where TProjection : class
{
    public sealed record LoadFailedResult : ProxyConfigurationReloadResult<TProjection>
    {
        internal LoadFailedResult(
            string sourceDirectory,
            DateTimeOffset attemptedAtUtc,
            int? activeVersion,
            DateTimeOffset? loadedAtUtc,
            ProxyConfigurationDiscovery discovery,
            IReadOnlyList<string> errors,
            IReadOnlyList<ProxyConfigurationFileError> fileErrors,
            TProjection? activeConfiguration)
            : base(
                sourceDirectory,
                attemptedAtUtc,
                activeVersion,
                loadedAtUtc,
                loadedAtUtc,
                discovery,
                errors,
                fileErrors)
        {
            ActiveConfiguration = activeConfiguration;
        }

        public TProjection? ActiveConfiguration { get; }
    }

    public sealed record ListenerReloadFailedResult : ProxyConfigurationReloadResult<TProjection>
    {
        internal ListenerReloadFailedResult(
            string sourceDirectory,
            DateTimeOffset attemptedAtUtc,
            int? activeVersion,
            DateTimeOffset? loadedAtUtc,
            ProxyConfigurationDiscovery discovery,
            ProxyListenerReloadResult listenerReload,
            TProjection? activeConfiguration)
            : base(
                sourceDirectory,
                attemptedAtUtc,
                activeVersion,
                loadedAtUtc,
                loadedAtUtc,
                discovery,
                listenerReload.Errors,
                listenerReload.Errors.Select(static error => ProxyConfigurationFileError.Global(error)).ToArray())
        {
            if (listenerReload is not ProxyListenerReloadResult.FailedResult)
            {
                throw new ArgumentException("A listener reload failure result requires a failed listener reload.", nameof(listenerReload));
            }

            ListenerReload = listenerReload;
            ActiveConfiguration = activeConfiguration;
        }

        public ProxyListenerReloadResult ListenerReload { get; }

        public TProjection? ActiveConfiguration { get; }
    }
}

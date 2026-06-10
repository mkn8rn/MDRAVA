using MDRAVA.API.Proxy.Hosting;
using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.INF.Proxy.Health;

namespace MDRAVA.Tests;

internal static class ProxyStatusOperationFactory
{
    public static ProxyStatusOperations Create(
        ProxyRuntimeState runtime,
        ProxyMetrics metrics,
        ProxyConfigurationStore store,
        UpstreamHealthStore health,
        IProxyLogPersistenceStore? logPersistenceStore = null,
        ResponseCacheStore? cacheStore = null,
        AcmeCertificateStatusStore? acmeStatusStore = null,
        ProxyRuntimePreflightService? preflightService = null,
        IProxyConfigLintOperations? lintOperations = null,
        TimeProvider? timeProvider = null)
    {
        var cache = cacheStore ?? new ResponseCacheStore(TimeProvider.System);
        var acme = acmeStatusStore ?? new AcmeCertificateStatusStore();
        IProxyStatusRuntimePreflightSource preflightSource = preflightService is null
            ? FixedRuntimePreflightSource.Instance
            : preflightService;

        var inputReader = new ProxyStatusInputReader(
            runtime,
            metrics,
            store,
            health,
            lintOperations ?? FixedConfigLintOperations.Instance,
            logPersistenceStore ?? FixedLogPersistenceStore.Instance,
            new ProxyCacheStatusReader(
                new ProxyCacheStatusConfigurationSource(store),
                new ProxyCacheRuntimeStatusSource(cache)),
            new ProxyAcmeCertificateLifecycleStatusSource(acme),
            preflightSource,
            TestHttp3PlatformSupport.SupportedSource,
            timeProvider ?? TimeProvider.System);
        return new ProxyStatusOperations(inputReader);
    }

    private sealed class FixedConfigLintOperations : IProxyConfigLintOperations
    {
        public static FixedConfigLintOperations Instance { get; } = new();

        public ConfigLintStatus LastActiveStatus => ConfigLintStatus.Empty;

        public ConfigLintResult LintActive()
        {
            return EmptyResult();
        }

        public ConfigLintResult LintSubmitted(ConfigLintRequest request)
        {
            return EmptyResult();
        }

        private static ConfigLintResult EmptyResult()
        {
            return new ConfigLintResult(
                true,
                DateTimeOffset.UnixEpoch,
                ConfigLintSummary.Empty,
                [],
                []);
        }
    }

    private sealed class FixedLogPersistenceStore : IProxyLogPersistenceStore
    {
        public static FixedLogPersistenceStore Instance { get; } = new();

        public void WriteAccess(ProxyAccessLogEntry entry)
        {
        }

        public void WriteAdminAudit(ProxyAdminAuditEvent auditEvent)
        {
        }

        public ProxyLogPersistenceStatus GetStatus()
        {
            return ProxyLogPersistenceStatus.Unknown;
        }
    }

    private sealed class FixedRuntimePreflightSource : IProxyStatusRuntimePreflightSource
    {
        public static FixedRuntimePreflightSource Instance { get; } = new();

        public ProxyRuntimePreflightStatus ReadRuntimePreflight()
        {
            return ProxyRuntimePreflightStatus.Unknown;
        }
    }
}

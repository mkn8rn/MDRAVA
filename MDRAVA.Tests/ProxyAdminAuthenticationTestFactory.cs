using MDRAVA.API.Proxy.Security;
using MDRAVA.INF.Observability;
using MDRAVA.INF.Runtime;
using Microsoft.AspNetCore.Http;

namespace MDRAVA.Tests;

internal static class ProxyAdminAuthenticationTestFactory
{
    public static AdminAuthenticationMiddleware CreateMiddleware(
        RequestDelegate next,
        IProxyConfigurationStore store,
        AdminAuditStore audit,
        ProxyMetrics? metrics = null,
        TimeProvider? timeProvider = null)
    {
        return new AdminAuthenticationMiddleware(
            next,
            new ProxyAdminAuthenticationService(
                new ProxyAdminSecurityOptionsReader(store),
                audit,
                metrics ?? new ProxyMetrics(),
                SilentProxyAdminAuthenticationEventSink.Instance,
                timeProvider ?? TimeProvider.System));
    }

    private sealed class SilentProxyAdminAuthenticationEventSink : IProxyAdminAuthenticationEventSink
    {
        public static SilentProxyAdminAuthenticationEventSink Instance { get; } = new();

        private SilentProxyAdminAuthenticationEventSink()
        {
        }

        public void ActiveConfigurationMissing()
        {
        }
    }
}

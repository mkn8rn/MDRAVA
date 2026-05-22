using MDRAVA.API.Controllers;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.API.Proxy.Observability;

namespace MDRAVA.Tests;

internal static class ObservabilityTests
{
    public static void RecentDiagnosticsStoreIsBounded()
    {
        var metrics = new ProxyMetrics();
        var store = new RecentRequestDiagnosticsStore(metrics);

        store.Add(CreateEvent("one", "/one"), capacity: 2);
        store.Add(CreateEvent("two", "/two"), capacity: 2);
        store.Add(CreateEvent("three", "/three"), capacity: 2);

        var recent = store.Recent(10);
        AssertEx.Equal(2, recent.Count);
        AssertEx.Equal("three", recent[0].RequestId);
        AssertEx.Equal("two", recent[1].RequestId);
        AssertEx.Equal(1L, metrics.Snapshot().RecentDiagnosticsOverwritten);
    }

    public static void DiagnosticsControllerHonorsSafeLimit()
    {
        var metrics = new ProxyMetrics();
        var store = new RecentRequestDiagnosticsStore(metrics);
        for (var index = 0; index < 10; index++)
        {
            store.Add(CreateEvent($"request-{index}", $"/{index}"), capacity: 20);
        }

        var controller = new ProxyDiagnosticsController(
            new ProxyDiagnosticsAdministrationService(
                new ProxyRequestDiagnosticsReader(store)));
        var recent = controller.Recent(limit: 3);

        AssertEx.Equal(3, recent.Count);
        AssertEx.Equal("request-9", recent[0].RequestId);
    }

    public static void DiagnosticsEventDoesNotCarryBodiesOrSecrets()
    {
        var diagnostic = CreateEvent("request", "/secret");

        AssertEx.Equal("/secret", diagnostic.Target);
        AssertEx.False(diagnostic.ToString().Contains("request-body", StringComparison.OrdinalIgnoreCase));
        AssertEx.False(diagnostic.ToString().Contains("certificate-password", StringComparison.OrdinalIgnoreCase));
    }

    private static ProxyRequestDiagnosticEvent CreateEvent(string requestId, string target)
    {
        return new ProxyRequestDiagnosticEvent(
            DateTimeOffset.UtcNow,
            requestId,
            null,
            1,
            "main",
            "Http",
            "127.0.0.1:12345",
            "GET",
            "example.test",
            target,
            "home",
            "local-test",
            "127.0.0.1:5000",
            200,
            10,
            ProxyFailureKind.None.ToString(),
            true,
            false,
            false,
            false,
            null,
            0,
            0);
    }
}

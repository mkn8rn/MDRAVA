using System.Net;
using System.Net.Sockets;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Connections;
using MDRAVA.API.Proxy.Health;
using MDRAVA.API.Proxy.Metrics;

namespace MDRAVA.Tests;

internal static class HealthCheckTests
{
    public static async Task HealthCheck2xxIsHealthy()
    {
        var sample = await RunHealthCheckAsync("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");

        AssertEx.True(sample.Healthy, sample.Result);
    }

    public static async Task HealthCheck3xxIsHealthy()
    {
        var sample = await RunHealthCheckAsync("HTTP/1.1 302 Found\r\nContent-Length: 0\r\n\r\n");

        AssertEx.True(sample.Healthy, sample.Result);
    }

    public static async Task HealthCheck4xxIsUnhealthy()
    {
        var sample = await RunHealthCheckAsync("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\n\r\n");

        AssertEx.False(sample.Healthy, sample.Result);
    }

    public static async Task HealthCheck5xxIsUnhealthy()
    {
        var sample = await RunHealthCheckAsync("HTTP/1.1 500 Internal Server Error\r\nContent-Length: 0\r\n\r\n");

        AssertEx.False(sample.Healthy, sample.Result);
    }

    public static async Task HealthCheckTimeoutIsUnhealthy()
    {
        var port = GetFreeTcpPort();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var upstream = Upstream(port);
        var route = Route([upstream], timeoutSeconds: 1);
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();

        try
        {
            var acceptTask = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync(timeout.Token);
                await Task.Delay(TimeSpan.FromMilliseconds(1500), timeout.Token);
            }, timeout.Token);

            var sample = await new UpstreamHealthCheckClient(new UpstreamConnectionFactory())
                .CheckAsync(route, upstream, timeout.Token);

            AssertEx.False(sample.Healthy, sample.Result);
            AssertEx.True(sample.Result.Contains("timeout", StringComparison.OrdinalIgnoreCase), sample.Result);
            await acceptTask;
        }
        finally
        {
            listener.Stop();
        }
    }

    public static void HealthStateTransitionsToUnhealthyAfterThreshold()
    {
        var metrics = new ProxyMetrics();
        using var pool = new UpstreamConnectionPool(new UpstreamConnectionFactory(), metrics);
        var store = new UpstreamHealthStore(metrics, pool);
        var upstream = Upstream(5001);
        var route = Route([upstream], unhealthyThreshold: 2);

        store.RecordHealthCheckResult(route, upstream, new HealthCheckSample(false, "fail"), DateTimeOffset.UtcNow);
        AssertEx.True(store.IsUsable(upstream));

        var state = store.RecordHealthCheckResult(route, upstream, new HealthCheckSample(false, "fail"), DateTimeOffset.UtcNow);

        AssertEx.Equal(UpstreamHealthState.Unhealthy, state);
        AssertEx.False(store.IsUsable(upstream));
    }

    public static void HealthStateTransitionsToHealthyAfterRecoveryThreshold()
    {
        var metrics = new ProxyMetrics();
        using var pool = new UpstreamConnectionPool(new UpstreamConnectionFactory(), metrics);
        var store = new UpstreamHealthStore(metrics, pool);
        var upstream = Upstream(5001);
        var route = Route([upstream], healthyThreshold: 2, unhealthyThreshold: 1);

        store.RecordHealthCheckResult(route, upstream, new HealthCheckSample(false, "fail"), DateTimeOffset.UtcNow);
        AssertEx.False(store.IsUsable(upstream));

        store.RecordHealthCheckResult(route, upstream, new HealthCheckSample(true, "ok"), DateTimeOffset.UtcNow);
        AssertEx.False(store.IsUsable(upstream));

        var state = store.RecordHealthCheckResult(route, upstream, new HealthCheckSample(true, "ok"), DateTimeOffset.UtcNow);

        AssertEx.Equal(UpstreamHealthState.Healthy, state);
        AssertEx.True(store.IsUsable(upstream));
    }

    private static async Task<HealthCheckSample> RunHealthCheckAsync(string response)
    {
        var port = GetFreeTcpPort();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var upstream = Upstream(port);
        var route = Route([upstream]);
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();

        try
        {
            var serverTask = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync(timeout.Token);
                await using var stream = client.GetStream();
                await ReadRequestHeadAsync(stream, timeout.Token);
                await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes(response), timeout.Token);
            }, timeout.Token);

            var sample = await new UpstreamHealthCheckClient(new UpstreamConnectionFactory())
                .CheckAsync(route, upstream, timeout.Token);
            await serverTask;
            return sample;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static RuntimeRoute Route(
        IReadOnlyList<RuntimeUpstream> upstreams,
        int timeoutSeconds = 1,
        int healthyThreshold = 2,
        int unhealthyThreshold = 2)
    {
        return new RuntimeRoute(
            "test",
            "*",
            "/",
            RuntimeRouteAction.Proxy,
            "round-robin",
            new RuntimeHealthCheckOptions(
                true,
                "/health",
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(timeoutSeconds),
                healthyThreshold,
                unhealthyThreshold),
            upstreams,
            new RuntimeHttpsRedirectPolicy(false, 308, null),
            new RuntimeCanonicalHostPolicy(false, "", 308),
            RuntimeHeaderPolicy.Empty,
            new RuntimePathRewritePolicy("", "", ""),
            new RuntimeRedirectPolicy(308, "", "", true),
            new RuntimeStaticResponse(200, "text/plain; charset=utf-8", ""),
            new RuntimeMaintenancePolicy(false, null, "text/plain; charset=utf-8", "Service Unavailable"),
            new RuntimeRouteResolvedOptions(
                100L * 1024 * 1024,
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30),
                true));
    }

    private static RuntimeUpstream Upstream(int port)
    {
        return new RuntimeUpstream("test", $"upstream-{port}", "127.0.0.1", port, 1);
    }

    private static async Task ReadRequestHeadAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        var total = 0;
        while (total < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(total, 1), cancellationToken);
            if (bytesRead == 0)
            {
                return;
            }

            total += bytesRead;
            if (total >= 4
                && buffer[total - 4] == '\r'
                && buffer[total - 3] == '\n'
                && buffer[total - 2] == '\r'
                && buffer[total - 1] == '\n')
            {
                return;
            }
        }
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}

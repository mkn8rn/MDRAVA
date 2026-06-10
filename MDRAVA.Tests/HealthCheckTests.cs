using System.Net;
using System.Net.Sockets;
using MDRAVA.INF.Proxy.Connections;
using MDRAVA.INF.Proxy.Health;

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

    public static async Task HealthCheckCoordinatorRunsDueChecksAndRecordsMetrics()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var metrics = new ProxyMetrics();
        using var pool = new UpstreamConnectionPool(new UpstreamConnectionFactory(), metrics);
        var store = new UpstreamHealthStore(metrics, pool);
        var upstream = Upstream(5001);
        var route = Route([upstream], healthyThreshold: 1);
        var client = new FixedHealthCheckClient(new HealthCheckSample(true, "ok"));
        var events = new CapturingHealthCheckEventSink();
        var coordinator = new UpstreamHealthCheckCoordinator(client, store, metrics, clock, events);

        await coordinator.RunDueChecksAsync(Snapshot(route), CancellationToken.None);

        AssertEx.Equal(1, client.Calls);
        var counters = metrics.Snapshot();
        AssertEx.Equal(1L, counters.HealthChecksAttempted);
        AssertEx.Equal(1L, counters.HealthChecksSucceeded);
        AssertEx.Equal(0L, counters.HealthChecksFailed);
        AssertEx.True(store.IsUsable(upstream));
        AssertEx.Equal(1, events.Events.Count);
        AssertEx.Equal(UpstreamHealthState.Healthy, events.Events[0].State);
    }

    public static async Task HealthCheckCoordinatorSkipsUntilIntervalElapses()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var metrics = new ProxyMetrics();
        using var pool = new UpstreamConnectionPool(new UpstreamConnectionFactory(), metrics);
        var store = new UpstreamHealthStore(metrics, pool);
        var upstream = Upstream(5001);
        var route = Route([upstream]);
        var client = new FixedHealthCheckClient(new HealthCheckSample(false, "fail"));
        var coordinator = new UpstreamHealthCheckCoordinator(
            client,
            store,
            metrics,
            clock,
            new CapturingHealthCheckEventSink());
        var snapshot = Snapshot(route);

        await coordinator.RunDueChecksAsync(snapshot, CancellationToken.None);
        await coordinator.RunDueChecksAsync(snapshot, CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(2));
        await coordinator.RunDueChecksAsync(snapshot, CancellationToken.None);

        AssertEx.Equal(2, client.Calls);
        var counters = metrics.Snapshot();
        AssertEx.Equal(2L, counters.HealthChecksAttempted);
        AssertEx.Equal(2L, counters.HealthChecksFailed);
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
            RuntimeCachePolicy.Disabled,
            new RuntimeRouteResolvedOptions(
                100L * 1024 * 1024,
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30),
                true));
    }

    private static ProxyConfigurationSnapshot Snapshot(RuntimeRoute route)
    {
        return ProxyConfigurationRuntimeMapper.ToRuntimeSnapshot(
            new ProxyOptions(),
            new ProxyOperationalOptions(),
            ProxyAdminSecurityTokenPolicy.Resolve(new ProxyAdminOptions(), static _ => null),
            new Dictionary<string, RuntimeCertificate>(StringComparer.OrdinalIgnoreCase),
            1,
            DateTimeOffset.UnixEpoch,
            "tests",
            [],
            new ProxyConfigurationDiscovery(
                new ProxyFilesystemLayout("tests", "tests/config", "tests/config/sites", "tests/logs", "tests/certs", "tests/state", "tests/config/proxy.json"),
                [],
                [],
                [])) with
        {
            Routes = [route]
        };
    }

    private static RuntimeUpstream Upstream(int port)
    {
        return new RuntimeUpstream(
            "test",
            $"upstream-{port}",
            "http",
            RuntimeUpstreamProtocol.Http1,
            "127.0.0.1",
            port,
            1,
            RuntimeUpstreamTlsOptions.Default);
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

    private sealed class FixedHealthCheckClient : IUpstreamHealthCheckClient
    {
        private readonly HealthCheckSample _sample;

        public FixedHealthCheckClient(HealthCheckSample sample)
        {
            _sample = sample;
        }

        public int Calls { get; private set; }

        public ValueTask<HealthCheckSample> CheckAsync(
            RuntimeRoute route,
            RuntimeUpstream upstream,
            CancellationToken cancellationToken)
        {
            _ = route;
            _ = upstream;
            _ = cancellationToken;
            Calls++;
            return ValueTask.FromResult(_sample);
        }
    }

    private sealed class CapturingHealthCheckEventSink : IUpstreamHealthCheckEventSink
    {
        public List<HealthCheckEvent> Events { get; } = [];

        public void Checked(
            string routeName,
            string upstreamName,
            string endpoint,
            string result,
            UpstreamHealthState state)
        {
            Events.Add(new HealthCheckEvent(routeName, upstreamName, endpoint, result, state));
        }
    }

    private sealed record HealthCheckEvent(
        string RouteName,
        string UpstreamName,
        string Endpoint,
        string Result,
        UpstreamHealthState State);

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void Advance(TimeSpan delay)
        {
            _utcNow += delay;
        }
    }
}

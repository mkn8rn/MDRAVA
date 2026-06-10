using System.Net;

namespace MDRAVA.Tests;

internal static class HardeningTests
{
    public static void AdmissionControllerEnforcesClientLimit()
    {
        var metrics = new ProxyMetrics();
        var admission = new ProxyAdmissionController(metrics);

        using var first = admission.TryAcquireClientConnection(1);
        var second = admission.TryAcquireClientConnection(1);

        AssertEx.True(first is not null);
        AssertEx.Equal(null, second);
        AssertEx.Equal(1L, metrics.Snapshot().ConnectionAdmissionRejections);
    }

    public static void AdmissionLeaseDisposalReleasesClientSlot()
    {
        var metrics = new ProxyMetrics();
        var admission = new ProxyAdmissionController(metrics);

        using (var lease = admission.TryAcquireClientConnection(1))
        {
            AssertEx.True(lease is not null);
            AssertEx.Equal(1, admission.ActiveClientConnections);
            AssertEx.Equal(null, admission.TryAcquireClientConnection(1));
        }

        using var reacquired = admission.TryAcquireClientConnection(1);
        AssertEx.True(reacquired is not null);
        AssertEx.Equal(1, admission.ActiveClientConnections);
    }

    public static void AdmissionControllerEnforcesTlsHandshakeLimit()
    {
        var metrics = new ProxyMetrics();
        var admission = new ProxyAdmissionController(metrics);

        using var first = admission.TryAcquireTlsHandshake(1);
        var second = admission.TryAcquireTlsHandshake(1);

        AssertEx.True(first is not null);
        AssertEx.Equal(null, second);
        AssertEx.Equal(1L, metrics.Snapshot().TlsHandshakeAdmissionRejections);
        AssertEx.Equal(1L, metrics.Snapshot().ActiveTlsHandshakes);
    }

    public static void AdmissionLeaseDisposalReleasesTlsHandshakeSlot()
    {
        var metrics = new ProxyMetrics();
        var admission = new ProxyAdmissionController(metrics);

        using (var lease = admission.TryAcquireTlsHandshake(1))
        {
            AssertEx.True(lease is not null);
            AssertEx.Equal(1, admission.ActiveTlsHandshakes);
            AssertEx.Equal(1L, metrics.Snapshot().ActiveTlsHandshakes);
            AssertEx.Equal(null, admission.TryAcquireTlsHandshake(1));
        }

        using var reacquired = admission.TryAcquireTlsHandshake(1);
        AssertEx.True(reacquired is not null);
        AssertEx.Equal(1, admission.ActiveTlsHandshakes);
        AssertEx.Equal(1L, metrics.Snapshot().ActiveTlsHandshakes);
    }

    public static void RateLimiterEnforcesRequestLimitAndRefills()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero));
        var metrics = new ProxyMetrics();
        var limiter = new ClientRateLimiter(metrics, clock);
        var ip = "127.0.0.1";

        AssertEx.True(limiter.TryAcquireRequest(ip, 1));
        AssertEx.False(limiter.TryAcquireRequest(ip, 1));
        clock.Advance(TimeSpan.FromSeconds(61));
        AssertEx.True(limiter.TryAcquireRequest(ip, 1));
        AssertEx.Equal(1L, metrics.Snapshot().RateLimitedRequests);
    }

    public static void ConcurrentRateLimiterBoundaryAllowsOnlyConfiguredLimit()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 6, 10, 10, 5, 0, TimeSpan.Zero));
        var metrics = new ProxyMetrics();
        var limiter = new ClientRateLimiter(metrics, clock);
        var ip = "127.0.0.1";
        var allowed = 0;

        Parallel.For(
            0,
            32,
            _ =>
            {
                if (limiter.TryAcquireRequest(ip, 5))
                {
                    Interlocked.Increment(ref allowed);
                }
            });

        AssertEx.Equal(5, allowed);
        AssertEx.Equal(27L, metrics.Snapshot().RateLimitedRequests);
    }

    public static void RateLimiterEnforcesUpgradeLimit()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 6, 10, 10, 10, 0, TimeSpan.Zero));
        var metrics = new ProxyMetrics();
        var limiter = new ClientRateLimiter(metrics, clock);
        var ip = "127.0.0.1";

        AssertEx.True(limiter.TryAcquireUpgrade(ip, 1));
        AssertEx.False(limiter.TryAcquireUpgrade(ip, 1));
        AssertEx.Equal(1L, metrics.Snapshot().RateLimitedUpgrades);
    }

    public static void RateLimiterUsesNormalizedClientAddressKeys()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 6, 10, 10, 15, 0, TimeSpan.Zero));
        var metrics = new ProxyMetrics();
        var limiter = new ClientRateLimiter(metrics, clock);

        var firstKey = MDRAVA.INF.Proxy.RuntimeGuards.ProxyClientAddressPolicy.NormalizeRequiredClientIp(IPAddress.Parse("127.0.0.1"));
        var secondKey = MDRAVA.INF.Proxy.RuntimeGuards.ProxyClientAddressPolicy.NormalizeRequiredClientIp(IPAddress.Parse("::ffff:127.0.0.1"));

        AssertEx.True(limiter.TryAcquireRequest(firstKey, 1));
        AssertEx.False(limiter.TryAcquireRequest(secondKey, 1));
        AssertEx.Equal(1, limiter.EntryCount);
        AssertEx.Equal(1L, metrics.Snapshot().RateLimitedRequests);
    }

    public static void RateLimiterCleansStaleEntries()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 6, 10, 10, 20, 0, TimeSpan.Zero));
        var metrics = new ProxyMetrics();
        var limiter = new ClientRateLimiter(metrics, clock);

        for (var index = 0; index < 300; index++)
        {
            limiter.TryAcquireRequest($"10.0.0.{index % 250}", 10);
        }

        AssertEx.True(limiter.EntryCount > 0);
        clock.Advance(TimeSpan.FromMinutes(6));
        for (var index = 0; index < 256; index++)
        {
            limiter.TryAcquireRequest($"10.1.0.{index % 250}", 10);
        }

        AssertEx.True(limiter.EntryCount < 300);
    }

    public static void ShutdownCoordinatorExposesGraceDeadlineAndCancels()
    {
        using var coordinator = new ProxyShutdownCoordinator();

        var token = coordinator.BeginShutdown(TimeSpan.FromMilliseconds(100));

        AssertEx.True(coordinator.IsShuttingDown);
        AssertEx.True(coordinator.StartedAtUtc is not null);
        AssertEx.True(coordinator.DeadlineUtc is not null);
        AssertEx.False(token.IsCancellationRequested);
        Thread.Sleep(250);
        AssertEx.True(token.IsCancellationRequested);
    }

    public static void ShutdownCoordinatorBeginShutdownIsIdempotent()
    {
        using var coordinator = new ProxyShutdownCoordinator();

        var firstToken = coordinator.BeginShutdown(TimeSpan.FromSeconds(5));
        var firstStartedAtUtc = coordinator.StartedAtUtc;
        var firstDeadlineUtc = coordinator.DeadlineUtc;
        var secondToken = coordinator.BeginShutdown(TimeSpan.FromMinutes(1));

        AssertEx.Equal(firstToken, secondToken);
        AssertEx.Equal(firstStartedAtUtc, coordinator.StartedAtUtc);
        AssertEx.Equal(firstDeadlineUtc, coordinator.DeadlineUtc);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly object _gate = new();
        private DateTimeOffset _utcNow;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            lock (_gate)
            {
                return _utcNow;
            }
        }

        public void Advance(TimeSpan interval)
        {
            lock (_gate)
            {
                _utcNow = _utcNow.Add(interval);
            }
        }
    }
}

using System.Net;
using MDRAVA.API.Proxy.Metrics;

namespace MDRAVA.API.Proxy.Runtime;

public sealed class ClientRateLimiter
{
    private static readonly TimeSpan StaleEntryAge = TimeSpan.FromMinutes(5);

    private readonly ProxyMetrics _metrics;
    private readonly Func<DateTimeOffset> _getUtcNow;
    private readonly object _gate = new();
    private readonly Dictionary<string, BucketSet> _buckets = new(StringComparer.Ordinal);
    private long _operationCount;

    public ClientRateLimiter(ProxyMetrics metrics)
        : this(metrics, static () => DateTimeOffset.UtcNow)
    {
    }

    public ClientRateLimiter(ProxyMetrics metrics, Func<DateTimeOffset> getUtcNow)
    {
        _metrics = metrics;
        _getUtcNow = getUtcNow;
    }

    public bool TryAcquireRequest(IPAddress? ipAddress, int perMinuteLimit)
    {
        if (ipAddress is null)
        {
            return true;
        }

        var allowed = TryAcquire(ipAddress, perMinuteLimit, static buckets => buckets.Requests);
        if (!allowed)
        {
            _metrics.RequestRateLimited();
        }

        return allowed;
    }

    public bool TryAcquireUpgrade(IPAddress? ipAddress, int perMinuteLimit)
    {
        if (ipAddress is null)
        {
            return true;
        }

        var allowed = TryAcquire(ipAddress, perMinuteLimit, static buckets => buckets.Upgrades);
        if (!allowed)
        {
            _metrics.UpgradeRateLimited();
        }

        return allowed;
    }

    public int EntryCount
    {
        get
        {
            lock (_gate)
            {
                return _buckets.Count;
            }
        }
    }

    private bool TryAcquire(IPAddress ipAddress, int perMinuteLimit, Func<BucketSet, TokenBucket> selectBucket)
    {
        var key = NormalizeIp(ipAddress);
        var now = _getUtcNow();

        lock (_gate)
        {
            if (!_buckets.TryGetValue(key, out var buckets))
            {
                buckets = new BucketSet(now);
                _buckets.Add(key, buckets);
            }

            buckets.LastSeenUtc = now;
            var bucket = selectBucket(buckets);
            var allowed = bucket.TryConsume(now, perMinuteLimit);

            if (Interlocked.Increment(ref _operationCount) % 256 == 0)
            {
                Cleanup(now);
            }

            return allowed;
        }
    }

    private void Cleanup(DateTimeOffset now)
    {
        var staleKeys = _buckets
            .Where(pair => now - pair.Value.LastSeenUtc > StaleEntryAge)
            .Select(static pair => pair.Key)
            .ToArray();

        foreach (var staleKey in staleKeys)
        {
            _buckets.Remove(staleKey);
        }
    }

    private static string NormalizeIp(IPAddress address)
    {
        return address.IsIPv4MappedToIPv6
            ? address.MapToIPv4().ToString()
            : address.ToString();
    }

    private sealed class BucketSet
    {
        public BucketSet(DateTimeOffset now)
        {
            LastSeenUtc = now;
            Requests = new TokenBucket(now);
            Upgrades = new TokenBucket(now);
        }

        public DateTimeOffset LastSeenUtc { get; set; }

        public TokenBucket Requests { get; }

        public TokenBucket Upgrades { get; }
    }

    private sealed class TokenBucket
    {
        private double _tokens;
        private DateTimeOffset _lastRefillUtc;
        private bool _initialized;

        public TokenBucket(DateTimeOffset now)
        {
            _lastRefillUtc = now;
        }

        public bool TryConsume(DateTimeOffset now, int perMinuteLimit)
        {
            var capacity = Math.Max(1, perMinuteLimit);
            if (!_initialized)
            {
                _tokens = capacity;
                _initialized = true;
            }

            var elapsedSeconds = Math.Max(0, (now - _lastRefillUtc).TotalSeconds);
            _tokens = Math.Min(capacity, _tokens + elapsedSeconds * capacity / 60d);
            _lastRefillUtc = now;

            if (_tokens < 1d)
            {
                return false;
            }

            _tokens -= 1d;
            return true;
        }
    }
}

using System.Text;
using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Caching;

public sealed class ResponseCacheStore : IProxyCacheControl
{
    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();
    private readonly Dictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _rejections = new(StringComparer.OrdinalIgnoreCase);
    private long _approximateBytes;
    private long _hits;
    private long _misses;
    private long _stores;
    private long _evictions;
    private long _storeRejections;
    private DateTimeOffset? _lastClearedAtUtc;
    private string? _lastClearReason;

    public ResponseCacheStore(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public bool TryGet(
        RuntimeRoute route,
        RuntimeListener listener,
        Http1RequestHead requestHead,
        string upstreamTarget,
        out CachedProxyResponse? response)
    {
        response = null;
        if (!TryCreateKey(route, listener, requestHead, upstreamTarget, out var key, out _))
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow();
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var entry))
            {
                _misses++;
                return false;
            }

            if (entry.ExpiresAtUtc <= now)
            {
                RemoveEntry(key, entry);
                _misses++;
                return false;
            }

            entry.LastAccessedAtUtc = now;
            _hits++;
            response = entry.ToResponse();
            return true;
        }
    }

    public void Store(
        RuntimeRoute route,
        RuntimeListener listener,
        Http1RequestHead requestHead,
        string upstreamTarget,
        Http1ResponseHead responseHead,
        IReadOnlyList<Http1HeaderField> responseHeaders,
        byte[] body)
    {
        if (!TryCreateKey(route, listener, requestHead, upstreamTarget, out var key, out var rejectionReason))
        {
            if (rejectionReason is not null)
            {
                RecordRejection(rejectionReason);
            }

            return;
        }

        var responseEligibility = ProxyCacheEligibilityPolicy.EvaluateStoredResponse(
            route,
            responseHead,
            body.LongLength,
            out var ttl);
        if (!responseEligibility.CanCache)
        {
            RecordRejection(responseEligibility.RejectionReason ?? ProxyCacheEligibilityPolicy.ReasonTtl);
            return;
        }

        var storedHeaders = SanitizeStoredHeaders(responseHeaders);
        var sizeBytes = CalculateSize(storedHeaders, body);
        if (sizeBytes > route.Cache.MaxEntryBytes || sizeBytes > route.Cache.MaxTotalBytes)
        {
            RecordRejection(ProxyCacheEligibilityPolicy.ReasonOversized);
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var entry = new CacheEntry(
            key,
            route.Name,
            responseHead.StatusCode,
            responseHead.ReasonPhrase,
            storedHeaders,
            body.ToArray(),
            now,
            now.Add(ttl),
            now,
            sizeBytes);

        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                RemoveEntry(key, existing);
            }

            _entries[key] = entry;
            _approximateBytes += sizeBytes;
            _stores++;
            EvictRouteOverCapacity(route.Name, route.Cache.MaxTotalBytes);
        }
    }

    public void RecordUncacheable(RuntimeRoute route, string reason)
    {
        if (route.Cache.Enabled)
        {
            RecordRejection(reason);
        }
    }

    public ProxyCacheRuntimeStatusSnapshot ReadStatusSnapshot()
    {
        lock (_gate)
        {
            return new ProxyCacheRuntimeStatusSnapshot(
                _entries.Count,
                _approximateBytes,
                _hits,
                _misses,
                _stores,
                _evictions,
                _storeRejections,
                _lastClearedAtUtc,
                _lastClearReason,
                _rejections
                    .Select(static pair => new ProxyCacheRuntimeRejectionSnapshot(pair.Key, pair.Value))
                    .ToArray(),
                _entries.Values
                    .Select(static entry => new ProxyCacheRuntimeEntrySnapshot(entry.RouteName, entry.SizeBytes))
                    .ToArray());
        }
    }

    public void Clear(string reason)
    {
        lock (_gate)
        {
            _entries.Clear();
            _approximateBytes = 0;
            _lastClearedAtUtc = _timeProvider.GetUtcNow();
            _lastClearReason = reason;
        }
    }

    private bool TryCreateKey(
        RuntimeRoute route,
        RuntimeListener listener,
        Http1RequestHead requestHead,
        string upstreamTarget,
        out string key,
        out string? rejectionReason)
    {
        key = "";
        rejectionReason = null;
        var requestEligibility = ProxyCacheEligibilityPolicy.EvaluateRequest(route, requestHead);
        if (!requestEligibility.CanCache)
        {
            rejectionReason = requestEligibility.RejectionReason;
            return false;
        }

        var scheme = listener.Transport == RuntimeListenerTransport.Https ? "https" : "http";
        var builder = new StringBuilder();
        AppendPart(builder, route.Name);
        AppendPart(builder, route.Host);
        AppendPart(builder, requestHead.Method.ToUpperInvariant());
        AppendPart(builder, scheme);
        AppendPart(builder, requestHead.Host.ToLowerInvariant());
        AppendPart(builder, upstreamTarget);

        foreach (var varyHeader in route.Cache.VaryByHeaders.OrderBy(static header => header, StringComparer.OrdinalIgnoreCase))
        {
            AppendPart(builder, varyHeader.ToLowerInvariant());
            AppendPart(builder, JoinHeaderValues(requestHead.Headers, varyHeader));
        }

        key = builder.ToString();
        return true;
    }

    private static IReadOnlyList<Http1HeaderField> SanitizeStoredHeaders(IReadOnlyList<Http1HeaderField> headers)
    {
        return headers
            .Where(static header => !Http1ManagedHeaderPolicy.IsManagedStoredResponseHeader(header.Name))
            .Select(static header => new Http1HeaderField(header.Name, header.Value))
            .ToArray();
    }

    private void EvictRouteOverCapacity(string routeName, long maxTotalBytes)
    {
        while (RouteBytes(routeName) > maxTotalBytes)
        {
            var oldest = _entries
                .Where(entry => string.Equals(entry.Value.RouteName, routeName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(static entry => entry.Value.LastAccessedAtUtc)
                .FirstOrDefault();
            if (oldest.Value is null)
            {
                return;
            }

            RemoveEntry(oldest.Key, oldest.Value);
            _evictions++;
        }
    }

    private long RouteBytes(string routeName)
    {
        return _entries.Values
            .Where(entry => string.Equals(entry.RouteName, routeName, StringComparison.OrdinalIgnoreCase))
            .Sum(static entry => entry.SizeBytes);
    }

    private void RemoveEntry(string key, CacheEntry entry)
    {
        if (_entries.Remove(key))
        {
            _approximateBytes -= entry.SizeBytes;
            if (_approximateBytes < 0)
            {
                _approximateBytes = 0;
            }
        }
    }

    private void RecordRejection(string reason)
    {
        lock (_gate)
        {
            _storeRejections++;
            _rejections.TryGetValue(reason, out var count);
            _rejections[reason] = count + 1;
        }
    }

    private static long CalculateSize(IReadOnlyList<Http1HeaderField> headers, byte[] body)
    {
        return body.LongLength + headers.Sum(static header => header.Name.Length + header.Value.Length + 4L);
    }

    private static string JoinHeaderValues(IReadOnlyList<Http1HeaderField> headers, string name)
    {
        return string.Join(
            "\u001f",
            headers
                .Where(header => string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase))
                .Select(static header => header.Value));
    }

    private static void AppendPart(StringBuilder builder, string value)
    {
        builder.Append(value.Length).Append(':').Append(value).Append('|');
    }

    private sealed class CacheEntry
    {
        public CacheEntry(
            string key,
            string routeName,
            int statusCode,
            string reasonPhrase,
            IReadOnlyList<Http1HeaderField> headers,
            byte[] body,
            DateTimeOffset storedAtUtc,
            DateTimeOffset expiresAtUtc,
            DateTimeOffset lastAccessedAtUtc,
            long sizeBytes)
        {
            Key = key;
            RouteName = routeName;
            StatusCode = statusCode;
            ReasonPhrase = reasonPhrase;
            Headers = headers;
            Body = body;
            StoredAtUtc = storedAtUtc;
            ExpiresAtUtc = expiresAtUtc;
            LastAccessedAtUtc = lastAccessedAtUtc;
            SizeBytes = sizeBytes;
        }

        public string Key { get; }

        public string RouteName { get; }

        public int StatusCode { get; }

        public string ReasonPhrase { get; }

        public IReadOnlyList<Http1HeaderField> Headers { get; }

        public byte[] Body { get; }

        public DateTimeOffset StoredAtUtc { get; }

        public DateTimeOffset ExpiresAtUtc { get; }

        public DateTimeOffset LastAccessedAtUtc { get; set; }

        public long SizeBytes { get; }

        public CachedProxyResponse ToResponse()
        {
            return new CachedProxyResponse(
                StatusCode,
                ReasonPhrase,
                Headers,
                Body.ToArray(),
                StoredAtUtc,
                ExpiresAtUtc);
        }
    }
}

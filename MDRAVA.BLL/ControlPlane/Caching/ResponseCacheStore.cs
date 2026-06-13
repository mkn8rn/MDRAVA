using MDRAVA.BLL.Http;
using MDRAVA.BLL.ControlPlane.Http1;

namespace MDRAVA.BLL.ControlPlane.Caching;

public sealed partial class ResponseCacheStore : IProxyCacheControl
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

    public ProxyCacheLookupResult Get(
        ProxyCacheRequestScope scope,
        Http1RequestHead requestHead,
        string upstreamTarget)
    {
        if (CreateKey(scope, requestHead, upstreamTarget) is not CacheKeyCreation.Created createdKey)
        {
            return ProxyCacheLookupResult.Miss;
        }

        var key = createdKey.Key;
        var now = _timeProvider.GetUtcNow();
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var entry))
            {
                _misses++;
                return ProxyCacheLookupResult.Miss;
            }

            if (entry.ExpiresAtUtc <= now)
            {
                RemoveEntry(key, entry);
                _misses++;
                return ProxyCacheLookupResult.Miss;
            }

            entry.LastAccessedAtUtc = now;
            _hits++;
            return ProxyCacheLookupResult.Hit(entry.ToResponse());
        }
    }

    public void Store(
        ProxyCacheRequestScope scope,
        Http1RequestHead requestHead,
        string upstreamTarget,
        Http1ResponseHead responseHead,
        IReadOnlyList<ProxyHeaderField> responseHeaders,
        byte[] body)
    {
        var keyCreation = CreateKey(scope, requestHead, upstreamTarget);
        if (keyCreation is not CacheKeyCreation.Created createdKey)
        {
            var rejectionReason = ((CacheKeyCreation.Rejected)keyCreation).Reason;
            if (rejectionReason is not null)
            {
                RecordRejection(rejectionReason);
            }

            return;
        }

        var key = createdKey.Key;
        var responseEligibility = ProxyCacheEligibilityPolicy.EvaluateStoredResponse(
            scope.Policy,
            responseHead,
            body.LongLength);
        if (responseEligibility is not ProxyCacheStorageEligibilityResult.AcceptedResult acceptedStorage)
        {
            RecordRejection(((ProxyCacheStorageEligibilityResult.RejectedResult)responseEligibility).Reason);
            return;
        }

        var ttl = acceptedStorage.Ttl;
        var storedHeaders = SanitizeStoredHeaders(responseHeaders);
        var sizeBytes = CalculateSize(storedHeaders, body);
        if (sizeBytes > scope.Policy.MaxEntryBytes || sizeBytes > scope.Policy.MaxTotalBytes)
        {
            RecordRejection(ProxyCacheEligibilityPolicy.ReasonOversized);
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var entry = new CacheEntry(
            key,
            scope.RouteName,
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
            EvictRouteOverCapacity(scope.RouteName, scope.Policy.MaxTotalBytes);
        }
    }

    public void RecordUncacheable(ProxyCachePolicyFacts policy, string reason)
    {
        if (policy.Enabled)
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

    private void RecordRejection(string reason)
    {
        lock (_gate)
        {
            _storeRejections++;
            _rejections.TryGetValue(reason, out var count);
            _rejections[reason] = count + 1;
        }
    }
}

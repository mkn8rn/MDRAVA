using MDRAVA.BLL.ControlPlane.Http1;
using MDRAVA.BLL.Http;

namespace MDRAVA.BLL.ControlPlane.Caching;

public sealed partial class ResponseCacheStore
{
    private static IReadOnlyList<ProxyHeaderField> SanitizeStoredHeaders(IReadOnlyList<ProxyHeaderField> headers)
    {
        return headers
            .Where(static header => !Http1ManagedHeaderPolicy.IsManagedStoredResponseHeader(header.Name))
            .Select(static header => new ProxyHeaderField(header.Name, header.Value))
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

    private static long CalculateSize(IReadOnlyList<ProxyHeaderField> headers, byte[] body)
    {
        return body.LongLength + headers.Sum(static header => header.Name.Length + header.Value.Length + 4L);
    }

    private sealed class CacheEntry
    {
        public CacheEntry(
            string key,
            string routeName,
            int statusCode,
            string reasonPhrase,
            IReadOnlyList<ProxyHeaderField> headers,
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

        public IReadOnlyList<ProxyHeaderField> Headers { get; }

        public byte[] Body { get; }

        public DateTimeOffset StoredAtUtc { get; }

        public DateTimeOffset ExpiresAtUtc { get; }

        public DateTimeOffset LastAccessedAtUtc { get; private set; }

        public long SizeBytes { get; }

        public void RecordAccess(DateTimeOffset accessedAtUtc)
        {
            LastAccessedAtUtc = accessedAtUtc;
        }

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

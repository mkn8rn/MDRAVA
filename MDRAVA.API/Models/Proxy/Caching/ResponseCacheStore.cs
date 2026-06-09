using System.Text;
using MDRAVA.API.Proxy.Protocol;

namespace MDRAVA.API.Proxy.Caching;

public sealed class ResponseCacheStore : IProxyCacheControl
{
    private const string ReasonAuthorization = "authorization";
    private const string ReasonCacheControlMustRevalidate = "cache-control-must-revalidate";
    private const string ReasonCacheControlNoCache = "cache-control-no-cache";
    private const string ReasonCacheControlNoStore = "cache-control-no-store";
    private const string ReasonCacheControlPrivate = "cache-control-private";
    private const string ReasonCookie = "cookie";
    private const string ReasonFraming = "framing";
    private const string ReasonMethod = "method";
    private const string ReasonOversized = "oversized";
    private const string ReasonRequestBody = "request-body";
    private const string ReasonSetCookie = "set-cookie";
    private const string ReasonStatus = "status";
    private const string ReasonTtl = "ttl";

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

        if (!ContainsStatus(route.Cache.CacheableStatusCodes, responseHead.StatusCode))
        {
            RecordRejection(ReasonStatus);
            return;
        }

        if (ContainsHeader(responseHead.Headers, "Set-Cookie"))
        {
            RecordRejection(ReasonSetCookie);
            return;
        }

        if (!TryResolveTtl(route.Cache, responseHead.Headers, out var ttl, out rejectionReason))
        {
            RecordRejection(rejectionReason ?? ReasonTtl);
            return;
        }

        if (body.Length > route.Cache.MaxEntryBytes)
        {
            RecordRejection(ReasonOversized);
            return;
        }

        var storedHeaders = SanitizeStoredHeaders(responseHeaders);
        var sizeBytes = CalculateSize(storedHeaders, body);
        if (sizeBytes > route.Cache.MaxEntryBytes || sizeBytes > route.Cache.MaxTotalBytes)
        {
            RecordRejection(ReasonOversized);
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

    public ProxyCacheStatusResponse Snapshot(ProxyConfigurationSnapshot? configuration)
    {
        return ProxyCacheStatusReader.Project(
            ProxyCacheStatusRuntimeRouteSourceMapper.ToRouteSources(configuration),
            ReadStatusSnapshot());
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
        if (!route.Cache.Enabled)
        {
            return false;
        }

        if (!ContainsMethod(route.Cache.Methods, requestHead.Method))
        {
            rejectionReason = ReasonMethod;
            return false;
        }

        if (requestHead.Framing.Kind != Http1BodyKind.None)
        {
            rejectionReason = ReasonRequestBody;
            return false;
        }

        if (ContainsHeader(requestHead.Headers, "Authorization"))
        {
            rejectionReason = ReasonAuthorization;
            return false;
        }

        if (ContainsHeader(requestHead.Headers, "Cookie"))
        {
            rejectionReason = ReasonCookie;
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

    private static bool TryResolveTtl(
        RuntimeCachePolicy policy,
        IReadOnlyList<Http1HeaderField> headers,
        out TimeSpan ttl,
        out string? rejectionReason)
    {
        ttl = policy.DefaultTtl;
        rejectionReason = null;
        if (!policy.RespectOriginCacheControl)
        {
            return ttl > TimeSpan.Zero;
        }

        var directives = CacheControlDirectives(headers);
        if (directives.ContainsKey("no-store"))
        {
            rejectionReason = ReasonCacheControlNoStore;
            return false;
        }

        if (directives.ContainsKey("private"))
        {
            rejectionReason = ReasonCacheControlPrivate;
            return false;
        }

        if (directives.ContainsKey("no-cache"))
        {
            rejectionReason = ReasonCacheControlNoCache;
            return false;
        }

        if (directives.ContainsKey("must-revalidate"))
        {
            rejectionReason = ReasonCacheControlMustRevalidate;
            return false;
        }

        if (directives.TryGetValue("max-age", out var maxAgeValue)
            && int.TryParse(maxAgeValue, out var maxAgeSeconds))
        {
            ttl = TimeSpan.FromSeconds(maxAgeSeconds);
        }

        if (ttl <= TimeSpan.Zero)
        {
            rejectionReason = ReasonTtl;
            return false;
        }

        return true;
    }

    private static Dictionary<string, string?> CacheControlDirectives(IReadOnlyList<Http1HeaderField> headers)
    {
        Dictionary<string, string?> directives = new(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers)
        {
            if (!string.Equals(header.Name, "Cache-Control", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var part in header.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var equals = part.IndexOf('=');
                if (equals < 0)
                {
                    directives[part] = null;
                    continue;
                }

                directives[part[..equals].Trim()] = part[(equals + 1)..].Trim().Trim('"');
            }
        }

        return directives;
    }

    private static IReadOnlyList<Http1HeaderField> SanitizeStoredHeaders(IReadOnlyList<Http1HeaderField> headers)
    {
        return headers
            .Where(static header => !IsStoredResponseManagedHeader(header.Name))
            .Select(static header => new Http1HeaderField(header.Name, header.Value))
            .ToArray();
    }

    private static bool IsStoredResponseManagedHeader(string headerName)
    {
        return string.Equals(headerName, "Age", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Connection", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Content-Length", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Keep-Alive", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Proxy-Authenticate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Proxy-Authorization", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "TE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Trailer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Upgrade", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "X-Request-Id", StringComparison.OrdinalIgnoreCase);
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

    private static bool ContainsHeader(IReadOnlyList<Http1HeaderField> headers, string name)
    {
        return headers.Any(header => string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsStatus(IReadOnlyList<int> statusCodes, int statusCode)
    {
        return statusCodes.Any(code => code == statusCode);
    }

    private static bool ContainsMethod(IReadOnlyList<string> methods, string method)
    {
        return methods.Any(value => string.Equals(value, method, StringComparison.OrdinalIgnoreCase));
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

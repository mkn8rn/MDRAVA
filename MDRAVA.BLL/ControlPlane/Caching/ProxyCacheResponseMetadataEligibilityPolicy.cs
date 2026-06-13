using MDRAVA.BLL.ControlPlane.Http1;
using MDRAVA.BLL.Http;

namespace MDRAVA.BLL.ControlPlane.Caching;

public static partial class ProxyCacheEligibilityPolicy
{
    private static CacheResponseMetadataEligibility EvaluateResponseMetadata(
        ProxyCachePolicyFacts policy,
        Http1ResponseHead responseHead)
    {
        if (!ContainsStatus(policy.CacheableStatusCodes, responseHead.StatusCode))
        {
            return CacheResponseMetadataEligibility.Reject(ReasonStatus);
        }

        if (ContainsHeader(responseHead.Headers, "Set-Cookie"))
        {
            return CacheResponseMetadataEligibility.Reject(ReasonSetCookie);
        }

        var ttlResolution = ResolveTtl(policy, responseHead.Headers);
        if (ttlResolution is not CacheTtlResolution.Resolved resolvedTtl)
        {
            return CacheResponseMetadataEligibility.Reject(((CacheTtlResolution.Rejected)ttlResolution).Reason);
        }

        return CacheResponseMetadataEligibility.Accept(resolvedTtl.Ttl);
    }

    private static CacheTtlResolution ResolveTtl(
        ProxyCachePolicyFacts policy,
        IReadOnlyList<ProxyHeaderField> headers)
    {
        var ttl = policy.DefaultTtl;
        if (!policy.RespectOriginCacheControl)
        {
            return ttl > TimeSpan.Zero
                ? CacheTtlResolution.Resolve(ttl)
                : CacheTtlResolution.Reject(ReasonTtl);
        }

        var directives = CacheControlDirectives(headers);
        if (directives.ContainsKey("no-store"))
        {
            return CacheTtlResolution.Reject(ReasonCacheControlNoStore);
        }

        if (directives.ContainsKey("private"))
        {
            return CacheTtlResolution.Reject(ReasonCacheControlPrivate);
        }

        if (directives.ContainsKey("no-cache"))
        {
            return CacheTtlResolution.Reject(ReasonCacheControlNoCache);
        }

        if (directives.ContainsKey("must-revalidate"))
        {
            return CacheTtlResolution.Reject(ReasonCacheControlMustRevalidate);
        }

        if (directives.TryGetValue("max-age", out var maxAgeValue)
            && int.TryParse(maxAgeValue, out var maxAgeSeconds))
        {
            ttl = TimeSpan.FromSeconds(maxAgeSeconds);
        }

        if (ttl <= TimeSpan.Zero)
        {
            return CacheTtlResolution.Reject(ReasonTtl);
        }

        return CacheTtlResolution.Resolve(ttl);
    }

    private static Dictionary<string, string?> CacheControlDirectives(IReadOnlyList<ProxyHeaderField> headers)
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

    private abstract record CacheTtlResolution
    {
        private CacheTtlResolution()
        {
        }

        public static CacheTtlResolution Resolve(TimeSpan ttl)
        {
            return new Resolved(ttl);
        }

        public static CacheTtlResolution Reject(string reason)
        {
            return new Rejected(reason);
        }

        public sealed record Resolved : CacheTtlResolution
        {
            public Resolved(TimeSpan ttl)
            {
                if (ttl <= TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException(nameof(ttl), "Cache TTL must be positive.");
                }

                Ttl = ttl;
            }

            public TimeSpan Ttl { get; }
        }

        public sealed record Rejected : CacheTtlResolution
        {
            public Rejected(string reason)
            {
                if (string.IsNullOrWhiteSpace(reason))
                {
                    throw new ArgumentException("Cache TTL rejection reason is required.", nameof(reason));
                }

                Reason = reason;
            }

            public string Reason { get; }
        }
    }

    private abstract record CacheResponseMetadataEligibility
    {
        private CacheResponseMetadataEligibility()
        {
        }

        public static CacheResponseMetadataEligibility Accept(TimeSpan ttl)
        {
            return new Accepted(ttl);
        }

        public static CacheResponseMetadataEligibility Reject(string reason)
        {
            return new Rejected(reason);
        }

        public sealed record Accepted : CacheResponseMetadataEligibility
        {
            public Accepted(TimeSpan ttl)
            {
                if (ttl <= TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException(nameof(ttl), "Cache metadata TTL must be positive.");
                }

                Ttl = ttl;
            }

            public TimeSpan Ttl { get; }
        }

        public sealed record Rejected : CacheResponseMetadataEligibility
        {
            public Rejected(string reason)
            {
                if (string.IsNullOrWhiteSpace(reason))
                {
                    throw new ArgumentException("Cache metadata rejection reason is required.", nameof(reason));
                }

                Reason = reason;
            }

            public string Reason { get; }
        }
    }
}

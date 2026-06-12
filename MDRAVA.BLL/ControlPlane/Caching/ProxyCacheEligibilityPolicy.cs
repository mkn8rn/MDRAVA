using MDRAVA.BLL.Http;
using MDRAVA.BLL.ControlPlane.Headers;
using MDRAVA.BLL.ControlPlane.Http1;

namespace MDRAVA.BLL.ControlPlane.Caching;

public static class ProxyCacheEligibilityPolicy
{
    public const string ReasonAuthorization = "authorization";
    public const string ReasonCacheControlMustRevalidate = "cache-control-must-revalidate";
    public const string ReasonCacheControlNoCache = "cache-control-no-cache";
    public const string ReasonCacheControlNoStore = "cache-control-no-store";
    public const string ReasonCacheControlPrivate = "cache-control-private";
    public const string ReasonCookie = "cookie";
    public const string ReasonFraming = "framing";
    public const string ReasonMethod = "method";
    public const string ReasonOversized = "oversized";
    public const string ReasonRequestBody = "request-body";
    public const string ReasonSetCookie = "set-cookie";
    public const string ReasonStatus = "status";
    public const string ReasonTtl = "ttl";

    public static ProxyCacheEligibilityResult EvaluateRequest(
        ProxyCachePolicyFacts policy,
        Http1RequestHead requestHead)
    {
        if (!policy.Enabled)
        {
            return ProxyCacheEligibilityResult.Reject(null);
        }

        if (!ContainsMethod(policy.Methods, requestHead.Method))
        {
            return ProxyCacheEligibilityResult.Reject(ReasonMethod);
        }

        if (requestHead.Framing.Kind != Http1BodyKind.None)
        {
            return ProxyCacheEligibilityResult.Reject(ReasonRequestBody);
        }

        if (ContainsHeader(requestHead.Headers, "Authorization"))
        {
            return ProxyCacheEligibilityResult.Reject(ReasonAuthorization);
        }

        if (ContainsHeader(requestHead.Headers, "Cookie"))
        {
            return ProxyCacheEligibilityResult.Reject(ReasonCookie);
        }

        return ProxyCacheEligibilityResult.Accept();
    }

    public static ProxyCacheEligibilityResult EvaluateResponseForBuffering(
        ProxyCachePolicyFacts policy,
        Http1RequestHead requestHead,
        Http1ResponseHead responseHead)
    {
        var requestEligibility = EvaluateRequest(policy, requestHead);
        if (!requestEligibility.CanCache)
        {
            return requestEligibility;
        }

        var responseEligibility = EvaluateResponseMetadata(policy, responseHead, out _);
        if (!responseEligibility.CanCache)
        {
            return responseEligibility;
        }

        if (TryGetResponseFramingRejection(policy, responseHead, out var framingReason))
        {
            return ProxyCacheEligibilityResult.Reject(framingReason);
        }

        return ProxyCacheEligibilityResult.Accept();
    }

    public static ProxyCacheEligibilityResult EvaluateStoredResponse(
        ProxyCachePolicyFacts policy,
        Http1ResponseHead responseHead,
        long bodyLength,
        out TimeSpan ttl)
    {
        var responseEligibility = EvaluateResponseMetadata(policy, responseHead, out ttl);
        if (!responseEligibility.CanCache)
        {
            return responseEligibility;
        }

        if (bodyLength > policy.MaxEntryBytes)
        {
            return ProxyCacheEligibilityResult.Reject(ReasonOversized);
        }

        return ProxyCacheEligibilityResult.Accept();
    }

    public static bool TryGetResponseFramingRejection(
        ProxyCachePolicyFacts policy,
        Http1ResponseHead responseHead,
        out string reason)
    {
        reason = "";
        if (!policy.Enabled)
        {
            return false;
        }

        if (responseHead.Framing.Kind is Http1BodyKind.Chunked or Http1BodyKind.CloseDelimited)
        {
            reason = ReasonFraming;
            return true;
        }

        if (responseHead.Framing.Kind == Http1BodyKind.ContentLength
            && responseHead.Framing.ContentLength.GetValueOrDefault() > policy.MaxEntryBytes)
        {
            reason = ReasonOversized;
            return true;
        }

        return false;
    }

    private static ProxyCacheEligibilityResult EvaluateResponseMetadata(
        ProxyCachePolicyFacts policy,
        Http1ResponseHead responseHead,
        out TimeSpan ttl)
    {
        ttl = policy.DefaultTtl;
        if (!ContainsStatus(policy.CacheableStatusCodes, responseHead.StatusCode))
        {
            return ProxyCacheEligibilityResult.Reject(ReasonStatus);
        }

        if (ContainsHeader(responseHead.Headers, "Set-Cookie"))
        {
            return ProxyCacheEligibilityResult.Reject(ReasonSetCookie);
        }

        var ttlResolution = ResolveTtl(policy, responseHead.Headers);
        if (ttlResolution is not CacheTtlResolution.Resolved resolvedTtl)
        {
            return ProxyCacheEligibilityResult.Reject(((CacheTtlResolution.Rejected)ttlResolution).Reason);
        }

        ttl = resolvedTtl.Ttl;
        return ProxyCacheEligibilityResult.Accept();
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

    private static bool ContainsHeader(IReadOnlyList<ProxyHeaderField> headers, string name)
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
}

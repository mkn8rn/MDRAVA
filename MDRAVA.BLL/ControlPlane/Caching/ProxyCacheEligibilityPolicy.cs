using MDRAVA.BLL.Configuration;

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
        RuntimeRoute route,
        Http1RequestHead requestHead)
    {
        if (!route.Cache.Enabled)
        {
            return ProxyCacheEligibilityResult.Reject(null);
        }

        if (!ContainsMethod(route.Cache.Methods, requestHead.Method))
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
        RuntimeRoute route,
        Http1RequestHead requestHead,
        Http1ResponseHead responseHead)
    {
        var requestEligibility = EvaluateRequest(route, requestHead);
        if (!requestEligibility.CanCache)
        {
            return requestEligibility;
        }

        var responseEligibility = EvaluateResponseMetadata(route.Cache, responseHead, out _);
        if (!responseEligibility.CanCache)
        {
            return responseEligibility;
        }

        if (TryGetResponseFramingRejection(route, responseHead, out var framingReason))
        {
            return ProxyCacheEligibilityResult.Reject(framingReason);
        }

        return ProxyCacheEligibilityResult.Accept();
    }

    public static ProxyCacheEligibilityResult EvaluateStoredResponse(
        RuntimeRoute route,
        Http1ResponseHead responseHead,
        long bodyLength,
        out TimeSpan ttl)
    {
        var responseEligibility = EvaluateResponseMetadata(route.Cache, responseHead, out ttl);
        if (!responseEligibility.CanCache)
        {
            return responseEligibility;
        }

        if (bodyLength > route.Cache.MaxEntryBytes)
        {
            return ProxyCacheEligibilityResult.Reject(ReasonOversized);
        }

        return ProxyCacheEligibilityResult.Accept();
    }

    public static bool TryGetResponseFramingRejection(
        RuntimeRoute route,
        Http1ResponseHead responseHead,
        out string reason)
    {
        reason = "";
        if (!route.Cache.Enabled)
        {
            return false;
        }

        if (responseHead.Framing.Kind is Http1BodyKind.Chunked or Http1BodyKind.CloseDelimited)
        {
            reason = ReasonFraming;
            return true;
        }

        if (responseHead.Framing.Kind == Http1BodyKind.ContentLength
            && responseHead.Framing.ContentLength.GetValueOrDefault() > route.Cache.MaxEntryBytes)
        {
            reason = ReasonOversized;
            return true;
        }

        return false;
    }

    private static ProxyCacheEligibilityResult EvaluateResponseMetadata(
        RuntimeCachePolicy policy,
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

        if (!TryResolveTtl(policy, responseHead.Headers, out ttl, out var rejectionReason))
        {
            return ProxyCacheEligibilityResult.Reject(rejectionReason ?? ReasonTtl);
        }

        return ProxyCacheEligibilityResult.Accept();
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
}

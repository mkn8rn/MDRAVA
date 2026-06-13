using MDRAVA.BLL.Http;
using MDRAVA.BLL.ControlPlane.Headers;
using MDRAVA.BLL.ControlPlane.Http1;

namespace MDRAVA.BLL.ControlPlane.Caching;

public static partial class ProxyCacheEligibilityPolicy
{
    public const string ReasonAuthorization = "authorization";
    public const string ReasonCacheControlMustRevalidate = "cache-control-must-revalidate";
    public const string ReasonCacheControlNoCache = "cache-control-no-cache";
    public const string ReasonCacheControlNoStore = "cache-control-no-store";
    public const string ReasonCacheControlPrivate = "cache-control-private";
    public const string ReasonCookie = "cookie";
    public const string ReasonDisabled = "disabled";
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
            return ProxyCacheEligibilityResult.Rejected(ReasonDisabled);
        }

        if (!ContainsMethod(policy.Methods, requestHead.Method))
        {
            return ProxyCacheEligibilityResult.Rejected(ReasonMethod);
        }

        if (requestHead.Framing.Kind != Http1BodyKind.None)
        {
            return ProxyCacheEligibilityResult.Rejected(ReasonRequestBody);
        }

        if (ContainsHeader(requestHead.Headers, "Authorization"))
        {
            return ProxyCacheEligibilityResult.Rejected(ReasonAuthorization);
        }

        if (ContainsHeader(requestHead.Headers, "Cookie"))
        {
            return ProxyCacheEligibilityResult.Rejected(ReasonCookie);
        }

        return ProxyCacheEligibilityResult.Accepted();
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

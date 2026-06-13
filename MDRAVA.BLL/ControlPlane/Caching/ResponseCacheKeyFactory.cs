using System.Text;
using MDRAVA.BLL.ControlPlane.Http1;
using MDRAVA.BLL.Http;

namespace MDRAVA.BLL.ControlPlane.Caching;

public sealed partial class ResponseCacheStore
{
    private CacheKeyCreation CreateKey(
        ProxyCacheRequestScope scope,
        Http1RequestHead requestHead,
        string upstreamTarget)
    {
        var requestEligibility = ProxyCacheEligibilityPolicy.EvaluateRequest(scope.Policy, requestHead);
        if (requestEligibility is ProxyCacheEligibilityResult.RejectedResult rejected)
        {
            return CacheKeyCreation.Reject(rejected.Reason);
        }

        var builder = new StringBuilder();
        AppendPart(builder, scope.RouteName);
        AppendPart(builder, scope.RouteHost);
        AppendPart(builder, requestHead.Method.ToUpperInvariant());
        AppendPart(builder, scope.Scheme);
        AppendPart(builder, requestHead.Host.ToLowerInvariant());
        AppendPart(builder, upstreamTarget);

        foreach (var varyHeader in scope.Policy.VaryByHeaders.OrderBy(static header => header, StringComparer.OrdinalIgnoreCase))
        {
            AppendPart(builder, varyHeader.ToLowerInvariant());
            AppendPart(builder, JoinHeaderValues(requestHead.Headers, varyHeader));
        }

        return CacheKeyCreation.Create(builder.ToString());
    }

    private static string JoinHeaderValues(IReadOnlyList<ProxyHeaderField> headers, string name)
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

    private abstract record CacheKeyCreation
    {
        private CacheKeyCreation()
        {
        }

        public static CacheKeyCreation Create(string key)
        {
            return new Created(key);
        }

        public static CacheKeyCreation Reject(string reason)
        {
            return new Rejected(reason);
        }

        public sealed record Created : CacheKeyCreation
        {
            public Created(string key)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new ArgumentException("Cache key is required.", nameof(key));
                }

                Key = key;
            }

            public string Key { get; }
        }

        public sealed record Rejected : CacheKeyCreation
        {
            public Rejected(string reason)
            {
                if (string.IsNullOrWhiteSpace(reason))
                {
                    throw new ArgumentException("Cache key rejection reason is required.", nameof(reason));
                }

                Reason = reason;
            }

            public string Reason { get; }
        }
    }
}

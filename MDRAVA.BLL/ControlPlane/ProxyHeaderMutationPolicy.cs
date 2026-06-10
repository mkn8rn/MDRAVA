using MDRAVA.BLL.ControlPlane.Http1;
using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane;

public static class ProxyHeaderMutationPolicy
{
    public static IReadOnlyList<Http1HeaderField> ApplyRequestHeaders(
        IReadOnlyList<Http1HeaderField> headers,
        RuntimeHeaderPolicy policy,
        ForwardedHeadersContext forwardedHeaders)
    {
        var result = headers
            .Where(header => !ForwardedHeadersPolicy.IsForwardedHeader(header.Name))
            .Where(header => !ContainsHeaderName(policy.RemoveRequestHeaders, header.Name))
            .Where(header => !ContainsHeaderName(policy.SetRequestHeaders.Select(static set => set.Name), header.Name))
            .ToList();

        result.AddRange(policy.SetRequestHeaders);
        foreach (var forwardedHeader in forwardedHeaders.Headers)
        {
            result.RemoveAll(header => string.Equals(header.Name, forwardedHeader.Name, StringComparison.OrdinalIgnoreCase));
            result.Add(forwardedHeader);
        }

        return result;
    }

    public static IReadOnlyList<Http1HeaderField> ApplyResponseHeaders(
        IReadOnlyList<Http1HeaderField> headers,
        RuntimeHeaderPolicy policy)
    {
        var result = headers
            .Where(header => !ContainsHeaderName(policy.RemoveResponseHeaders, header.Name))
            .Where(header => !ContainsHeaderName(policy.SetResponseHeaders.Select(static set => set.Name), header.Name))
            .ToList();

        result.AddRange(policy.SetResponseHeaders);
        return result;
    }

    private static bool ContainsHeaderName(IEnumerable<string> headerNames, string headerName)
    {
        return headerNames.Any(name => string.Equals(name, headerName, StringComparison.OrdinalIgnoreCase));
    }
}

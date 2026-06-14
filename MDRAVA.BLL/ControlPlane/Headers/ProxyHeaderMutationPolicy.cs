using MDRAVA.BLL.Http;
using System.Collections.ObjectModel;

namespace MDRAVA.BLL.ControlPlane.Headers;

public static class ProxyHeaderMutationPolicy
{
    public static IReadOnlyList<ProxyHeaderField> ApplyRequestHeaders(
        IReadOnlyList<ProxyHeaderField> headers,
        ProxyHeaderMutationPolicyInput policy,
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

    public static IReadOnlyList<ProxyHeaderField> ApplyResponseHeaders(
        IReadOnlyList<ProxyHeaderField> headers,
        ProxyHeaderMutationPolicyInput policy)
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

public sealed record ProxyHeaderMutationPolicyInput
{
    public ProxyHeaderMutationPolicyInput(
        IReadOnlyList<ProxyHeaderField> SetRequestHeaders,
        IReadOnlyList<string> RemoveRequestHeaders,
        IReadOnlyList<ProxyHeaderField> SetResponseHeaders,
        IReadOnlyList<string> RemoveResponseHeaders)
    {
        ArgumentNullException.ThrowIfNull(SetRequestHeaders);
        ArgumentNullException.ThrowIfNull(RemoveRequestHeaders);
        ArgumentNullException.ThrowIfNull(SetResponseHeaders);
        ArgumentNullException.ThrowIfNull(RemoveResponseHeaders);

        this.SetRequestHeaders = new ReadOnlyCollection<ProxyHeaderField>(SetRequestHeaders.ToArray());
        this.RemoveRequestHeaders = new ReadOnlyCollection<string>(RemoveRequestHeaders.ToArray());
        this.SetResponseHeaders = new ReadOnlyCollection<ProxyHeaderField>(SetResponseHeaders.ToArray());
        this.RemoveResponseHeaders = new ReadOnlyCollection<string>(RemoveResponseHeaders.ToArray());
    }

    public IReadOnlyList<ProxyHeaderField> SetRequestHeaders { get; }

    public IReadOnlyList<string> RemoveRequestHeaders { get; }

    public IReadOnlyList<ProxyHeaderField> SetResponseHeaders { get; }

    public IReadOnlyList<string> RemoveResponseHeaders { get; }
}

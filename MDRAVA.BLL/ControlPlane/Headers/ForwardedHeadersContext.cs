using MDRAVA.BLL.Http;

namespace MDRAVA.BLL.ControlPlane.Headers;

public sealed record ForwardedHeadersContext
{
    public ForwardedHeadersContext(
        string? ResolvedClientAddress,
        string? ResolvedClientEndpoint,
        IReadOnlyList<ProxyHeaderField> Headers)
    {
        this.ResolvedClientAddress = ResolvedClientAddress;
        this.ResolvedClientEndpoint = ResolvedClientEndpoint;
        this.Headers = ProxyHeaderFieldList.Copy(Headers);
    }

    public string? ResolvedClientAddress { get; init; }

    public string? ResolvedClientEndpoint { get; init; }

    public IReadOnlyList<ProxyHeaderField> Headers { get; }
}

using MDRAVA.BLL.Http;

namespace MDRAVA.BLL.ControlPlane.Headers;

public sealed record ForwardedHeadersContext(
    string? ResolvedClientAddress,
    string? ResolvedClientEndpoint,
    IReadOnlyList<ProxyHeaderField> Headers);

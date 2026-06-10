using MDRAVA.BLL.Http;
using System.Net;

namespace MDRAVA.BLL.ControlPlane.Headers;

public sealed record ForwardedHeadersContext(
    IPAddress? ResolvedClientIp,
    string? ResolvedClientEndpoint,
    IReadOnlyList<ProxyHeaderField> Headers);

using System.Net;

namespace MDRAVA.BLL.ControlPlane;

public sealed record ForwardedHeadersContext(
    IPAddress? ResolvedClientIp,
    string? ResolvedClientEndpoint,
    IReadOnlyList<Http1HeaderField> Headers);

using MDRAVA.BLL.ControlPlane.Http1;
using System.Net;

namespace MDRAVA.BLL.ControlPlane.Headers;

public sealed record ForwardedHeadersContext(
    IPAddress? ResolvedClientIp,
    string? ResolvedClientEndpoint,
    IReadOnlyList<Http1HeaderField> Headers);

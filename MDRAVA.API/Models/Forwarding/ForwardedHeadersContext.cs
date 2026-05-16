using System.Net;

namespace MDRAVA.API.Models.Forwarding;

public sealed record ForwardedHeadersContext(
    IPAddress? ResolvedClientIp,
    string? ResolvedClientEndpoint,
    IReadOnlyList<Http1HeaderField> Headers);

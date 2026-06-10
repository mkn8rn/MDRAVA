using MDRAVA.BLL.Http;
using MDRAVA.BLL.ControlPlane.Headers;

namespace MDRAVA.BLL.ControlPlane.Routing;

public sealed record GeneratedRouteResponse(
    int StatusCode,
    string ReasonPhrase,
    string? ContentType,
    string Body,
    IReadOnlyList<ProxyHeaderField> Headers);

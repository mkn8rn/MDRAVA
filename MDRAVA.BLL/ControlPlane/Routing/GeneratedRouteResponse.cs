using MDRAVA.BLL.Http;
using MDRAVA.BLL.ControlPlane.Headers;

namespace MDRAVA.BLL.ControlPlane.Routing;

public sealed record GeneratedRouteResponse
{
    public GeneratedRouteResponse(
        int StatusCode,
        string ReasonPhrase,
        string? ContentType,
        string Body,
        IReadOnlyList<ProxyHeaderField> Headers)
    {
        this.StatusCode = StatusCode;
        this.ReasonPhrase = ReasonPhrase;
        this.ContentType = ContentType;
        this.Body = Body;
        this.Headers = ProxyHeaderFieldList.Copy(Headers);
    }

    public int StatusCode { get; init; }

    public string ReasonPhrase { get; init; }

    public string? ContentType { get; init; }

    public string Body { get; init; }

    public IReadOnlyList<ProxyHeaderField> Headers { get; }
}

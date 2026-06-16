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
        ArgumentNullException.ThrowIfNull(ReasonPhrase);
        ArgumentNullException.ThrowIfNull(Body);

        this.StatusCode = StatusCode;
        this.ReasonPhrase = ReasonPhrase;
        this.ContentType = ContentType;
        this.Body = Body;
        this.Headers = ProxyHeaderFieldList.Copy(Headers);
    }

    public int StatusCode { get; }

    public string ReasonPhrase { get; }

    public string? ContentType { get; }

    public string Body { get; }

    public IReadOnlyList<ProxyHeaderField> Headers { get; }
}

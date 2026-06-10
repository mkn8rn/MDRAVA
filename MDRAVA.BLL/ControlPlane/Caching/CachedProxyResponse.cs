using MDRAVA.BLL.Http;
using MDRAVA.BLL.ControlPlane.Headers;
namespace MDRAVA.BLL.ControlPlane.Caching;

public sealed class CachedProxyResponse
{
    public CachedProxyResponse(
        int statusCode,
        string reasonPhrase,
        IReadOnlyList<ProxyHeaderField> headers,
        byte[] body,
        DateTimeOffset storedAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        StatusCode = statusCode;
        ReasonPhrase = reasonPhrase;
        Headers = headers;
        Body = body;
        StoredAtUtc = storedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public int StatusCode { get; }

    public string ReasonPhrase { get; }

    public IReadOnlyList<ProxyHeaderField> Headers { get; }

    public byte[] Body { get; }

    public DateTimeOffset StoredAtUtc { get; }

    public DateTimeOffset ExpiresAtUtc { get; }
}

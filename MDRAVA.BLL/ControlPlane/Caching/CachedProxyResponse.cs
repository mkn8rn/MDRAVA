namespace MDRAVA.BLL.ControlPlane.Caching;

public sealed class CachedProxyResponse
{
    public CachedProxyResponse(
        int statusCode,
        string reasonPhrase,
        IReadOnlyList<Http1HeaderField> headers,
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

    public IReadOnlyList<Http1HeaderField> Headers { get; }

    public byte[] Body { get; }

    public DateTimeOffset StoredAtUtc { get; }

    public DateTimeOffset ExpiresAtUtc { get; }
}

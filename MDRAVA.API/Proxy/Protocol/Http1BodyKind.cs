namespace MDRAVA.API.Proxy.Protocol;

public enum Http1BodyKind
{
    None,
    ContentLength,
    Chunked,
    CloseDelimited
}

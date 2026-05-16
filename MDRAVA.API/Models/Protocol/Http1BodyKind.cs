namespace MDRAVA.API.Models.Protocol;

public enum Http1BodyKind
{
    None,
    ContentLength,
    Chunked,
    CloseDelimited
}

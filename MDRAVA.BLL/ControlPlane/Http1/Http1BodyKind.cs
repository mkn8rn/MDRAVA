namespace MDRAVA.BLL.ControlPlane.Http1;

public enum Http1BodyKind
{
    None,
    ContentLength,
    Chunked,
    CloseDelimited
}

namespace MDRAVA.BLL.ControlPlane;

public enum Http1BodyKind
{
    None,
    ContentLength,
    Chunked,
    CloseDelimited
}

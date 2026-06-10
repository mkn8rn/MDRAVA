namespace MDRAVA.BLL.ControlPlane.Http1;

public sealed record Http1RequestParseLimits(
    int MaxHeaderCount,
    int MaxHeaderLineBytes,
    int MaxPathBytes);

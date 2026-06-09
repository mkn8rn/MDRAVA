namespace MDRAVA.BLL.ControlPlane;

public sealed record Http1RequestParseLimits(
    int MaxHeaderCount,
    int MaxHeaderLineBytes,
    int MaxPathBytes);

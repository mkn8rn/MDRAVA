namespace MDRAVA.API.Models.Protocol;

public sealed record Http1RequestParseLimits(
    int MaxHeaderCount,
    int MaxHeaderLineBytes,
    int MaxPathBytes);

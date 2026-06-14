namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeHttp2LimitsProjection(
    int MaxConcurrentStreams,
    int MaxHeaderListBytes,
    int MaxFrameSize);

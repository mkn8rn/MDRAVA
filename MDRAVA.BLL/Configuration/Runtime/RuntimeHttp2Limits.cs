namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeHttp2Limits(
    int MaxConcurrentStreams,
    int MaxHeaderListBytes,
    int MaxFrameSize)
{
    public static RuntimeHttp2Limits Default { get; } = new(100, 32 * 1024, 16 * 1024);
}

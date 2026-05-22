namespace MDRAVA.BLL.Configuration;

public sealed class ProxyCachePolicyOptions
{
    public bool Enabled { get; init; }

    public long MaxEntryBytes { get; init; } = 1024 * 1024;

    public long MaxTotalBytes { get; init; } = 16 * 1024 * 1024;

    public int DefaultTtlSeconds { get; init; } = 60;

    public bool RespectOriginCacheControl { get; init; } = true;

    public List<string> VaryByHeaders { get; init; } = [];

    public List<int> CacheableStatusCodes { get; init; } = [200];

    public List<string> Methods { get; init; } = ["GET", "HEAD"];
}

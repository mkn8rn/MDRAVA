namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeCachePolicy(
    bool Enabled,
    long MaxEntryBytes,
    long MaxTotalBytes,
    TimeSpan DefaultTtl,
    bool RespectOriginCacheControl,
    IReadOnlyList<string> VaryByHeaders,
    IReadOnlyList<int> CacheableStatusCodes,
    IReadOnlyList<string> Methods)
{
    public static RuntimeCachePolicy Disabled { get; } = new(
        false,
        0,
        0,
        TimeSpan.Zero,
        true,
        [],
        [200],
        ["GET", "HEAD"]);
}

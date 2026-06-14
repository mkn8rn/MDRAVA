namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeCacheProjection
{
    public RuntimeCacheProjection(
        bool Enabled,
        long MaxEntryBytes,
        long MaxTotalBytes,
        TimeSpan DefaultTtl,
        bool RespectOriginCacheControl,
        IReadOnlyList<string> VaryByHeaders,
        IReadOnlyList<int> CacheableStatusCodes,
        IReadOnlyList<string> Methods)
    {
        this.Enabled = Enabled;
        this.MaxEntryBytes = MaxEntryBytes;
        this.MaxTotalBytes = MaxTotalBytes;
        this.DefaultTtl = DefaultTtl;
        this.RespectOriginCacheControl = RespectOriginCacheControl;
        this.VaryByHeaders = RuntimeList.Copy(VaryByHeaders);
        this.CacheableStatusCodes = RuntimeList.Copy(CacheableStatusCodes);
        this.Methods = RuntimeList.Copy(Methods);
    }

    public bool Enabled { get; init; }

    public long MaxEntryBytes { get; init; }

    public long MaxTotalBytes { get; init; }

    public TimeSpan DefaultTtl { get; init; }

    public bool RespectOriginCacheControl { get; init; }

    public IReadOnlyList<string> VaryByHeaders { get; }

    public IReadOnlyList<int> CacheableStatusCodes { get; }

    public IReadOnlyList<string> Methods { get; }
}

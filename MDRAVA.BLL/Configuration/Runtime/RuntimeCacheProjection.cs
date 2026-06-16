namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeCacheProjection
{
    public RuntimeCacheProjection(
        bool Enabled,
        long MaxEntryBytes,
        long MaxTotalBytes,
        TimeSpan DefaultTtl,
        bool RespectOriginCacheControl,
        IEnumerable<string> VaryByHeaders,
        IEnumerable<int> CacheableStatusCodes,
        IEnumerable<string> Methods)
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

    public bool Enabled { get; }

    public long MaxEntryBytes { get; }

    public long MaxTotalBytes { get; }

    public TimeSpan DefaultTtl { get; }

    public bool RespectOriginCacheControl { get; }

    public IReadOnlyList<string> VaryByHeaders { get; }

    public IReadOnlyList<int> CacheableStatusCodes { get; }

    public IReadOnlyList<string> Methods { get; }
}

namespace MDRAVA.BLL.ControlPlane.Caching;

public abstract record ProxyCacheLookupResult
{
    private ProxyCacheLookupResult()
    {
    }

    public static ProxyCacheLookupResult Miss { get; } = new MissResult();

    public static ProxyCacheLookupResult Hit(CachedProxyResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new HitResult(response);
    }

    public sealed record HitResult : ProxyCacheLookupResult
    {
        public HitResult(CachedProxyResponse response)
        {
            ArgumentNullException.ThrowIfNull(response);
            Response = response;
        }

        public CachedProxyResponse Response { get; }
    }

    public sealed record MissResult : ProxyCacheLookupResult;
}

namespace MDRAVA.BLL.ControlPlane.Caching;

public sealed record ProxyCacheRejectionStatus
{
    private ProxyCacheRejectionStatus(
        string reason,
        long count)
    {
        Reason = reason;
        Count = count;
    }

    public string Reason { get; }

    public long Count { get; }

    public static ProxyCacheRejectionStatus FromSources(
        string reason,
        long count)
    {
        return new ProxyCacheRejectionStatus(
            reason,
            count);
    }
}

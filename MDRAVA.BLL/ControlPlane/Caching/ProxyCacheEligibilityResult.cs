namespace MDRAVA.BLL.ControlPlane.Caching;

public readonly record struct ProxyCacheEligibilityResult
{
    private ProxyCacheEligibilityResult(bool canCache, string? rejectionReason)
    {
        CanCache = canCache;
        RejectionReason = rejectionReason;
    }

    public bool CanCache { get; }

    public string? RejectionReason { get; }

    public static ProxyCacheEligibilityResult Accept()
    {
        return new ProxyCacheEligibilityResult(
            canCache: true,
            rejectionReason: null);
    }

    public static ProxyCacheEligibilityResult Reject(string? reason)
    {
        return new ProxyCacheEligibilityResult(
            canCache: false,
            rejectionReason: reason);
    }
}

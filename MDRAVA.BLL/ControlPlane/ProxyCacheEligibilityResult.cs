namespace MDRAVA.BLL.ControlPlane;

public readonly record struct ProxyCacheEligibilityResult(bool CanCache, string? RejectionReason)
{
    public static ProxyCacheEligibilityResult Accept()
    {
        return new ProxyCacheEligibilityResult(true, null);
    }

    public static ProxyCacheEligibilityResult Reject(string? reason)
    {
        return new ProxyCacheEligibilityResult(false, reason);
    }
}

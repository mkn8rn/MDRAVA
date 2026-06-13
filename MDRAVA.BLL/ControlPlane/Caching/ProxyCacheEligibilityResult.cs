namespace MDRAVA.BLL.ControlPlane.Caching;

public abstract record ProxyCacheEligibilityResult
{
    private ProxyCacheEligibilityResult()
    {
    }

    public static ProxyCacheEligibilityResult Accepted()
    {
        return AcceptedResult.Instance;
    }

    public static ProxyCacheEligibilityResult Rejected(string reason)
    {
        return new RejectedResult(reason);
    }

    public sealed record AcceptedResult : ProxyCacheEligibilityResult
    {
        public static AcceptedResult Instance { get; } = new();

        private AcceptedResult()
        {
        }
    }

    public sealed record RejectedResult : ProxyCacheEligibilityResult
    {
        public RejectedResult(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Cache eligibility rejection reason is required.", nameof(reason));
            }

            Reason = reason;
        }

        public string Reason { get; }
    }
}

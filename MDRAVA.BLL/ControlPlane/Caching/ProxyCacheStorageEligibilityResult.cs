namespace MDRAVA.BLL.ControlPlane.Caching;

public abstract record ProxyCacheStorageEligibilityResult
{
    private ProxyCacheStorageEligibilityResult()
    {
    }

    public static ProxyCacheStorageEligibilityResult Accepted(TimeSpan ttl)
    {
        return new AcceptedResult(ttl);
    }

    public static ProxyCacheStorageEligibilityResult Rejected(string reason)
    {
        return new RejectedResult(reason);
    }

    public sealed record AcceptedResult : ProxyCacheStorageEligibilityResult
    {
        public AcceptedResult(TimeSpan ttl)
        {
            if (ttl <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(ttl), "Cache storage TTL must be positive.");
            }

            Ttl = ttl;
        }

        public TimeSpan Ttl { get; }
    }

    public sealed record RejectedResult : ProxyCacheStorageEligibilityResult
    {
        public RejectedResult(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Cache storage rejection reason is required.", nameof(reason));
            }

            Reason = reason;
        }

        public string Reason { get; }
    }
}

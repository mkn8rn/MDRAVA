namespace MDRAVA.BLL.ControlPlane.Caching;

public abstract record ProxyCacheResponseFramingEligibility
{
    private ProxyCacheResponseFramingEligibility()
    {
    }

    public static ProxyCacheResponseFramingEligibility Accept()
    {
        return Accepted.Instance;
    }

    public static ProxyCacheResponseFramingEligibility Reject(string reason)
    {
        return new Rejected(reason);
    }

    public sealed record Accepted : ProxyCacheResponseFramingEligibility
    {
        internal static Accepted Instance { get; } = new();

        private Accepted()
        {
        }
    }

    public sealed record Rejected : ProxyCacheResponseFramingEligibility
    {
        public Rejected(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Cache response framing rejection reason is required.", nameof(reason));
            }

            Reason = reason;
        }

        public string Reason { get; }
    }
}

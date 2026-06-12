namespace MDRAVA.BLL.ControlPlane.Headers;

public interface IForwardedHeadersAddressPolicy
{
    bool IsTrustedPeer(string peerAddress, IReadOnlyList<string> trustedProxyEntries);

    ForwardedForNormalizationResult NormalizeForwardedFor(IReadOnlyList<string> forwardedFor);
}

public abstract record ForwardedForNormalizationResult
{
    private ForwardedForNormalizationResult()
    {
    }

    public static ForwardedForNormalizationResult Missing { get; } = new MissingResult();

    public static ForwardedForNormalizationResult Normalized(string clientAddress)
    {
        return new NormalizedResult(clientAddress);
    }

    public sealed record NormalizedResult : ForwardedForNormalizationResult
    {
        public NormalizedResult(string clientAddress)
        {
            if (string.IsNullOrWhiteSpace(clientAddress))
            {
                throw new ArgumentException("Forwarded client address is required.", nameof(clientAddress));
            }

            ClientAddress = clientAddress;
        }

        public string ClientAddress { get; }
    }

    private sealed record MissingResult : ForwardedForNormalizationResult;
}

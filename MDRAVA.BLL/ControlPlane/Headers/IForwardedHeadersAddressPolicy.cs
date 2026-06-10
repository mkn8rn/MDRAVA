namespace MDRAVA.BLL.ControlPlane.Headers;

public interface IForwardedHeadersAddressPolicy
{
    bool IsTrustedPeer(string peerAddress, IReadOnlyList<string> trustedProxyEntries);

    bool TryNormalizeForwardedFor(
        IReadOnlyList<string> forwardedFor,
        out string? clientAddress);
}

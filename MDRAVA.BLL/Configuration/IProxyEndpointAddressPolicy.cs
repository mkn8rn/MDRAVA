namespace MDRAVA.BLL.Configuration;

public interface IProxyEndpointAddressPolicy
{
    bool IsListenerAddress(string value);

    bool IsAmbiguousUpstreamAddress(string value);

    bool IsValidSniHost(string value);
}

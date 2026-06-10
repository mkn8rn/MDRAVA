using System.Net;
using MDRAVA.INF.Proxy.RuntimeGuards;

namespace MDRAVA.Tests;

internal static class ProxyClientAddressPolicyTests
{
    public static void NormalizesClientIpAddresses()
    {
        AssertEx.Equal(null, ProxyClientAddressPolicy.NormalizeClientIp(null));
        AssertEx.Equal("127.0.0.1", ProxyClientAddressPolicy.NormalizeRequiredClientIp(IPAddress.Parse("127.0.0.1")));
        AssertEx.Equal("127.0.0.1", ProxyClientAddressPolicy.NormalizeRequiredClientIp(IPAddress.Parse("::ffff:127.0.0.1")));
        AssertEx.Equal("2001:db8::1", ProxyClientAddressPolicy.NormalizeRequiredClientIp(IPAddress.Parse("2001:db8::1")));
    }
}

namespace MDRAVA.Tests;

internal static class UpgradeRequestPolicyTests
{
    public static void ClassifiesManagedUpgradeHeaders()
    {
        AssertEx.True(UpgradeRequestPolicy.IsManagedUpgradeHeader("Connection"));
        AssertEx.True(UpgradeRequestPolicy.IsManagedUpgradeHeader("upgrade"));
        AssertEx.False(UpgradeRequestPolicy.IsManagedUpgradeHeader("Sec-WebSocket-Accept"));
    }

    public static void ClassifiesUnsafeSwitchingProtocolsResponseHeaders()
    {
        AssertEx.True(UpgradeRequestPolicy.IsUnsafeSwitchingProtocolsResponseHeader("Keep-Alive"));
        AssertEx.True(UpgradeRequestPolicy.IsUnsafeSwitchingProtocolsResponseHeader("proxy-authenticate"));
        AssertEx.True(UpgradeRequestPolicy.IsUnsafeSwitchingProtocolsResponseHeader("Proxy-Authorization"));
        AssertEx.True(UpgradeRequestPolicy.IsUnsafeSwitchingProtocolsResponseHeader("TE"));
        AssertEx.True(UpgradeRequestPolicy.IsUnsafeSwitchingProtocolsResponseHeader("Trailer"));
        AssertEx.True(UpgradeRequestPolicy.IsUnsafeSwitchingProtocolsResponseHeader("transfer-encoding"));
        AssertEx.False(UpgradeRequestPolicy.IsUnsafeSwitchingProtocolsResponseHeader("Sec-WebSocket-Accept"));
    }
}

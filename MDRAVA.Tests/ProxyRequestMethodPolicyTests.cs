namespace MDRAVA.Tests;

internal static class ProxyRequestMethodPolicyTests
{
    public static void ValidatesHttpMethodTokens()
    {
        AssertEx.True(ProxyRequestMethodPolicy.IsValidMethodToken("GET"));
        AssertEx.True(ProxyRequestMethodPolicy.IsValidMethodToken("custom-token"));
        AssertEx.False(ProxyRequestMethodPolicy.IsValidMethodToken("GE T"));
        AssertEx.False(ProxyRequestMethodPolicy.IsValidMethodToken(""));
    }

    public static void ClassifiesSupportedApplicationMethods()
    {
        var getSupported = ProxyRequestMethodPolicy.IsSupportedApplicationMethod("GET", out var getReason);
        var lowerPostSupported = ProxyRequestMethodPolicy.IsSupportedApplicationMethod("post", out var lowerPostReason);

        AssertEx.True(getSupported);
        AssertEx.Equal("", getReason);
        AssertEx.True(lowerPostSupported);
        AssertEx.Equal("", lowerPostReason);
    }

    public static void ClassifiesSafeReadMethods()
    {
        AssertEx.True(ProxyRequestMethodPolicy.IsSafeReadMethod("GET"));
        AssertEx.True(ProxyRequestMethodPolicy.IsSafeReadMethod("head"));
        AssertEx.False(ProxyRequestMethodPolicy.IsSafeReadMethod("POST"));
        AssertEx.False(ProxyRequestMethodPolicy.IsSafeReadMethod("DELETE"));
    }

    public static void ClassifiesUnsupportedMethods()
    {
        var traceSupported = ProxyRequestMethodPolicy.IsSupportedApplicationMethod("TRACE", out var traceReason);
        var connectSupported = ProxyRequestMethodPolicy.IsSupportedApplicationMethod("connect", out var connectReason);

        AssertEx.False(traceSupported);
        AssertEx.Equal(ProxyRequestMethodPolicy.MethodUnsupportedReason, traceReason);
        AssertEx.False(connectSupported);
        AssertEx.Equal(ProxyRequestMethodPolicy.ConnectUnsupportedReason, connectReason);
        AssertEx.True(ProxyRequestMethodPolicy.IsConnectTunnelMethod("CONNECT"));
        AssertEx.True(ProxyRequestMethodPolicy.IsConnectTunnelMethod("connect"));
    }
}

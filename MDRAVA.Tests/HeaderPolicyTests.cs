using MDRAVA.API.Proxy.Forwarding;
using MDRAVA.API.Proxy.Protocol;

namespace MDRAVA.Tests;

internal static class HeaderPolicyTests
{
    public static void FiltersStandardHopByHopHeaders()
    {
        var policy = new HopByHopHeaderPolicy();
        var filtered = policy.FilterForForwarding(
            [
                new Http1HeaderField("Host", "example.test"),
                new Http1HeaderField("Connection", "close"),
                new Http1HeaderField("Keep-Alive", "timeout=5"),
                new Http1HeaderField("Upgrade", "websocket")
            ],
            preserveTransferEncoding: false,
            preserveTrailer: false);

        AssertEx.Equal(1, filtered.Count);
        AssertEx.Equal("Host", filtered[0].Name);
    }

    public static void FiltersConnectionNominatedHeaders()
    {
        var policy = new HopByHopHeaderPolicy();
        var filtered = policy.FilterForForwarding(
            [
                new Http1HeaderField("Host", "example.test"),
                new Http1HeaderField("Connection", "x-private"),
                new Http1HeaderField("X-Private", "do-not-forward")
            ],
            preserveTransferEncoding: false,
            preserveTrailer: false);

        AssertEx.Equal(1, filtered.Count);
        AssertEx.Equal("Host", filtered[0].Name);
    }
}

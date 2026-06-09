using MDRAVA.API.Proxy.Forwarding;

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

    public static void ClassifiesHopByHopNamesAndConnectionTokens()
    {
        var policy = new HopByHopHeaderPolicy();
        var filtered = policy.FilterForForwarding(
            [
                new Http1HeaderField("Host", "example.test"),
                new Http1HeaderField("Proxy-Connection", "keep-alive"),
                new Http1HeaderField("TE", "trailers"),
                new Http1HeaderField("Trailer", "expires")
            ],
            preserveTransferEncoding: false,
            preserveTrailer: false);

        AssertEx.Equal(1, filtered.Count);
        AssertEx.Equal("Host", filtered[0].Name);
        AssertEx.True(HopByHopHeaderPolicy.IsHopByHopHeader("proxy-connection"));
        AssertEx.True(HopByHopHeaderPolicy.IsHopByHopHeader("TE"));
        AssertEx.True(HopByHopHeaderPolicy.HasConnectionToken(
            [new Http1HeaderField("Connection", "keep-alive, Upgrade")],
            "upgrade"));
        AssertEx.False(HopByHopHeaderPolicy.HasConnectionToken(
            [new Http1HeaderField("Connection", "keep-alive")],
            "upgrade"));
    }

    public static void AppliesRequestHeaderMutationPolicy()
    {
        var policy = new RuntimeHeaderPolicy(
            [new Http1HeaderField("X-Set", "new")],
            ["X-Remove"],
            [],
            []);
        var forwardedHeaders = new ForwardedHeadersContext(
            System.Net.IPAddress.Parse("203.0.113.10"),
            "203.0.113.10:443",
            [
                new Http1HeaderField("Forwarded", "for=203.0.113.10;proto=https"),
                new Http1HeaderField("X-Forwarded-For", "203.0.113.10")
            ]);

        var result = ProxyHeaderMutationPolicy.ApplyRequestHeaders(
            [
                new Http1HeaderField("Host", "example.test"),
                new Http1HeaderField("X-Remove", "old"),
                new Http1HeaderField("X-Set", "old"),
                new Http1HeaderField("Forwarded", "for=10.0.0.1"),
                new Http1HeaderField("X-Keep", "yes")
            ],
            policy,
            forwardedHeaders);

        AssertEx.True(result.Any(static header => header.Name == "Host" && header.Value == "example.test"));
        AssertEx.True(result.Any(static header => header.Name == "X-Keep" && header.Value == "yes"));
        AssertEx.True(result.Any(static header => header.Name == "X-Set" && header.Value == "new"));
        AssertEx.True(result.Any(static header => header.Name == "Forwarded" && header.Value == "for=203.0.113.10;proto=https"));
        AssertEx.True(result.Any(static header => header.Name == "X-Forwarded-For" && header.Value == "203.0.113.10"));
        AssertEx.False(result.Any(static header => header.Name == "X-Remove"));
        AssertEx.False(result.Any(static header => header.Name == "X-Set" && header.Value == "old"));
        AssertEx.False(result.Any(static header => header.Name == "Forwarded" && header.Value == "for=10.0.0.1"));
    }

    public static void AppliesResponseHeaderMutationPolicy()
    {
        var policy = new RuntimeHeaderPolicy(
            [],
            [],
            [new Http1HeaderField("X-Set", "new")],
            ["X-Remove"]);

        var result = ProxyHeaderMutationPolicy.ApplyResponseHeaders(
            [
                new Http1HeaderField("Content-Type", "text/plain"),
                new Http1HeaderField("X-Remove", "old"),
                new Http1HeaderField("X-Set", "old")
            ],
            policy);

        AssertEx.True(result.Any(static header => header.Name == "Content-Type" && header.Value == "text/plain"));
        AssertEx.True(result.Any(static header => header.Name == "X-Set" && header.Value == "new"));
        AssertEx.False(result.Any(static header => header.Name == "X-Remove"));
        AssertEx.False(result.Any(static header => header.Name == "X-Set" && header.Value == "old"));
    }
}

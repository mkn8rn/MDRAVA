using MDRAVA.INF.Proxy.Forwarding;

namespace MDRAVA.Tests;

internal static class HeaderPolicyTests
{
    public static void FiltersStandardHopByHopHeaders()
    {
        var policy = new HopByHopHeaderPolicy();
        var filtered = policy.FilterForForwarding(
            [
                new ProxyHeaderField("Host", "example.test"),
                new ProxyHeaderField("Connection", "close"),
                new ProxyHeaderField("Keep-Alive", "timeout=5"),
                new ProxyHeaderField("Upgrade", "websocket")
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
                new ProxyHeaderField("Host", "example.test"),
                new ProxyHeaderField("Connection", "x-private"),
                new ProxyHeaderField("X-Private", "do-not-forward")
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
                new ProxyHeaderField("Host", "example.test"),
                new ProxyHeaderField("Proxy-Connection", "keep-alive"),
                new ProxyHeaderField("TE", "trailers"),
                new ProxyHeaderField("Trailer", "expires")
            ],
            preserveTransferEncoding: false,
            preserveTrailer: false);

        AssertEx.Equal(1, filtered.Count);
        AssertEx.Equal("Host", filtered[0].Name);
        AssertEx.True(HopByHopHeaderPolicy.IsHopByHopHeader("proxy-connection"));
        AssertEx.True(HopByHopHeaderPolicy.IsHopByHopHeader("TE"));
        AssertEx.True(HopByHopHeaderPolicy.HasConnectionToken(
            [new ProxyHeaderField("Connection", "keep-alive, Upgrade")],
            "upgrade"));
        AssertEx.False(HopByHopHeaderPolicy.HasConnectionToken(
            [new ProxyHeaderField("Connection", "keep-alive")],
            "upgrade"));
    }

    public static void ClassifiesManagedHttp1FramingHeaders()
    {
        AssertEx.True(Http1ManagedHeaderPolicy.IsManagedFramingHeader("Content-Length"));
        AssertEx.True(Http1ManagedHeaderPolicy.IsManagedFramingHeader("transfer-encoding"));
        AssertEx.True(Http1ManagedHeaderPolicy.IsManagedFramingHeader("Connection"));
        AssertEx.True(Http1ManagedHeaderPolicy.IsManagedFramingHeader("x-request-id"));
        AssertEx.False(Http1ManagedHeaderPolicy.IsManagedFramingHeader("X-Application"));
    }

    public static void ClassifiesManagedStoredResponseHeaders()
    {
        AssertEx.True(Http1ManagedHeaderPolicy.IsManagedStoredResponseHeader("Age"));
        AssertEx.True(Http1ManagedHeaderPolicy.IsManagedStoredResponseHeader("content-length"));
        AssertEx.True(Http1ManagedHeaderPolicy.IsManagedStoredResponseHeader("Transfer-Encoding"));
        AssertEx.True(Http1ManagedHeaderPolicy.IsManagedStoredResponseHeader("Keep-Alive"));
        AssertEx.True(Http1ManagedHeaderPolicy.IsManagedStoredResponseHeader("x-request-id"));
        AssertEx.False(Http1ManagedHeaderPolicy.IsManagedStoredResponseHeader("Cache-Control"));
    }

    public static void AppliesRequestHeaderMutationPolicy()
    {
        var policy = new RuntimeHeaderPolicy(
            [new ProxyHeaderField("X-Set", "new")],
            ["X-Remove"],
            [],
            []);
        var forwardedHeaders = new ForwardedHeadersContext(
            "203.0.113.10",
            "203.0.113.10:443",
            [
                new ProxyHeaderField("Forwarded", "for=203.0.113.10;proto=https"),
                new ProxyHeaderField("X-Forwarded-For", "203.0.113.10")
            ]);

        var result = ProxyHeaderMutationPolicy.ApplyRequestHeaders(
            [
                new ProxyHeaderField("Host", "example.test"),
                new ProxyHeaderField("X-Remove", "old"),
                new ProxyHeaderField("X-Set", "old"),
                new ProxyHeaderField("Forwarded", "for=10.0.0.1"),
                new ProxyHeaderField("X-Keep", "yes")
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
            [new ProxyHeaderField("X-Set", "new")],
            ["X-Remove"]);

        var result = ProxyHeaderMutationPolicy.ApplyResponseHeaders(
            [
                new ProxyHeaderField("Content-Type", "text/plain"),
                new ProxyHeaderField("X-Remove", "old"),
                new ProxyHeaderField("X-Set", "old")
            ],
            policy);

        AssertEx.True(result.Any(static header => header.Name == "Content-Type" && header.Value == "text/plain"));
        AssertEx.True(result.Any(static header => header.Name == "X-Set" && header.Value == "new"));
        AssertEx.False(result.Any(static header => header.Name == "X-Remove"));
        AssertEx.False(result.Any(static header => header.Name == "X-Set" && header.Value == "old"));
    }
}

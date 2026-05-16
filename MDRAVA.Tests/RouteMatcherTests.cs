using MDRAVA.API.Proxy.Configuration;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Protocol;
using MDRAVA.API.Proxy.Routing;

namespace MDRAVA.Tests;

internal static class RouteMatcherTests
{
    public static void MatchesWildcardRoute()
    {
        var matcher = new SingleUpstreamRouteMatcher();
        var snapshot = Snapshot(new ProxyOptions
        {
            Routes =
            [
                new ProxyRouteOptions
                {
                    Name = "default",
                    Host = "*",
                    PathPrefix = "/",
                    Upstreams =
                    [
                        new UpstreamOptions
                        {
                            Name = "local",
                            Address = "127.0.0.1",
                            Port = 5000
                        }
                    ]
                }
            ]
        });

        var request = new Http1RequestHead("GET", "/anything", "/anything", "HTTP/1.1", "example.test", Http1RequestFraming.None, []);

        var match = matcher.Match(snapshot, request);

        AssertEx.NotNull(match);
        AssertEx.Equal("default", match!.Route.Name);
        AssertEx.Equal("local", match.Upstream.Name);
    }

    public static void MatchesHostWithoutRequestPort()
    {
        var matcher = new SingleUpstreamRouteMatcher();
        var snapshot = Snapshot(new ProxyOptions
        {
            Routes =
            [
                new ProxyRouteOptions
                {
                    Name = "example",
                    Host = "example.test",
                    PathPrefix = "/api",
                    Upstreams =
                    [
                        new UpstreamOptions
                        {
                            Name = "api",
                            Address = "127.0.0.1",
                            Port = 5001
                        }
                    ]
                }
            ]
        });

        var request = new Http1RequestHead("GET", "/api/status", "/api/status", "HTTP/1.1", "example.test:8080", Http1RequestFraming.None, []);

        var match = matcher.Match(snapshot, request);

        AssertEx.NotNull(match);
        AssertEx.Equal("example", match!.Route.Name);
        AssertEx.Equal("api", match.Upstream.Name);
    }

    private static ProxyConfigurationSnapshot Snapshot(ProxyOptions options)
    {
        return ProxyConfigurationMapper.ToRuntimeSnapshot(
            options,
            1,
            DateTimeOffset.UnixEpoch,
            "test",
            []);
    }
}

using MDRAVA.API.Proxy.Configuration;
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
        AssertEx.Equal("local", match.Route.Upstreams[0].Name);
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
        AssertEx.Equal("api", match.Route.Upstreams[0].Name);
    }

    public static void ExactHostRouteBeatsWildcardFallbackWhenBothCouldMatch()
    {
        var matcher = new SingleUpstreamRouteMatcher();
        var snapshot = Snapshot(new ProxyOptions
        {
            Routes =
            [
                Route("exact", "example.test", "/", "exact-upstream", 5001),
                Route("wildcard", "*", "/", "wildcard-upstream", 5002)
            ]
        });
        var request = Request("GET", "/resource", "example.test");

        var match = AssertEx.NotNull(matcher.Match(snapshot, request));

        AssertEx.Equal("exact", match.Route.Name);
        AssertEx.Equal("exact-upstream", match.Route.Upstreams[0].Name);
    }

    public static void PortSpecificHostRouteBeatsHostFallbackWhenAuthorityIncludesPort()
    {
        var matcher = new SingleUpstreamRouteMatcher();
        var snapshot = Snapshot(new ProxyOptions
        {
            Routes =
            [
                Route("port-specific", "example.test:8443", "/", "port-upstream", 5001),
                Route("host-only", "example.test", "/", "host-upstream", 5002)
            ]
        });
        var request = Request("GET", "/resource", "example.test:8443");

        var match = AssertEx.NotNull(matcher.Match(snapshot, request));

        AssertEx.Equal("port-specific", match.Route.Name);
        AssertEx.Equal("port-upstream", match.Route.Upstreams[0].Name);
    }

    public static void SpecificRoutePathBeatsCatchAllFallbackWhenBothCouldMatch()
    {
        var matcher = new SingleUpstreamRouteMatcher();
        var snapshot = Snapshot(new ProxyOptions
        {
            Routes =
            [
                Route("api", "example.test", "/api", "api-upstream", 5001),
                Route("default", "example.test", "/", "default-upstream", 5002)
            ]
        });
        var request = Request("GET", "/api/users", "example.test");

        var match = AssertEx.NotNull(matcher.Match(snapshot, request));

        AssertEx.Equal("api", match.Route.Name);
        AssertEx.Equal("api-upstream", match.Route.Upstreams[0].Name);
    }

    private static ProxyRouteOptions Route(
        string name,
        string host,
        string pathPrefix,
        string upstreamName,
        int upstreamPort)
    {
        return new ProxyRouteOptions
        {
            Name = name,
            Host = host,
            PathPrefix = pathPrefix,
            Upstreams =
            [
                new UpstreamOptions
                {
                    Name = upstreamName,
                    Address = "127.0.0.1",
                    Port = upstreamPort
                }
            ]
        };
    }

    private static Http1RequestHead Request(string method, string target, string host)
    {
        var path = target.Split('?', 2)[0];
        return new Http1RequestHead(method, target, path, "HTTP/1.1", host, Http1RequestFraming.None, []);
    }

    private static ProxyConfigurationSnapshot Snapshot(ProxyOptions options)
    {
        var operationalOptions = new ProxyOperationalOptions();
        return ProxyConfigurationRuntimeMapper.ToRuntimeSnapshot(
            options,
            operationalOptions,
            ProxyAdminSecurityTokenPolicy.Resolve(operationalOptions.Admin, static _ => null),
            new Dictionary<string, RuntimeCertificate>(StringComparer.OrdinalIgnoreCase),
            1,
            DateTimeOffset.UnixEpoch,
            "test",
            [],
            new ProxyConfigurationDiscovery(
                new ProxyFilesystemLayout("test", "test/config", "test/config/sites", "test/logs", "test/certs", "test/state", "test/config/proxy.json"),
                [],
                [],
                []));
    }
}

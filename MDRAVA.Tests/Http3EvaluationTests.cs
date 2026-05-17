using MDRAVA.API.Proxy.Configuration;

namespace MDRAVA.Tests;

internal static class Http3EvaluationTests
{
    public static void ListenerProtocolRejectsHttp3Preview()
    {
        var validation = new ProxyOptionsValidator().Validate(
            null,
            ValidProxyOptions(
                new ListenerOptions
                {
                    Name = "preview",
                    Address = "127.0.0.1",
                    Port = 8443,
                    Transport = "https",
                    Protocols = "http3Preview"
                }));

        AssertEx.True(validation.Failed);
        var failures = AssertEx.NotNull(validation.Failures);
        AssertEx.True(
            failures.Any(static failure => failure.Contains("Protocols must be 'http1', 'http2', or 'http1AndHttp2'", StringComparison.Ordinal)),
            string.Join("; ", failures));
    }

    public static void UpstreamProtocolRejectsHttp3()
    {
        var options = ValidProxyOptions(
            new ListenerOptions
            {
                Name = "main",
                Address = "127.0.0.1",
                Port = 8080,
                Transport = "http",
                Protocols = "http1"
            });
        options.Routes[0].Upstreams[0] = new UpstreamOptions
        {
            Name = "h3",
            Scheme = "https",
            Protocol = "http3",
            Address = "upstream.test",
            Port = 443
        };

        var validation = new ProxyOptionsValidator().Validate(null, options);

        AssertEx.True(validation.Failed);
        var failures = AssertEx.NotNull(validation.Failures);
        AssertEx.True(
            failures.Any(static failure => failure.Contains("Protocol must be 'http1' or 'http2'", StringComparison.Ordinal)),
            string.Join("; ", failures));
    }

    public static void ExistingHttp1AndHttp2ProtocolsStillValidate()
    {
        var http1 = new ProxyOptionsValidator().Validate(
            null,
            ValidProxyOptions(
                new ListenerOptions
                {
                    Name = "http1",
                    Address = "127.0.0.1",
                    Port = 8080,
                    Transport = "http",
                    Protocols = "http1"
                }));
        var http2 = new ProxyOptionsValidator().Validate(
            null,
            ValidProxyOptions(
                new ListenerOptions
                {
                    Name = "http2",
                    Address = "127.0.0.1",
                    Port = 8443,
                    Transport = "https",
                    Protocols = "http2"
                }));
        var both = new ProxyOptionsValidator().Validate(
            null,
            ValidProxyOptions(
                new ListenerOptions
                {
                    Name = "both",
                    Address = "127.0.0.1",
                    Port = 9443,
                    Transport = "https",
                    Protocols = "http1AndHttp2"
                }));

        AssertEx.False(http1.Failed, string.Join("; ", http1.Failures ?? []));
        AssertEx.False(http2.Failed, string.Join("; ", http2.Failures ?? []));
        AssertEx.False(both.Failed, string.Join("; ", both.Failures ?? []));
    }

    private static ProxyOptions ValidProxyOptions(ListenerOptions listener)
    {
        return new ProxyOptions
        {
            Listeners = [listener],
            Routes =
            [
                new ProxyRouteOptions
                {
                    Name = "proxy",
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
        };
    }
}

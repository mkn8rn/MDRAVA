using MDRAVA.Tests;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Http1RequestParser parses a valid GET request", Sync(Http1RequestParserTests.ParsesValidGet)),
    ("Http1RequestParser rejects missing Host", Sync(Http1RequestParserTests.RejectsMissingHost)),
    ("Http1RequestParser rejects invalid Content-Length", Sync(Http1RequestParserTests.RejectsInvalidContentLength)),
    ("Http1RequestParser detects request bodies", Sync(Http1RequestParserTests.DetectsRequestBodyIndicators)),
    ("Http1RequestParser parses chunked transfer encoding", Sync(Http1RequestParserTests.ParsesChunkedTransferEncoding)),
    ("Http1RequestParser rejects conflicting Content-Length", Sync(Http1RequestParserTests.RejectsConflictingContentLength)),
    ("Http1RequestParser rejects Content-Length with Transfer-Encoding", Sync(Http1RequestParserTests.RejectsContentLengthWithTransferEncoding)),
    ("Http1RequestParser rejects unsupported Transfer-Encoding", Sync(Http1RequestParserTests.RejectsUnsupportedTransferEncoding)),
    ("Http1ResponseParser parses Content-Length response", Sync(Http1ResponseParserTests.ParsesContentLengthResponse)),
    ("Http1ResponseParser parses chunked response", Sync(Http1ResponseParserTests.ParsesChunkedResponse)),
    ("Http1ResponseParser treats HEAD response as no body", Sync(Http1ResponseParserTests.TreatsHeadResponseAsNoBody)),
    ("Http1ResponseParser treats 204 as no body", Sync(Http1ResponseParserTests.TreatsNoContentAsNoBody)),
    ("Http1ResponseParser treats 304 as no body", Sync(Http1ResponseParserTests.TreatsNotModifiedAsNoBody)),
    ("Http1ResponseParser rejects invalid Content-Length", Sync(Http1ResponseParserTests.RejectsInvalidResponseContentLength)),
    ("Header policy filters standard hop-by-hop headers", Sync(HeaderPolicyTests.FiltersStandardHopByHopHeaders)),
    ("Header policy filters Connection-nominated headers", Sync(HeaderPolicyTests.FiltersConnectionNominatedHeaders)),
    ("SingleUpstreamRouteMatcher matches wildcard route", Sync(RouteMatcherTests.MatchesWildcardRoute)),
    ("SingleUpstreamRouteMatcher matches host without request port", Sync(RouteMatcherTests.MatchesHostWithoutRequestPort)),
    ("Data directory uses configured override", Sync(ConfigurationTests.DataDirectoryUsesConfiguredOverride)),
    ("Data directory uses environment override", Sync(ConfigurationTests.DataDirectoryUsesEnvironmentOverride)),
    ("Data directory defaults under local application data when available", Sync(ConfigurationTests.DataDirectoryDefaultsUnderLocalApplicationDataWhenAvailable)),
    ("Loader loads valid per-site JSON config files", ConfigurationTests.LoaderLoadsValidSiteFiles),
    ("Loader rejects invalid per-site JSON config files", ConfigurationTests.LoaderRejectsInvalidSiteFile),
    ("Reload preserves active snapshot when load fails", ConfigurationTests.ReloadPreservesActiveSnapshotWhenLoadFails),
    ("Reload replaces active snapshot when load succeeds", ConfigurationTests.ReloadReplacesActiveSnapshotWhenLoadSucceeds),
    ("Active inspection projection reflects store", ConfigurationTests.ActiveInspectionProjectionReflectsStore),
    ("Proxy dataplane proxies one GET request end to end", ProxyIntegrationTests.ProxiesSingleGetToUpstream),
    ("Proxy dataplane proxies fixed-length request and response", ProxyIntegrationTests.ProxiesFixedLengthRequestAndResponse),
    ("Proxy dataplane proxies chunked request and response", ProxyIntegrationTests.ProxiesChunkedRequestAndResponse),
    ("Proxy dataplane does not relay HEAD response body", ProxyIntegrationTests.DoesNotRelayHeadResponseBody),
    ("Proxy dataplane proxies 204 without response body", ProxyIntegrationTests.ProxiesNoContentWithoutBody),
    ("Proxy dataplane proxies 304 without response body", ProxyIntegrationTests.ProxiesNotModifiedWithoutBody),
    ("Proxy dataplane rejects invalid request framing", ProxyIntegrationTests.RejectsInvalidRequestFraming),
    ("Proxy dataplane rejects malformed chunked request body", ProxyIntegrationTests.RejectsMalformedChunkedRequestBody),
    ("Proxy dataplane filters hop-by-hop request headers", ProxyIntegrationTests.FiltersHopByHopRequestHeaders),
    ("Proxy dataplane preserves Host header", ProxyIntegrationTests.PreservesHostHeader)
};

var failures = 0;

foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine(exception);
    }
}

if (failures > 0)
{
    Environment.ExitCode = 1;
    return;
}

Console.WriteLine($"Passed {tests.Length} tests.");

static Func<Task> Sync(Action test)
{
    return () =>
    {
        test();
        return Task.CompletedTask;
    };
}

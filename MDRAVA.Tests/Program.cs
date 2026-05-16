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
    ("Loader loads route load-balancing and health-check settings", ConfigurationTests.LoaderLoadsRouteLoadBalancingAndHealthCheckSettings),
    ("Loader creates missing config directories and loads empty snapshot", ConfigurationTests.LoaderCreatesMissingConfigDirectoriesAndLoadsEmptySnapshot),
    ("Loader loads existing empty sites directory", ConfigurationTests.LoaderLoadsExistingEmptySitesDirectory),
    ("Loader uses defaults when operational config is missing", ConfigurationTests.LoaderUsesDefaultsWhenOperationalConfigIsMissing),
    ("Loader loads explicit operational timeout settings", ConfigurationTests.LoaderLoadsExplicitOperationalTimeouts),
    ("Loader loads observability defaults", ConfigurationTests.LoaderLoadsObservabilityDefaults),
    ("Loader loads explicit observability settings", ConfigurationTests.LoaderLoadsExplicitObservabilitySettings),
    ("Loader rejects invalid observability capacity", ConfigurationTests.LoaderRejectsInvalidObservabilityCapacity),
    ("Loader loads hardening limit defaults", ConfigurationTests.LoaderLoadsLimitDefaults),
    ("Loader rejects invalid hardening limits", ConfigurationTests.LoaderRejectsInvalidLimitSettings),
    ("Loader rejects invalid operational timeout settings", ConfigurationTests.LoaderRejectsInvalidOperationalTimeouts),
    ("Loader rejects invalid tunnel connection limit", ConfigurationTests.LoaderRejectsInvalidTunnelLimit),
    ("Loader loads HTTPS listener with certificate", ConfigurationTests.LoaderLoadsHttpsListenerWithCertificate),
    ("Loader rejects HTTPS listener with missing certificate reference", ConfigurationTests.LoaderRejectsHttpsListenerWithMissingCertificateReference),
    ("Loader rejects invalid certificate path", ConfigurationTests.LoaderRejectsInvalidCertificatePath),
    ("Loader rejects invalid certificate password", ConfigurationTests.LoaderRejectsInvalidCertificatePassword),
    ("Loader rejects duplicate SNI certificate mapping", ConfigurationTests.LoaderRejectsDuplicateSniCertificateMapping),
    ("Loader merges SNI mappings from shared HTTPS listener", ConfigurationTests.LoaderMergesSniMappingsFromSharedHttpsListener),
    ("Loader rejects invalid per-site JSON config files", ConfigurationTests.LoaderRejectsInvalidSiteFile),
    ("Reload preserves active snapshot when load fails", ConfigurationTests.ReloadPreservesActiveSnapshotWhenLoadFails),
    ("Reload replaces active snapshot when load succeeds", ConfigurationTests.ReloadReplacesActiveSnapshotWhenLoadSucceeds),
    ("Reload replaces active snapshot with empty sites directory", ConfigurationTests.ReloadReplacesActiveSnapshotWithEmptySitesDirectory),
    ("Active inspection projection reflects store", ConfigurationTests.ActiveInspectionProjectionReflectsStore),
    ("Host startup succeeds from fresh data directory", StartupSmokeTests.StartsFromFreshDataDirectory),
    ("Host startup fails when existing site config is invalid", StartupSmokeTests.FailsStartupWhenExistingSiteConfigIsInvalid),
    ("Host startup succeeds with valid site config", StartupSmokeTests.StartsWithValidSiteConfig),
    ("Proxy dataplane proxies one GET request end to end", ProxyIntegrationTests.ProxiesSingleGetToUpstream),
    ("Proxy dataplane proxies fixed-length request and response", ProxyIntegrationTests.ProxiesFixedLengthRequestAndResponse),
    ("Proxy dataplane proxies chunked request and response", ProxyIntegrationTests.ProxiesChunkedRequestAndResponse),
    ("Proxy dataplane does not relay HEAD response body", ProxyIntegrationTests.DoesNotRelayHeadResponseBody),
    ("Proxy dataplane proxies 204 without response body", ProxyIntegrationTests.ProxiesNoContentWithoutBody),
    ("Proxy dataplane proxies 304 without response body", ProxyIntegrationTests.ProxiesNotModifiedWithoutBody),
    ("Proxy dataplane rejects invalid request framing", ProxyIntegrationTests.RejectsInvalidRequestFraming),
    ("Proxy dataplane rejects malformed chunked request body", ProxyIntegrationTests.RejectsMalformedChunkedRequestBody),
    ("Proxy dataplane filters hop-by-hop request headers", ProxyIntegrationTests.FiltersHopByHopRequestHeaders),
    ("Proxy dataplane preserves Host header", ProxyIntegrationTests.PreservesHostHeader),
    ("Proxy dataplane emits generated response request ID", ProxyIntegrationTests.ResponseIncludesGeneratedRequestId),
    ("Proxy dataplane preserves external request ID in diagnostics", ProxyIntegrationTests.ExternalRequestIdIsPreservedInDiagnostics),
    ("Proxy dataplane records successful diagnostics", ProxyIntegrationTests.SuccessfulRequestProducesDiagnosticRouteAndUpstream),
    ("Proxy dataplane records upstream connect failure diagnostics", ProxyIntegrationTests.UpstreamConnectFailureProducesDiagnosticClassification),
    ("Proxy dataplane can disable access logs while keeping diagnostics", ProxyIntegrationTests.AccessLoggingCanBeDisabledWhileDiagnosticsRemainEnabled),
    ("Proxy dataplane records no-route diagnostics and status summary", ProxyIntegrationTests.NoMatchingRouteProducesDiagnosticClassification),
    ("Proxy dataplane rejects oversized request head", ProxyIntegrationTests.OversizedRequestHeadIsRejected),
    ("Proxy dataplane rejects excessive header count", ProxyIntegrationTests.ExcessiveHeaderCountIsRejected),
    ("Proxy dataplane rejects excessive header line", ProxyIntegrationTests.ExcessiveHeaderLineIsRejected),
    ("Proxy dataplane rejects excessive request body size", ProxyIntegrationTests.ExcessiveRequestBodySizeIsRejected),
    ("Proxy dataplane rejects oversized chunked request body", ProxyIntegrationTests.ChunkedRequestBodySizeIsRejected),
    ("Proxy dataplane enforces per-IP request rate limit", ProxyIntegrationTests.PerIpRequestRateLimitIsEnforced),
    ("Proxy dataplane times out incomplete request head", ProxyIntegrationTests.TimesOutIncompleteRequestHead),
    ("Proxy dataplane times out incomplete Content-Length request body", ProxyIntegrationTests.TimesOutIncompleteContentLengthRequestBody),
    ("Proxy dataplane times out incomplete chunked request body", ProxyIntegrationTests.TimesOutIncompleteChunkedRequestBody),
    ("Proxy dataplane maps unavailable upstream to 502", ProxyIntegrationTests.UnavailableUpstreamProducesBadGateway),
    ("Proxy dataplane maps upstream response-head timeout to 504", ProxyIntegrationTests.UpstreamResponseHeadTimeoutProducesGatewayTimeout),
    ("Proxy dataplane closes after started response on upstream early close", ProxyIntegrationTests.UpstreamContentLengthEarlyCloseClosesAfterStartedResponse),
    ("HTTPS listener proxies GET to upstream", ProxyIntegrationTests.HttpsListenerProxiesGetToUpstream),
    ("HTTPS listener selects certificate by SNI", ProxyIntegrationTests.HttpsListenerSelectsCertificateBySni),
    ("HTTPS listener uses default certificate for unmatched SNI", ProxyIntegrationTests.HttpsListenerUsesDefaultCertificateForUnmatchedSni),
    ("HTTPS listener uses default certificate without SNI", ProxyIntegrationTests.HttpsListenerUsesDefaultCertificateWithoutSni),
    ("HTTPS listener fails handshake when no certificate matches", ProxyIntegrationTests.HttpsListenerFailsHandshakeWhenNoCertificateMatches),
    ("HTTPS listener times out incomplete TLS handshake", ProxyIntegrationTests.HttpsListenerTimesOutIncompleteTlsHandshake),
    ("Persistent client processes two sequential GETs and reuses upstream", ProxyIntegrationTests.PersistentClientProcessesTwoSequentialGetsAndReusesUpstream),
    ("Client Connection close header closes after response", ProxyIntegrationTests.ClientConnectionCloseHeaderClosesAfterResponse),
    ("HTTP/1.0 client closes by default", ProxyIntegrationTests.Http10ClientClosesByDefault),
    ("Max requests per client connection is enforced", ProxyIntegrationTests.MaxRequestsPerClientConnectionIsEnforced),
    ("Client keep-alive idle timeout closes connection", ProxyIntegrationTests.ClientKeepAliveIdleTimeoutClosesConnection),
    ("Malformed second request closes connection", ProxyIntegrationTests.MalformedSecondRequestClosesConnection),
    ("Persistent client proxies Content-Length POST", ProxyIntegrationTests.PersistentClientProxiesContentLengthPost),
    ("Persistent client proxies chunked POST", ProxyIntegrationTests.PersistentClientProxiesChunkedPost),
    ("Upstream connection is not reused after response Connection close", ProxyIntegrationTests.UpstreamConnectionIsNotReusedAfterResponseConnectionClose),
    ("Upstream connection is not reused after premature disconnect", ProxyIntegrationTests.UpstreamConnectionIsNotReusedAfterPrematureDisconnect),
    ("Upstream connection is not reused after framing error", ProxyIntegrationTests.UpstreamConnectionIsNotReusedAfterFramingError),
    ("WebSocket Upgrade over plaintext returns 101", ProxyIntegrationTests.WebSocketUpgradeOverPlaintextReturnsSwitchingProtocols),
    ("WebSocket Upgrade produces tunnel diagnostic", ProxyIntegrationTests.WebSocketUpgradeProducesTunnelDiagnostic),
    ("WebSocket tunnel relays client bytes upstream", ProxyIntegrationTests.WebSocketTunnelRelaysClientBytesToUpstream),
    ("WebSocket tunnel relays upstream bytes client", ProxyIntegrationTests.WebSocketTunnelRelaysUpstreamBytesToClient),
    ("WebSocket tunnel closes when client closes", ProxyIntegrationTests.WebSocketTunnelClosesWhenClientCloses),
    ("WebSocket tunnel closes when upstream closes", ProxyIntegrationTests.WebSocketTunnelClosesWhenUpstreamCloses),
    ("WebSocket tunnel idle timeout closes tunnel", ProxyIntegrationTests.WebSocketTunnelIdleTimeoutClosesTunnel),
    ("WebSocket Upgrade over HTTPS returns 101", ProxyIntegrationTests.WebSocketUpgradeOverHttpsReturnsSwitchingProtocols),
    ("Upgrade does not use normal upstream pool", ProxyIntegrationTests.UpgradeDoesNotUseNormalUpstreamPool),
    ("Missing WebSocket headers are rejected", ProxyIntegrationTests.MissingWebSocketHeadersAreRejected),
    ("Upstream non-101 Upgrade response is forwarded and closed", ProxyIntegrationTests.UpstreamNon101UpgradeResponseIsForwardedAndClosed),
    ("Malformed 101 Upgrade response produces bad gateway", ProxyIntegrationTests.MalformedSwitchingProtocolsResponseProducesBadGateway),
    ("Health check 2xx response is healthy", HealthCheckTests.HealthCheck2xxIsHealthy),
    ("Health check 3xx response is healthy", HealthCheckTests.HealthCheck3xxIsHealthy),
    ("Health check 4xx response is unhealthy", HealthCheckTests.HealthCheck4xxIsUnhealthy),
    ("Health check 5xx response is unhealthy", HealthCheckTests.HealthCheck5xxIsUnhealthy),
    ("Health check timeout is unhealthy", HealthCheckTests.HealthCheckTimeoutIsUnhealthy),
    ("Health state transitions to unhealthy after threshold", Sync(HealthCheckTests.HealthStateTransitionsToUnhealthyAfterThreshold)),
    ("Health state transitions to healthy after recovery threshold", Sync(HealthCheckTests.HealthStateTransitionsToHealthyAfterRecoveryThreshold)),
    ("Round-robin distributes sequential requests across two upstreams", ProxyIntegrationTests.RoundRobinDistributesSequentialRequestsAcrossTwoUpstreams),
    ("Unhealthy upstream is not selected", ProxyIntegrationTests.UnhealthyUpstreamIsNotSelected),
    ("All unhealthy upstreams return service unavailable", ProxyIntegrationTests.AllUnhealthyUpstreamsReturnServiceUnavailable),
    ("WebSocket Upgrade uses round-robin upstream selection", ProxyIntegrationTests.WebSocketUpgradeUsesRoundRobinUpstreamSelection),
    ("Upstream pool uses distinct endpoint keys", ProxyIntegrationTests.UpstreamPoolUsesDistinctEndpointKeys),
    ("Recent diagnostics store is bounded", Sync(ObservabilityTests.RecentDiagnosticsStoreIsBounded)),
    ("Diagnostics controller honors safe limit", Sync(ObservabilityTests.DiagnosticsControllerHonorsSafeLimit)),
    ("Diagnostics event omits bodies and secrets", Sync(ObservabilityTests.DiagnosticsEventDoesNotCarryBodiesOrSecrets)),
    ("Admission controller enforces client limit", Sync(HardeningTests.AdmissionControllerEnforcesClientLimit)),
    ("Admission controller enforces TLS handshake limit", Sync(HardeningTests.AdmissionControllerEnforcesTlsHandshakeLimit)),
    ("Rate limiter enforces request limit and refill", Sync(HardeningTests.RateLimiterEnforcesRequestLimitAndRefills)),
    ("Rate limiter enforces upgrade limit", Sync(HardeningTests.RateLimiterEnforcesUpgradeLimit)),
    ("Rate limiter cleans stale entries", Sync(HardeningTests.RateLimiterCleansStaleEntries)),
    ("Shutdown coordinator exposes grace deadline", Sync(HardeningTests.ShutdownCoordinatorExposesGraceDeadlineAndCancels))
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

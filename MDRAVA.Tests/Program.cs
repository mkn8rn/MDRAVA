using System.Text.Json;
using MDRAVA.Tests;

if (PerformanceSmokeRunner.IsPerformanceCommand(args))
{
    Environment.ExitCode = await PerformanceSmokeRunner.RunAsync(args);
    return;
}

var tests = new TestCase[]
{
    Test("Http1RequestParser parses a valid GET request", Sync(Http1RequestParserTests.ParsesValidGet), TestTaxonomy.Http1),
    Test("Http1RequestParser rejects missing Host", Sync(Http1RequestParserTests.RejectsMissingHost), TestTaxonomy.Http1, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("Http1RequestParser rejects invalid Content-Length", Sync(Http1RequestParserTests.RejectsInvalidContentLength), TestTaxonomy.Http1, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("Http1RequestParser detects request bodies", Sync(Http1RequestParserTests.DetectsRequestBodyIndicators), TestTaxonomy.Http1),
    Test("Http1RequestParser parses chunked transfer encoding", Sync(Http1RequestParserTests.ParsesChunkedTransferEncoding), TestTaxonomy.Http1, TestTaxonomy.Headers),
    Test("Http1RequestParser rejects conflicting Content-Length", Sync(Http1RequestParserTests.RejectsConflictingContentLength), TestTaxonomy.Http1, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("Http1RequestParser rejects Content-Length with Transfer-Encoding", Sync(Http1RequestParserTests.RejectsContentLengthWithTransferEncoding), TestTaxonomy.Http1, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("Http1RequestParser rejects unsupported Transfer-Encoding", Sync(Http1RequestParserTests.RejectsUnsupportedTransferEncoding), TestTaxonomy.Http1, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("Http1ResponseParser parses Content-Length response", Sync(Http1ResponseParserTests.ParsesContentLengthResponse), TestTaxonomy.Http1, TestTaxonomy.Headers),
    Test("Http1ResponseParser parses chunked response", Sync(Http1ResponseParserTests.ParsesChunkedResponse), TestTaxonomy.Http1, TestTaxonomy.Headers),
    Test("Http1ResponseParser treats HEAD response as no body", Sync(Http1ResponseParserTests.TreatsHeadResponseAsNoBody), TestTaxonomy.Http1),
    Test("Http1ResponseParser treats 204 as no body", Sync(Http1ResponseParserTests.TreatsNoContentAsNoBody), TestTaxonomy.Http1),
    Test("Http1ResponseParser treats 304 as no body", Sync(Http1ResponseParserTests.TreatsNotModifiedAsNoBody), TestTaxonomy.Http1),
    Test("Http1ResponseParser rejects invalid Content-Length", Sync(Http1ResponseParserTests.RejectsInvalidResponseContentLength), TestTaxonomy.Http1, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("Header policy filters standard hop-by-hop headers", Sync(HeaderPolicyTests.FiltersStandardHopByHopHeaders), TestTaxonomy.Headers),
    Test("Header policy filters Connection-nominated headers", Sync(HeaderPolicyTests.FiltersConnectionNominatedHeaders), TestTaxonomy.Headers),
    Test("SingleUpstreamRouteMatcher matches wildcard route", Sync(RouteMatcherTests.MatchesWildcardRoute), TestTaxonomy.Routing),
    Test("SingleUpstreamRouteMatcher matches host without request port", Sync(RouteMatcherTests.MatchesHostWithoutRequestPort), TestTaxonomy.Routing, TestTaxonomy.Headers),
    Test("SingleUpstreamRouteMatcher exact host route beats wildcard fallback", Sync(RouteMatcherTests.ExactHostRouteBeatsWildcardFallbackWhenBothCouldMatch), TestTaxonomy.Routing, TestTaxonomy.Headers),
    Test("SingleUpstreamRouteMatcher port-specific host route beats host fallback", Sync(RouteMatcherTests.PortSpecificHostRouteBeatsHostFallbackWhenAuthorityIncludesPort), TestTaxonomy.Routing, TestTaxonomy.Headers),
    Test("SingleUpstreamRouteMatcher specific route path beats catch-all fallback", Sync(RouteMatcherTests.SpecificRoutePathBeatsCatchAllFallbackWhenBothCouldMatch), TestTaxonomy.Routing),
    Test("Data directory uses configured override", Sync(ConfigurationTests.DataDirectoryUsesConfiguredOverride), TestTaxonomy.Config),
    Test("Data directory uses environment override", Sync(ConfigurationTests.DataDirectoryUsesEnvironmentOverride), TestTaxonomy.Config),
    Test("Data directory defaults under local application data when available", Sync(ConfigurationTests.DataDirectoryDefaultsUnderLocalApplicationDataWhenAvailable), TestTaxonomy.Config),
    Test("Loader loads valid per-site JSON config files", ConfigurationTests.LoaderLoadsValidSiteFiles, TestTaxonomy.Config),
    Test("Loader loads equivalent JSON and YAML site files", ConfigurationTests.LoaderLoadsEquivalentJsonAndYamlSiteFiles, TestTaxonomy.Config),
    Test("Loader reports YAML parse errors with per-file diagnostics", ConfigurationTests.LoaderReportsYamlParseErrorsWithPerFileDiagnostics, TestTaxonomy.Config, TestTaxonomy.Metrics),
    Test("Loader loads route load-balancing and health-check settings", ConfigurationTests.LoaderLoadsRouteLoadBalancingAndHealthCheckSettings, TestTaxonomy.Config, TestTaxonomy.Routing),
    Test("Loader creates missing config directories and loads empty snapshot", ConfigurationTests.LoaderCreatesMissingConfigDirectoriesAndLoadsEmptySnapshot, TestTaxonomy.Config, TestTaxonomy.SecurityNegativePaths),
    Test("Loader does not overwrite existing placeholder files", ConfigurationTests.LoaderDoesNotOverwriteExistingPlaceholderFiles, TestTaxonomy.Config),
    Test("Loader loads existing empty sites directory", ConfigurationTests.LoaderLoadsExistingEmptySitesDirectory, TestTaxonomy.Config),
    Test("Loader uses defaults when operational config is missing", ConfigurationTests.LoaderUsesDefaultsWhenOperationalConfigIsMissing, TestTaxonomy.Config, TestTaxonomy.SecurityNegativePaths),
    Test("Loader loads explicit operational timeout settings", ConfigurationTests.LoaderLoadsExplicitOperationalTimeouts, TestTaxonomy.Config, TestTaxonomy.Limits, TestTaxonomy.SecurityNegativePaths),
    Test("Loader loads observability defaults", ConfigurationTests.LoaderLoadsObservabilityDefaults, TestTaxonomy.Config, TestTaxonomy.Metrics),
    Test("Loader loads explicit observability settings", ConfigurationTests.LoaderLoadsExplicitObservabilitySettings, TestTaxonomy.Config, TestTaxonomy.Metrics),
    Test("Loader rejects invalid observability capacity", ConfigurationTests.LoaderRejectsInvalidObservabilityCapacity, TestTaxonomy.Config, TestTaxonomy.Limits, TestTaxonomy.Metrics, TestTaxonomy.SecurityNegativePaths),
    Test("Loader loads hardening limit defaults", ConfigurationTests.LoaderLoadsLimitDefaults, TestTaxonomy.Config, TestTaxonomy.Limits),
    Test("Loader rejects invalid hardening limits", ConfigurationTests.LoaderRejectsInvalidLimitSettings, TestTaxonomy.Config, TestTaxonomy.Limits, TestTaxonomy.SecurityNegativePaths),
    Test("Loader rejects invalid operational timeout settings", ConfigurationTests.LoaderRejectsInvalidOperationalTimeouts, TestTaxonomy.Config, TestTaxonomy.Limits, TestTaxonomy.SecurityNegativePaths),
    Test("Loader rejects invalid tunnel connection limit", ConfigurationTests.LoaderRejectsInvalidTunnelLimit, TestTaxonomy.Config, TestTaxonomy.Limits, TestTaxonomy.SecurityNegativePaths),
    Test("Loader loads HTTPS listener with certificate", ConfigurationTests.LoaderLoadsHttpsListenerWithCertificate, TestTaxonomy.Config, TestTaxonomy.Tls),
    Test("Loader rejects HTTPS listener with missing certificate reference", ConfigurationTests.LoaderRejectsHttpsListenerWithMissingCertificateReference, TestTaxonomy.Config, TestTaxonomy.Tls, TestTaxonomy.SecurityNegativePaths),
    Test("Loader rejects invalid certificate path", ConfigurationTests.LoaderRejectsInvalidCertificatePath, TestTaxonomy.Config, TestTaxonomy.Routing, TestTaxonomy.Tls, TestTaxonomy.SecurityNegativePaths),
    Test("Loader rejects invalid certificate password", ConfigurationTests.LoaderRejectsInvalidCertificatePassword, TestTaxonomy.Config, TestTaxonomy.Tls, TestTaxonomy.SecurityNegativePaths),
    Test("Loader rejects duplicate SNI certificate mapping", ConfigurationTests.LoaderRejectsDuplicateSniCertificateMapping, TestTaxonomy.Config, TestTaxonomy.Tls, TestTaxonomy.SecurityNegativePaths),
    Test("Loader merges SNI mappings from shared HTTPS listener", ConfigurationTests.LoaderMergesSniMappingsFromSharedHttpsListener, TestTaxonomy.Config, TestTaxonomy.Tls),
    Test("Loader rejects invalid per-site JSON config files", ConfigurationTests.LoaderRejectsInvalidSiteFile, TestTaxonomy.Config, TestTaxonomy.SecurityNegativePaths),
    Test("Reload preserves active snapshot when load fails", ConfigurationTests.ReloadPreservesActiveSnapshotWhenLoadFails, TestTaxonomy.Config, TestTaxonomy.SecurityNegativePaths),
    Test("Reload replaces active snapshot when load succeeds", ConfigurationTests.ReloadReplacesActiveSnapshotWhenLoadSucceeds, TestTaxonomy.Config),
    Test("Reload replaces active snapshot with empty sites directory", ConfigurationTests.ReloadReplacesActiveSnapshotWithEmptySitesDirectory, TestTaxonomy.Config),
    Test("Active inspection projection reflects store", ConfigurationTests.ActiveInspectionProjectionReflectsStore, TestTaxonomy.Config),
    Test("Loader rejects unsafe header policy rule", ConfigurationTests.LoaderRejectsUnsafeHeaderRule, TestTaxonomy.Config, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("Config response header policy cannot emit hop-by-hop headers", ConfigurationTests.ResponseHeaderPolicyCannotEmitHopByHopHeaders, TestTaxonomy.Config, TestTaxonomy.Headers),
    Test("Config multi-file conflict reporting is deterministic", ConfigurationTests.MultiFileConfigConflictReportingIsDeterministic, TestTaxonomy.Config),
    Test("Config validate reports valid config without applying", ConfigurationTests.ConfigValidateReportsValidWithoutApplying, TestTaxonomy.Config),
    Test("Config validate reports invalid config without replacing active config", ConfigurationTests.ConfigValidateReportsInvalidWithoutReplacingActiveConfig, TestTaxonomy.Config, TestTaxonomy.SecurityNegativePaths),
    Test("Config normalize converts YAML to JSON without applying", ConfigurationTests.ConfigNormalizeConvertsYamlToJsonWithoutApplying, TestTaxonomy.Config),
    Test("Effective config projection redacts certificate secrets", ConfigurationTests.EffectiveConfigProjectionRedactsCertificateSecrets, TestTaxonomy.Config, TestTaxonomy.Tls, TestTaxonomy.SecurityNegativePaths),
    Test("Expired certificate projection keeps validity window visible", ConfigurationTests.ExpiredCertificateProjectionKeepsValidityWindowVisible, TestTaxonomy.Config, TestTaxonomy.Tls),
    Test("Not-yet-valid certificate projection keeps validity window visible", ConfigurationTests.NotYetValidCertificateProjectionKeepsValidityWindowVisible, TestTaxonomy.Config, TestTaxonomy.Tls),
    Test("Reload failure reports per-file error and preserves active config", ConfigurationTests.ReloadFailureReportsPerFileErrorAndPreservesActiveConfig, TestTaxonomy.Config, TestTaxonomy.SecurityNegativePaths),
    Test("Default admin bind is localhost-only", Sync(AdminSecurityTests.DefaultAdminBindIsLocalhostOnly), TestTaxonomy.Headers, TestTaxonomy.Admin),
    Test("Non-local admin bind without auth is rejected", Sync(AdminSecurityTests.NonLocalAdminBindWithoutAuthIsRejected), TestTaxonomy.Admin, TestTaxonomy.SecurityNegativePaths),
    Test("Operational config rejects non-local admin URL without auth", Sync(AdminSecurityTests.OperationalConfigRejectsNonLocalAdminUrlWithoutAuth), TestTaxonomy.Admin, TestTaxonomy.SecurityNegativePaths),
    Test("Protected admin endpoint rejects missing auth", AdminSecurityTests.ProtectedEndpointRejectsMissingAuth, TestTaxonomy.Admin, TestTaxonomy.SecurityNegativePaths),
    Test("Protected admin endpoint rejects wrong auth", AdminSecurityTests.ProtectedEndpointRejectsWrongAuth, TestTaxonomy.Admin, TestTaxonomy.SecurityNegativePaths),
    Test("Protected admin endpoint accepts valid bearer token", AdminSecurityTests.ProtectedEndpointAcceptsValidBearerToken, TestTaxonomy.Admin),
    Test("Known admin endpoint paths require authentication", AdminSecurityTests.KnownAdminEndpointPathsRequireAuthentication, TestTaxonomy.Routing, TestTaxonomy.Admin),
    Test("Known admin endpoint inventory matches controller routes", Sync(AdminSecurityTests.KnownAdminEndpointInventoryMatchesControllerRoutes), TestTaxonomy.Routing, TestTaxonomy.Admin),
    Test("Known admin endpoint paths accept bearer token and API key", AdminSecurityTests.KnownAdminEndpointPathsAcceptBearerAndApiKey, TestTaxonomy.Admin),
    Test("Admin auth failure response and audit do not expose presented secrets", AdminSecurityTests.AdminAuthFailureResponseAndAuditDoNotExposePresentedSecrets, TestTaxonomy.Admin, TestTaxonomy.SecurityNegativePaths),
    Test("Admin audit capacity evicts oldest entries", Sync(AdminSecurityTests.AdminAuditCapacityEvictsOldestEntries), TestTaxonomy.Limits, TestTaxonomy.Admin),
    Test("Admin audit path omits query secrets", AdminSecurityTests.AdminAuditPathOmitsQuerySecrets, TestTaxonomy.Routing, TestTaxonomy.Admin),
    Test("Sensitive admin projections redact configured secrets", Sync(AdminSecurityTests.SensitiveProjectionRedactsConfiguredAdminSecrets), TestTaxonomy.Config, TestTaxonomy.Admin, TestTaxonomy.SecurityNegativePaths),
    Test("Effective config does not expose admin token", Sync(AdminSecurityTests.EffectiveConfigDoesNotExposeAdminToken), TestTaxonomy.Config, TestTaxonomy.Admin, TestTaxonomy.SecurityNegativePaths),
    Test("Generated placeholder config does not contain real secret", Sync(AdminSecurityTests.GeneratedPlaceholderConfigDoesNotContainRealSecret), TestTaxonomy.Config, TestTaxonomy.Admin),
    Test("Admin audit does not log token values", AdminSecurityTests.AdminAuditDoesNotLogTokenValues, TestTaxonomy.Admin),
    Test("Manual PFX certificate behavior remains valid", AcmeTests.ManualPfxCertificateBehaviorRemainsValid, TestTaxonomy.Tls),
    Test("ACME config validation rejects missing terms acceptance", Sync(AcmeTests.AcmeConfigValidationRejectsMissingTermsAcceptance), TestTaxonomy.Tls, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP-01 challenge returns exact token response", Sync(AcmeTests.Http01ChallengeReturnsExactTokenResponse), TestTaxonomy.Tls, TestTaxonomy.Admin),
    Test("Unknown HTTP-01 challenge returns safe 404", Sync(AcmeTests.UnknownHttp01ChallengeReturnsSafe404), TestTaxonomy.Tls),
    Test("ACME renewal stores material under certs directory", AcmeTests.AcmeRenewalStoresMaterialUnderCertsDirectory, TestTaxonomy.Tls),
    Test("Loader loads stored ACME certificate on startup", AcmeTests.LoaderLoadsStoredAcmeCertificateOnStartup, TestTaxonomy.Config, TestTaxonomy.Tls),
    Test("Failed ACME renewal preserves current active certificate", AcmeTests.FailedAcmeRenewalPreservesCurrentActiveCertificate, TestTaxonomy.Tls, TestTaxonomy.SecurityNegativePaths),
    Test("ACME status projection does not expose private material", AcmeTests.AcmeStatusProjectionDoesNotExposePrivateMaterial, TestTaxonomy.Config, TestTaxonomy.Tls, TestTaxonomy.SecurityNegativePaths),
    Test("ACME renewal avoids tight retry loop after failure", AcmeTests.AcmeRenewalAvoidsTightRetryLoopAfterFailure, TestTaxonomy.Tls, TestTaxonomy.RetryCircuit, TestTaxonomy.SecurityNegativePaths),
    Test("Existing HTTP upstream config remains valid", UpstreamTlsTests.ExistingHttpUpstreamConfigRemainsValid, TestTaxonomy.UpstreamHttp1, TestTaxonomy.Tls),
    Test("HTTPS upstream config parses and validates", UpstreamTlsTests.HttpsUpstreamConfigParsesAndValidates, TestTaxonomy.UpstreamHttp1, TestTaxonomy.Tls),
    Test("Unsupported upstream scheme is rejected", UpstreamTlsTests.UnsupportedUpstreamSchemeIsRejected, TestTaxonomy.UpstreamHttp1, TestTaxonomy.Tls, TestTaxonomy.SecurityNegativePaths),
    Test("Ambiguous upstream address is rejected", UpstreamTlsTests.AmbiguousUpstreamAddressIsRejected, TestTaxonomy.UpstreamHttp1, TestTaxonomy.Tls, TestTaxonomy.SecurityNegativePaths),
    Test("Upstream pool key differs for HTTP and HTTPS", Sync(UpstreamTlsTests.PoolKeyDiffersForHttpAndHttps), TestTaxonomy.UpstreamHttp1, TestTaxonomy.Tls),
    Test("Upstream pool key differs for SNI and validation", Sync(UpstreamTlsTests.PoolKeyDiffersForDifferentSniAndValidation), TestTaxonomy.UpstreamHttp1, TestTaxonomy.Tls),
    Test("HTTPS upstream uses SslStream path", UpstreamTlsTests.HttpsUpstreamUsesSslStreamPath, TestTaxonomy.UpstreamHttp1, TestTaxonomy.Routing, TestTaxonomy.Tls),
    Test("HTTPS upstream proxy forwards through TLS", UpstreamTlsTests.HttpsUpstreamProxyForwardsThroughTls, TestTaxonomy.UpstreamHttp1, TestTaxonomy.Tls),
    Test("HTTPS health checks use TLS settings", UpstreamTlsTests.HttpsHealthChecksUseTlsSettings, TestTaxonomy.UpstreamHttp1, TestTaxonomy.Tls, TestTaxonomy.HealthChecks),
    Test("Upstream certificate validation is enabled by default", UpstreamTlsTests.CertificateValidationIsEnabledByDefault, TestTaxonomy.UpstreamHttp1, TestTaxonomy.Tls),
    Test("Explicit unsafe upstream validation projects as unsafe", UpstreamTlsTests.ExplicitUnsafeValidationModeIsProjectedAsUnsafe, TestTaxonomy.UpstreamHttp1, TestTaxonomy.Tls, TestTaxonomy.SecurityNegativePaths),
    Test("TLS validation failure does not fall back to plaintext", UpstreamTlsTests.TlsValidationFailureDoesNotFallBackToPlaintext, TestTaxonomy.UpstreamHttp1, TestTaxonomy.Tls, TestTaxonomy.SecurityNegativePaths),
    Test("Upstream SNI override validation rejects URL port and wildcard", UpstreamTlsTests.UpstreamSniOverrideValidationRejectsUrlPortAndWildcard, TestTaxonomy.UpstreamHttp1, TestTaxonomy.Tls, TestTaxonomy.SecurityNegativePaths),
    Test("Caching disabled by default", CacheTests.CachingDisabledByDefault, TestTaxonomy.Caching),
    Test("Disabled cache keeps existing proxy behavior", CacheTests.DisabledCacheKeepsExistingProxyBehavior, TestTaxonomy.Caching),
    Test("Invalid cache policy is rejected", Sync(CacheTests.InvalidCachePolicyIsRejected), TestTaxonomy.Caching, TestTaxonomy.SecurityNegativePaths),
    Test("Enabled GET 200 response is stored and served", CacheTests.EnabledGet200ResponseIsStoredAndServed, TestTaxonomy.Caching),
    Test("HEAD cached response returns headers without body", CacheTests.HeadResponseReturnsHeadersWithoutBody, TestTaxonomy.Headers, TestTaxonomy.Caching),
    Test("Query string is part of cache key", Sync(CacheTests.QueryStringIsPartOfCacheKey), TestTaxonomy.Caching),
    Test("Rewrite target is part of cache key", Sync(CacheTests.RewriteTargetIsPartOfCacheKey), TestTaxonomy.Caching),
    Test("Host and vary headers affect cache key", Sync(CacheTests.HostAndVaryHeadersAffectCacheKey), TestTaxonomy.Headers, TestTaxonomy.Caching),
    Test("Authorization request is not cached by default", Sync(CacheTests.AuthorizationRequestIsNotCachedByDefault), TestTaxonomy.Caching, TestTaxonomy.SecurityNegativePaths),
    Test("Cookie request is not cached by default", Sync(CacheTests.CookieRequestIsNotCachedByDefault), TestTaxonomy.Caching, TestTaxonomy.SecurityNegativePaths),
    Test("Set-Cookie response is not cached by default", Sync(CacheTests.SetCookieResponseIsNotCachedByDefault), TestTaxonomy.Caching, TestTaxonomy.SecurityNegativePaths),
    Test("Cache-Control no-store is not cached", Sync(CacheTests.NoStoreResponseIsNotCached), TestTaxonomy.Caching, TestTaxonomy.SecurityNegativePaths),
    Test("Cache-Control no-cache is not cached", Sync(CacheTests.NoCacheResponseIsNotCached), TestTaxonomy.Caching, TestTaxonomy.SecurityNegativePaths),
    Test("Cache-Control must-revalidate is not cached", Sync(CacheTests.MustRevalidateResponseIsNotCached), TestTaxonomy.Caching, TestTaxonomy.SecurityNegativePaths),
    Test("Cache-Control private is not cached by default", Sync(CacheTests.PrivateResponseIsNotCachedByDefault), TestTaxonomy.Caching, TestTaxonomy.SecurityNegativePaths),
    Test("Cache-Control max-age controls TTL and expiry", Sync(CacheTests.MaxAgeControlsTtlAndExpiredEntryIsNotServed), TestTaxonomy.Caching),
    Test("Oversized response is streamed but not cached", CacheTests.OversizedResponseIsStreamedButNotCached, TestTaxonomy.Caching, TestTaxonomy.Limits, TestTaxonomy.SecurityNegativePaths),
    Test("Hop-by-hop cache headers are not stored", Sync(CacheTests.HopByHopHeadersAndTransferEncodingAreNotStored), TestTaxonomy.Headers, TestTaxonomy.Caching),
    Test("Vary header case and duplicate values affect cache key deterministically", Sync(CacheTests.VaryHeaderCaseAndDuplicateValuesAffectCacheKeyDeterministically), TestTaxonomy.Headers, TestTaxonomy.Caching),
    Test("Cache evicts oldest entries at max total bytes", Sync(CacheTests.CacheEvictsOldestEntriesAtMaxTotalBytes), TestTaxonomy.Caching),
    Test("Partial upstream response is not cached", CacheTests.PartialUpstreamResponseIsNotCached, TestTaxonomy.Caching, TestTaxonomy.SecurityNegativePaths),
    Test("Cache clear endpoint clears entries", CacheTests.CacheClearEndpointClearsEntries, TestTaxonomy.Caching),
    Test("Cache clear endpoint is protected", CacheTests.CacheClearEndpointIsProtected, TestTaxonomy.Caching),
    Test("Successful config reload clears cache", CacheTests.SuccessfulReloadClearsCache, TestTaxonomy.Caching),
    Test("Failed config reload does not clear cache", CacheTests.FailedReloadDoesNotClearCache, TestTaxonomy.Caching, TestTaxonomy.SecurityNegativePaths),
    Test("Metrics endpoint is protected by admin auth", MetricsTests.MetricsEndpointIsProtectedByAdminAuth, TestTaxonomy.Admin, TestTaxonomy.Metrics),
    Test("Metrics endpoint returns Prometheus text", Sync(MetricsTests.MetricsEndpointReturnsPrometheusText), TestTaxonomy.Metrics),
    Test("Metrics include request counters after proxied request", MetricsTests.MetricsIncludeRequestCountersAfterProxiedRequest, TestTaxonomy.Metrics),
    Test("Metrics include cache counters after cache activity", Sync(MetricsTests.MetricsIncludeCacheCountersAfterCacheActivity), TestTaxonomy.Caching, TestTaxonomy.Metrics),
    Test("Metrics include reload success and failure counters", MetricsTests.MetricsIncludeReloadCounters, TestTaxonomy.Metrics, TestTaxonomy.SecurityNegativePaths),
    Test("Metrics do not expose raw request details", MetricsTests.MetricsDoNotExposeRawRequestDetails, TestTaxonomy.Metrics),
    Test("Metrics failure matrix does not expose authorization cookie or query secrets", MetricsTests.MetricsFailureMatrixDoesNotExposeAuthorizationCookieOrQuerySecrets, TestTaxonomy.Admin, TestTaxonomy.Metrics, TestTaxonomy.SecurityNegativePaths),
    Test("Public metrics exposure is disabled by default", Sync(MetricsTests.PublicMetricsExposureIsDisabledByDefault), TestTaxonomy.Metrics),
    Test("Invalid metrics config is rejected", Sync(MetricsTests.InvalidMetricsConfigIsRejected), TestTaxonomy.Metrics, TestTaxonomy.SecurityNegativePaths),
    Test("Metric labels are bounded and sanitized", Sync(MetricsTests.MetricLabelsAreBoundedAndSanitized), TestTaxonomy.Limits, TestTaxonomy.Metrics),
    Test("Route dry-run matches dataplane matcher", Sync(RouteDiagnosticsTests.DryRunMatchesSameRouteAsDataplaneMatcher), TestTaxonomy.Routing),
    Test("Route dry-run has no upstream I/O or retry circuit cache mutation", Sync(RouteDiagnosticsTests.DryRunDoesNotPerformUpstreamIoOrMutateRetryCircuitOrCacheState), TestTaxonomy.Routing, TestTaxonomy.Caching, TestTaxonomy.RetryCircuit),
    Test("Route dry-run reports no-match reason", Sync(RouteDiagnosticsTests.DryRunReportsNoMatchReason), TestTaxonomy.Routing),
    Test("Route dry-run reports path rewrite result", Sync(RouteDiagnosticsTests.DryRunReportsPathRewriteResult), TestTaxonomy.Routing),
    Test("Route dry-run can select HTTP/3 protocol listener", Sync(RouteDiagnosticsTests.DryRunCanSelectHttp3ProtocolListener), TestTaxonomy.Http3, TestTaxonomy.Routing),
    Test("Route dry-run reports generated route actions", Sync(RouteDiagnosticsTests.DryRunReportsGeneratedRouteActions), TestTaxonomy.Routing),
    Test("Route dry-run redacts sensitive headers", Sync(RouteDiagnosticsTests.DryRunRedactsSensitiveHeaders), TestTaxonomy.Routing, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("Config lint detects route shadowing and broad catch-all", Sync(RouteDiagnosticsTests.LintDetectsRouteShadowingAndBroadCatchAll), TestTaxonomy.Config, TestTaxonomy.Routing),
    Test("Config lint detects canonical redirect loop", Sync(RouteDiagnosticsTests.LintDetectsCanonicalRedirectLoop), TestTaxonomy.Config, TestTaxonomy.Routing),
    Test("Config lint detects HTTPS redirect without HTTPS listener", Sync(RouteDiagnosticsTests.LintDetectsHttpsRedirectWithoutHttpsListener), TestTaxonomy.Config, TestTaxonomy.Routing, TestTaxonomy.Tls),
    Test("Config lint warns about unsafe upstream TLS validation", Sync(RouteDiagnosticsTests.LintWarnsAboutUnsafeUpstreamTlsValidation), TestTaxonomy.UpstreamHttp1, TestTaxonomy.Config, TestTaxonomy.Routing, TestTaxonomy.Tls, TestTaxonomy.SecurityNegativePaths),
    Test("Config lint omits resolved upstream HTTP/3 pooling limitation", Sync(RouteDiagnosticsTests.LintDoesNotReportResolvedUpstreamHttp3PoolingLimitation), TestTaxonomy.Http3, TestTaxonomy.Config, TestTaxonomy.Routing, TestTaxonomy.Limits),
    Test("Config lint handles JSON and YAML submitted config without applying", Sync(RouteDiagnosticsTests.LintHandlesJsonAndYamlSubmittedConfigWithoutApplying), TestTaxonomy.Config, TestTaxonomy.Routing),
    Test("Config lint output has stable codes and severities", Sync(RouteDiagnosticsTests.LintOutputHasStableCodesAndSeverities), TestTaxonomy.Config, TestTaxonomy.Routing),
    Test("Diagnostic endpoints require admin auth", RouteDiagnosticsTests.DiagnosticEndpointsRequireAdminAuth, TestTaxonomy.Routing, TestTaxonomy.Admin, TestTaxonomy.Metrics),
    Test("Metrics include lint and route dry-run counters", Sync(RouteDiagnosticsTests.MetricsIncludeLintAndRouteDryRunCounters), TestTaxonomy.Routing, TestTaxonomy.Metrics),
    Test("Route-only reload does not rebind unchanged listener", ListenerRebindingTests.RouteOnlyReloadDoesNotRebindUnchangedListener, TestTaxonomy.Config, TestTaxonomy.Routing),
    Test("Adding listener starts only new listener", ListenerRebindingTests.AddingListenerStartsOnlyNewListener, TestTaxonomy.Config),
    Test("Removing listener stops accepting new connections", ListenerRebindingTests.RemovingListenerStopsAcceptingNewConnections, TestTaxonomy.Config),
    Test("Changed listener is replaced safely", ListenerRebindingTests.ChangedListenerIsReplacedSafely, TestTaxonomy.Config),
    Test("Failed new listener start preserves old active listener", ListenerRebindingTests.FailedNewListenerStartPreservesOldActiveListener, TestTaxonomy.Config, TestTaxonomy.SecurityNegativePaths),
    Test("Failed config reload preserves old active listeners", ListenerRebindingTests.FailedConfigReloadPreservesOldActiveListeners, TestTaxonomy.Config, TestTaxonomy.SecurityNegativePaths),
    Test("Certificate-only update does not rebind listener", ListenerRebindingTests.CertificateOnlyUpdateDoesNotRebindListener, TestTaxonomy.Config, TestTaxonomy.Tls),
    Test("Admin bind is not affected by proxy listener reload", ListenerRebindingTests.AdminBindIsNotAffectedByProxyListenerReload, TestTaxonomy.Config, TestTaxonomy.Admin),
    Test("Reload diagnostics report listener diff", ListenerRebindingTests.ReloadDiagnosticsReportListenerDiff, TestTaxonomy.Config, TestTaxonomy.Metrics),
    Test("Metrics count listener reload outcomes", ListenerRebindingTests.MetricsCountListenerReloadOutcomes, TestTaxonomy.Config, TestTaxonomy.Metrics),
    Test("HTTP/2 client preserves HTTP/1.1 behavior", ClientHttp2Tests.ExistingHttp1BehaviorRemainsUnchanged, TestTaxonomy.Http1, TestTaxonomy.Http2),
    Test("HTTP/2 plaintext listener is rejected", Sync(ClientHttp2Tests.PlaintextHttp2ListenerIsRejected), TestTaxonomy.Http2, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/2 ALPN selects h2 when enabled", ClientHttp2Tests.AlpnSelectsHttp2WhenEnabled, TestTaxonomy.Http2, TestTaxonomy.Tls),
    Test("HTTP/2 request maps to route matcher", ClientHttp2Tests.Http2RequestMapsToRouteMatcher, TestTaxonomy.Http2, TestTaxonomy.Routing),
    Test("HTTP/2 authority maps to host routing", ClientHttp2Tests.AuthorityMapsToHostRouting, TestTaxonomy.Http2, TestTaxonomy.Headers, TestTaxonomy.Admin),
    Test("HTTP/2 query string is preserved", ClientHttp2Tests.QueryStringIsPreserved, TestTaxonomy.Http2),
    Test("HTTP/2 invalid pseudo headers are rejected", ClientHttp2Tests.InvalidPseudoHeadersAreRejected, TestTaxonomy.Http2, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/2 forbidden connection headers are rejected", ClientHttp2Tests.ForbiddenConnectionHeadersAreRejected, TestTaxonomy.Http2, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/2 Huffman request header values are decoded", ClientHttp2Tests.HuffmanRequestHeaderValuesAreDecoded, TestTaxonomy.Http2, TestTaxonomy.Headers),
    Test("HTTP/2 response omits hop-by-hop headers", ClientHttp2Tests.ResponseOmitsHopByHopHeaders, TestTaxonomy.Http2, TestTaxonomy.Headers),
    Test("HTTP/2 static response route works", ClientHttp2Tests.StaticResponseRouteWorksOverHttp2, TestTaxonomy.Http2, TestTaxonomy.Routing),
    Test("Active HTTP/2 traffic survives certificate reload and new connections use reloaded certificate", ClientHttp2Tests.ActiveHttp2TrafficSurvivesCertificateReloadAndNewConnectionsUseReloadedCertificate, TestTaxonomy.Http2, TestTaxonomy.Tls),
    Test("Failed HTTP/2 certificate reload preserves previous active certificate", ClientHttp2Tests.FailedHttp2CertificateReloadPreservesPreviousActiveCertificate, TestTaxonomy.Http2, TestTaxonomy.Tls, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/2 redirect route works", ClientHttp2Tests.RedirectRouteWorksOverHttp2, TestTaxonomy.Http2, TestTaxonomy.Routing),
    Test("HTTP/2 maintenance route works", ClientHttp2Tests.MaintenanceRouteWorksOverHttp2, TestTaxonomy.Http2, TestTaxonomy.Routing),
    Test("HTTP/2 HEAD returns no body", ClientHttp2Tests.HeadReturnsHeadersWithoutBody, TestTaxonomy.Http2),
    Test("HTTP/2 cache works", ClientHttp2Tests.CacheWorksOverHttp2, TestTaxonomy.Http2, TestTaxonomy.Caching),
    Test("HTTP/2 retry works for proxy requests", ClientHttp2Tests.RetryWorksForHttp2ProxyRequests, TestTaxonomy.Http2, TestTaxonomy.RetryCircuit),
    Test("HTTP/2 extended CONNECT is rejected", ClientHttp2Tests.ExtendedConnectIsRejected, TestTaxonomy.Http2, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/2 concurrent streams reach different routes", ClientHttp2Tests.ConcurrentStreamsReachDifferentRoutes, TestTaxonomy.Http2, TestTaxonomy.Routing),
    Test("HTTP/2 DATA before HEADERS is rejected safely", ClientHttp2Tests.DataBeforeHeadersIsRejectedSafely, TestTaxonomy.Http2, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/2 CONTINUATION header fragmentation is accepted", ClientHttp2Tests.ContinuationHeaderFragmentationIsAccepted, TestTaxonomy.Http2, TestTaxonomy.Headers),
    Test("HTTP/2 RST_STREAM releases state and keeps connection usable", ClientHttp2Tests.RstStreamReleasesStateAndKeepsConnectionUsable, TestTaxonomy.Http2),
    Test("HTTP/2 GOAWAY stops new streams safely", ClientHttp2Tests.GoAwayStopsNewStreamsSafely, TestTaxonomy.Http2),
    Test("HTTP/2 oversized header list is rejected", ClientHttp2Tests.OversizedHeaderListIsRejected, TestTaxonomy.Http2, TestTaxonomy.Headers, TestTaxonomy.Limits, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/2 metrics include counters", ClientHttp2Tests.MetricsIncludeHttp2Counters, TestTaxonomy.Http2, TestTaxonomy.Metrics),
    Test("Upstream HTTP/2 preserves HTTP/1.1 upstream default", UpstreamHttp2Tests.ExistingHttp1UpstreamProtocolRemainsDefault, TestTaxonomy.Http1, TestTaxonomy.Http2, TestTaxonomy.UpstreamHttp2),
    Test("Upstream HTTP/2 requires HTTPS", Sync(UpstreamHttp2Tests.Http2UpstreamRequiresHttps), TestTaxonomy.Http2, TestTaxonomy.UpstreamHttp2, TestTaxonomy.Tls),
    Test("Unsupported upstream protocol is rejected", Sync(UpstreamHttp2Tests.UnsupportedUpstreamProtocolIsRejected), TestTaxonomy.Http2, TestTaxonomy.UpstreamHttp2, TestTaxonomy.SecurityNegativePaths),
    Test("Upstream pool key differs for HTTP/1.1 and HTTP/2", Sync(UpstreamHttp2Tests.PoolKeyDiffersForHttp1AndHttp2), TestTaxonomy.Http1, TestTaxonomy.Http2, TestTaxonomy.UpstreamHttp2),
    Test("Upstream HTTP/2 advertises h2 ALPN", UpstreamHttp2Tests.UpstreamAlpnAdvertisesHttp2, TestTaxonomy.Http2, TestTaxonomy.UpstreamHttp2, TestTaxonomy.Tls),
    Test("Upstream HTTP/2 ALPN failure does not fallback", UpstreamHttp2Tests.AlpnFailureDoesNotFallbackToHttp1, TestTaxonomy.Http2, TestTaxonomy.UpstreamHttp2, TestTaxonomy.Tls, TestTaxonomy.SecurityNegativePaths),
    Test("Upstream HTTP/2 proxy maps headers query and response", UpstreamHttp2Tests.Http2UpstreamProxyMapsHeadersQueryAndResponse, TestTaxonomy.Http2, TestTaxonomy.UpstreamHttp2, TestTaxonomy.Headers),
    Test("Upstream HTTP/2 forwards request body", UpstreamHttp2Tests.Http2UpstreamForwardsRequestBody, TestTaxonomy.Http2, TestTaxonomy.UpstreamHttp2),
    Test("Upstream HTTP/2 ends zero-length request body", UpstreamHttp2Tests.Http2UpstreamEndsZeroLengthRequestBody, TestTaxonomy.Http2, TestTaxonomy.UpstreamHttp2),
    Test("Cache works with HTTP/2 upstream", UpstreamHttp2Tests.CacheWorksWithHttp2Upstream, TestTaxonomy.Http2, TestTaxonomy.UpstreamHttp2, TestTaxonomy.Caching),
    Test("HTTP/2 upstream health check uses h2 and rejects wrong ALPN", UpstreamHttp2Tests.Http2HealthCheckUsesH2AndRejectsWrongAlpn, TestTaxonomy.Http2, TestTaxonomy.UpstreamHttp2, TestTaxonomy.Tls, TestTaxonomy.HealthChecks, TestTaxonomy.SecurityNegativePaths),
    Test("Metrics include upstream HTTP/2 counters", UpstreamHttp2Tests.MetricsIncludeUpstreamHttp2Counters, TestTaxonomy.Http2, TestTaxonomy.UpstreamHttp2, TestTaxonomy.Metrics),
    Test("HTTP/2 upstream close before response headers returns safe failure", UpstreamHttp2Tests.Http2UpstreamCloseBeforeResponseHeadersReturnsSafeFailure, TestTaxonomy.Http2, TestTaxonomy.UpstreamHttp2, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/2 upstream close after response headers does not retry after headers are sent", UpstreamHttp2Tests.Http2UpstreamCloseAfterResponseHeadersDoesNotRetryAfterHeadersAreSent, TestTaxonomy.Http2, TestTaxonomy.UpstreamHttp2, TestTaxonomy.RetryCircuit),
    Test("HTTP/2 streaming POST body is not retried after upstream failure", UpstreamHttp2Tests.Http2StreamingPostBodyIsNotRetriedAfterUpstreamFailure, TestTaxonomy.Http2, TestTaxonomy.UpstreamHttp2, TestTaxonomy.RetryCircuit, TestTaxonomy.SecurityNegativePaths),
    Test("Upstream HTTP/3 requires HTTPS", Sync(UpstreamHttp3Tests.Http3UpstreamRequiresHttps), TestTaxonomy.Http3, TestTaxonomy.UpstreamHttp3, TestTaxonomy.Tls),
    Test("Upstream HTTP/3 config parses and validates", UpstreamHttp3Tests.Http3UpstreamConfigParsesAndValidates, TestTaxonomy.Http3, TestTaxonomy.UpstreamHttp3),
    Test("Upstream HTTP/3 effective projection reports reused multiplexed pooling", UpstreamHttp3Tests.Http3EffectiveProjectionReportsReusedMultiplexedPooling, TestTaxonomy.Http3, TestTaxonomy.UpstreamHttp3, TestTaxonomy.Config),
    Test("Upstream pool key differs for HTTP/1.1 HTTP/2 and HTTP/3", Sync(UpstreamHttp3Tests.PoolKeyDiffersForHttp1Http2AndHttp3), TestTaxonomy.Http1, TestTaxonomy.Http2, TestTaxonomy.Http3, TestTaxonomy.UpstreamHttp3),
    Test("Upstream HTTP/3 pool key includes SNI and validation", Sync(UpstreamHttp3Tests.PoolKeyIncludesHttp3SniAndValidation), TestTaxonomy.Http3, TestTaxonomy.UpstreamHttp3, TestTaxonomy.Tls),
    Test("Upstream HTTP/3 proxy maps headers query and response", UpstreamHttp3Tests.Http3UpstreamProxyMapsHeadersQueryAndResponse, TestTaxonomy.Http3, TestTaxonomy.UpstreamHttp3, TestTaxonomy.Headers),
    Test("Sequential upstream HTTP/3 requests reuse a pooled connection", UpstreamHttp3Tests.SequentialHttp3UpstreamRequestsReuseConnection, TestTaxonomy.Http3, TestTaxonomy.UpstreamHttp3),
    Test("Concurrent upstream HTTP/3 requests share a pooled connection", UpstreamHttp3Tests.ConcurrentHttp3UpstreamRequestsShareConnection, TestTaxonomy.Http3, TestTaxonomy.UpstreamHttp3),
    Test("Idle upstream HTTP/3 pooled connections expire", UpstreamHttp3Tests.IdleHttp3UpstreamConnectionsExpire, TestTaxonomy.Http3, TestTaxonomy.UpstreamHttp3),
    Test("Upstream HTTP/3 GOAWAY drains connection without breaking active stream", UpstreamHttp3Tests.UpstreamHttp3GoAwayDrainsConnectionWithoutBreakingActiveStream, TestTaxonomy.Http3, TestTaxonomy.UpstreamHttp3),
    Test("Upstream HTTP/3 pool stream-limit exhaustion returns safe failure", UpstreamHttp3Tests.UpstreamHttp3PoolStreamLimitExhaustionReturnsSafeFailure, TestTaxonomy.Http3, TestTaxonomy.UpstreamHttp3, TestTaxonomy.Limits, TestTaxonomy.SecurityNegativePaths),
    Test("Upstream HTTP/3 concurrent reuse releases active stream gauge", UpstreamHttp3Tests.ConcurrentHttp3UpstreamReuseReleasesActiveStreamGauge, TestTaxonomy.Http3, TestTaxonomy.UpstreamHttp3),
    Test("Upstream HTTP/3 stream reset does not poison unrelated active stream", UpstreamHttp3Tests.UpstreamHttp3StreamResetDoesNotPoisonUnrelatedActiveStream, TestTaxonomy.Http3, TestTaxonomy.UpstreamHttp3),
    Test("Upstream HTTP/3 failed connection does not receive new streams", UpstreamHttp3Tests.FailedHttp3UpstreamConnectionDoesNotReceiveNewStreams, TestTaxonomy.Http3, TestTaxonomy.UpstreamHttp3, TestTaxonomy.SecurityNegativePaths),
    Test("Upstream HTTP/3 ALPN failure does not downgrade", UpstreamHttp3Tests.Http3UpstreamAlpnFailureDoesNotDowngrade, TestTaxonomy.Http3, TestTaxonomy.UpstreamHttp3, TestTaxonomy.Tls, TestTaxonomy.SecurityNegativePaths),
    Test("Upstream HTTP/3 malformed response headers are rejected", UpstreamHttp3Tests.Http3UpstreamMalformedResponseHeadersAreRejected, TestTaxonomy.Http3, TestTaxonomy.UpstreamHttp3, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("Upstream HTTP/3 forwards request body", UpstreamHttp3Tests.Http3UpstreamForwardsRequestBody, TestTaxonomy.Http3, TestTaxonomy.UpstreamHttp3),
    Test("Upstream HTTP/3 health check uses h3", UpstreamHttp3Tests.Http3HealthCheckUsesH3, TestTaxonomy.Http3, TestTaxonomy.UpstreamHttp3, TestTaxonomy.HealthChecks),
    Test("Cache works with HTTP/3 upstream", UpstreamHttp3Tests.CacheWorksWithHttp3Upstream, TestTaxonomy.Http3, TestTaxonomy.UpstreamHttp3, TestTaxonomy.Caching),
    Test("Metrics include upstream HTTP/3 counters", UpstreamHttp3Tests.MetricsIncludeUpstreamHttp3Counters, TestTaxonomy.Http3, TestTaxonomy.UpstreamHttp3, TestTaxonomy.Metrics),
    Test("HTTP/3 upstream close before response headers returns safe failure", UpstreamHttp3Tests.Http3UpstreamCloseBeforeResponseHeadersReturnsSafeFailure, TestTaxonomy.Http3, TestTaxonomy.UpstreamHttp3, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/3 upstream close after response headers releases stream slot", UpstreamHttp3Tests.Http3UpstreamCloseAfterResponseHeadersReleasesStreamSlot, TestTaxonomy.Http3, TestTaxonomy.UpstreamHttp3, TestTaxonomy.RetryCircuit),
    Test("HTTP/3 streaming POST body is not retried after upstream failure", UpstreamHttp3Tests.Http3StreamingPostBodyIsNotRetriedAfterUpstreamFailure, TestTaxonomy.Http3, TestTaxonomy.UpstreamHttp3, TestTaxonomy.RetryCircuit, TestTaxonomy.SecurityNegativePaths),
    Test("Existing HTTP/1.1 and HTTP/2 listener protocols still validate", Sync(Http3InfrastructureTests.ExistingHttp1AndHttp2ProtocolsStillValidate), TestTaxonomy.Http1, TestTaxonomy.Http2, TestTaxonomy.Http3, TestTaxonomy.Config),
    Test("Listener protocol config parsing preserves HTTP/3 compatibility names", Sync(Http3InfrastructureTests.ListenerProtocolConfigParsingPreservesCompatibility), TestTaxonomy.Http3, TestTaxonomy.Config),
    Test("HTTP/3 defaults on for eligible TLS listeners", Sync(Http3InfrastructureTests.Http3DefaultEnabledForEligibleTlsListener), TestTaxonomy.Http3, TestTaxonomy.Config, TestTaxonomy.Tls),
    Test("HTTP/3 defaults off for plaintext listeners", Sync(Http3InfrastructureTests.Http3DefaultDisabledForPlaintextListener), TestTaxonomy.Http3, TestTaxonomy.Config),
    Test("Legacy HTTP/3 protocol token no longer requires experimental gate for default enablement", Sync(Http3InfrastructureTests.Http3PreviewProtocolDoesNotRequireExperimentalGateForDefaultEnablement), TestTaxonomy.Http3, TestTaxonomy.Config, TestTaxonomy.Admin),
    Test("Legacy HTTP/3 protocol token requires TLS certificate capable listener", Sync(Http3InfrastructureTests.Http3PreviewRequiresTlsCertificateCapableListener), TestTaxonomy.Http3, TestTaxonomy.Config, TestTaxonomy.Tls, TestTaxonomy.Admin),
    Test("Legacy HTTP/3 config is accepted with gate and enables traffic", Sync(Http3InfrastructureTests.Http3PreviewConfigIsAcceptedWithExplicitGateAndEnablesPreviewTraffic), TestTaxonomy.Http3, TestTaxonomy.Config),
    Test("HTTP/3-only legacy config does not enable TCP traffic", Sync(Http3InfrastructureTests.Http3OnlyPreviewDoesNotEnableTcpTraffic), TestTaxonomy.Http3, TestTaxonomy.Config),
    Test("TCP listener identity remains unchanged", Sync(Http3InfrastructureTests.TcpListenerIdentityRemainsUnchanged), TestTaxonomy.Http3, TestTaxonomy.Config),
    Test("Future QUIC listener identity is separate from TCP identity", Sync(Http3InfrastructureTests.FutureQuicListenerIdentityIsSeparateFromTcpIdentity), TestTaxonomy.Http3, TestTaxonomy.Config),
    Test("TCP ALPN does not advertise HTTP/3", Sync(Http3InfrastructureTests.TcpAlpnDoesNotAdvertiseHttp3), TestTaxonomy.Http3, TestTaxonomy.Config, TestTaxonomy.Tls),
    Test("Status and effective projection report legacy HTTP/3 config enabled", Sync(Http3InfrastructureTests.StatusAndEffectiveProjectionReportLegacyHttp3PreviewEnabled), TestTaxonomy.Http3, TestTaxonomy.Config),
    Test("Final HTTP/3 support projection reports matrix and final naming", Sync(Http3InfrastructureTests.FinalSupportProjectionReportsHttp3MatrixAndFinalNaming), TestTaxonomy.Http3, TestTaxonomy.Config),
    Test("HTTP/3 upstream protocol accepts explicit config", Sync(Http3InfrastructureTests.UpstreamProtocolAcceptsExplicitHttp3), TestTaxonomy.Http3, TestTaxonomy.UpstreamHttp3, TestTaxonomy.Config),
    Test("HTTP/3 defaults on for eligible TLS runtime listener", Sync(ClientHttp3PreviewTests.Http3DefaultEnabledForEligibleTlsListener), TestTaxonomy.Http3, TestTaxonomy.Tls),
    Test("Explicit HTTP/3 disable prevents traffic", Sync(ClientHttp3PreviewTests.ExplicitHttp3DisablePreventsTraffic), TestTaxonomy.Http3),
    Test("HTTP/3 QUIC listener identity is separate from TCP identity", Sync(ClientHttp3PreviewTests.QuicListenerIdentityIsSeparateFromTcpIdentity), TestTaxonomy.Http3),
    Test("Failed HTTP/3 QUIC listener start does not break TCP listener", ClientHttp3PreviewTests.FailedQuicListenerStartDoesNotBreakTcpListener, TestTaxonomy.Http3, TestTaxonomy.SecurityNegativePaths),
    Test("Default HTTP/3 TLS listener starts QUIC and emits Alt-Svc", ClientHttp3PreviewTests.DefaultHttp3TlsListenerStartsQuicAndEmitsAltSvc, TestTaxonomy.Http3, TestTaxonomy.Tls),
    Test("Successful reload can add and remove HTTP/3 QUIC listener", ClientHttp3PreviewTests.SuccessfulReloadCanAddAndRemovePreviewQuicListener, TestTaxonomy.Http3),
    Test("Failed reload preserves old HTTP/3 listener set", ClientHttp3PreviewTests.FailedReloadPreservesOldPreviewQuicListenerSet, TestTaxonomy.Http3, TestTaxonomy.SecurityNegativePaths),
    Test("Successful HTTP/3 certificate reload keeps QUIC listener and uses new certificate", ClientHttp3PreviewTests.SuccessfulHttp3CertificateReloadKeepsQuicListenerAndUsesNewCertificate, TestTaxonomy.Http3, TestTaxonomy.Tls),
    Test("Failed HTTP/3 certificate reload preserves previous QUIC certificate", ClientHttp3PreviewTests.FailedHttp3CertificateReloadPreservesPreviousQuicCertificate, TestTaxonomy.Http3, TestTaxonomy.Tls, TestTaxonomy.SecurityNegativePaths),
    Test("Status and effective config mark HTTP/3 as experimental preview", Sync(ClientHttp3PreviewTests.StatusAndEffectiveConfigMarkHttp3AsExperimentalPreview), TestTaxonomy.Http3, TestTaxonomy.Config),
    Test("HTTP/3 beta enablement is explicitly projected", Sync(ClientHttp3PreviewTests.Http3BetaEnablementIsExplicitlyProjected), TestTaxonomy.Http3),
    Test("HTTP/3 Alt-Svc is absent when HTTP/3 is explicitly disabled", ClientHttp3PreviewTests.AltSvcIsAbsentWhenHttp3ExplicitlyDisabled, TestTaxonomy.Http3, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/3 Alt-Svc is emitted only when configured and ready", ClientHttp3PreviewTests.AltSvcIsEmittedOnlyWhenConfiguredAndReady, TestTaxonomy.Http3),
    Test("HTTP/3 Alt-Svc is not emitted when QUIC listener is not ready", ClientHttp3PreviewTests.AltSvcIsNotEmittedWhenQuicListenerIsNotReady, TestTaxonomy.Http3),
    Test("Admin responses do not emit HTTP/3 Alt-Svc", Sync(ClientHttp3PreviewTests.AdminResponsesDoNotEmitAltSvc), TestTaxonomy.Http3, TestTaxonomy.Admin),
    Test("Minimal HTTP/3 GET can reach generated route", ClientHttp3PreviewTests.MinimalHttp3GetCanReachGeneratedRoute, TestTaxonomy.Http3, TestTaxonomy.Routing),
    Test("HTTP/3 HEAD returns headers without body", ClientHttp3PreviewTests.HeadReturnsHeadersWithoutBody, TestTaxonomy.Http3, TestTaxonomy.Headers),
    Test("HTTP/3 generated redirect route works", ClientHttp3PreviewTests.Http3GeneratedRedirectRouteWorks, TestTaxonomy.Http3, TestTaxonomy.Routing),
    Test("HTTP/3 generated maintenance route works", ClientHttp3PreviewTests.Http3GeneratedMaintenanceRouteWorks, TestTaxonomy.Http3, TestTaxonomy.Routing),
    Test("HTTP/3 route miss returns safe 404", ClientHttp3PreviewTests.Http3RouteMissReturnsSafe404, TestTaxonomy.Http3, TestTaxonomy.Routing),
    Test("HTTP/3 route miss remains stable across repeated ready-listener requests", ClientHttp3PreviewTests.Http3RouteMissRemainsStableAcrossRepeatedReadyListenerRequests, TestTaxonomy.Http3, TestTaxonomy.Routing),
    Test("HTTP/3 GET proxy route works", ClientHttp3PreviewTests.Http3GetProxyRouteWorks, TestTaxonomy.Http3, TestTaxonomy.Routing),
    Test("HTTP/3 HEAD proxy route works", ClientHttp3PreviewTests.Http3HeadProxyRouteWorks, TestTaxonomy.Http3, TestTaxonomy.Routing),
    Test("HTTP/3 proxy preserves query string", ClientHttp3PreviewTests.Http3ProxyPreservesQueryString, TestTaxonomy.Http3),
    Test("HTTP/3 proxy strips pseudo headers before upstream", ClientHttp3PreviewTests.Http3ProxyStripsPseudoHeadersBeforeUpstream, TestTaxonomy.Http3, TestTaxonomy.Headers),
    Test("HTTP/3 response headers are encoded safely", ClientHttp3PreviewTests.Http3ResponseHeadersAreEncodedSafely, TestTaxonomy.Http3, TestTaxonomy.Headers),
    Test("HTTP/3 chunked response streams body without Transfer-Encoding", ClientHttp3PreviewTests.Http3ChunkedResponseStreamsBodyWithoutTransferEncoding, TestTaxonomy.Http3, TestTaxonomy.Headers),
    Test("HTTP/3 response streams before upstream completes", ClientHttp3PreviewTests.Http3ResponseStreamsBeforeUpstreamCompletes, TestTaxonomy.Http3),
    Test("HTTP/3 cache interaction uses stored response", ClientHttp3PreviewTests.Http3CacheInteractionUsesStoredResponse, TestTaxonomy.Http3, TestTaxonomy.Caching),
    Test("HTTP/3 oversized cache candidate streams but is not cached", ClientHttp3PreviewTests.Http3OversizedCacheCandidateStreamsButIsNotCached, TestTaxonomy.Http3, TestTaxonomy.Caching, TestTaxonomy.Limits, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/3 retry for GET can reach second upstream", ClientHttp3PreviewTests.Http3RetryForGetCanReachSecondUpstream, TestTaxonomy.Http3, TestTaxonomy.RetryCircuit),
    Test("HTTP/3 unsupported CONNECT is rejected", ClientHttp3PreviewTests.UnsupportedConnectIsRejected, TestTaxonomy.Http3, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/3 malformed CONNECT is rejected", ClientHttp3PreviewTests.MalformedHttp3ConnectIsRejected, TestTaxonomy.Http3, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/3 extended CONNECT WebSocket form is rejected", ClientHttp3PreviewTests.ExtendedHttp3ConnectWebSocketIsRejected, TestTaxonomy.Http1, TestTaxonomy.Http3, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/3 POST with bounded body reaches upstream", ClientHttp3PreviewTests.Http3PostWithBoundedBodyReachesUpstream, TestTaxonomy.Http3, TestTaxonomy.Limits),
    Test("HTTP/3 PUT PATCH and DELETE bodies reach upstream", ClientHttp3PreviewTests.Http3PutPatchAndDeleteBodiesReachUpstream, TestTaxonomy.Http3),
    Test("HTTP/3 path rewrite applies to proxy route", ClientHttp3PreviewTests.Http3PathRewriteAppliesToProxyRoute, TestTaxonomy.Http3, TestTaxonomy.Routing),
    Test("HTTP/3 body size limit applies", ClientHttp3PreviewTests.Http3BodySizeLimitApplies, TestTaxonomy.Http3, TestTaxonomy.Limits),
    Test("HTTP/3 legacy buffered request body limit does not block streaming", ClientHttp3PreviewTests.Http3LegacyBufferedRequestBodyLimitDoesNotBlockStreaming, TestTaxonomy.Http3, TestTaxonomy.Limits),
    Test("HTTP/3 request with body is not retried", ClientHttp3PreviewTests.Http3RequestWithBodyIsNotRetried, TestTaxonomy.Http3, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/3 invalid frame sequence is rejected", ClientHttp3PreviewTests.InvalidFrameSequenceIsRejected, TestTaxonomy.Http3, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/3 unexpected control frame on request stream is rejected", ClientHttp3PreviewTests.UnexpectedControlFrameOnRequestStreamIsRejected, TestTaxonomy.Http3, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/3 GOAWAY frame on request stream is rejected", ClientHttp3PreviewTests.GoAwayFrameOnRequestStreamIsRejected, TestTaxonomy.Http3, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/3 duplicate HEADERS after request headers are rejected", ClientHttp3PreviewTests.DuplicateHeadersAfterHeadersIsRejected, TestTaxonomy.Http3, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/3 unknown frame before headers is rejected", ClientHttp3PreviewTests.UnknownFrameBeforeHeadersIsRejected, TestTaxonomy.Http3, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/3 MAX_PUSH frame on request stream is rejected", ClientHttp3PreviewTests.MaxPushFrameOnRequestStreamIsRejected, TestTaxonomy.Http3, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/3 stream-level protocol error does not poison connection", ClientHttp3PreviewTests.StreamLevelProtocolErrorDoesNotPoisonConnection, TestTaxonomy.Http3),
    Test("HTTP/3 concurrent stream reset does not leak active streams", ClientHttp3PreviewTests.ConcurrentStreamResetDoesNotLeakActiveStreams, TestTaxonomy.Http3),
    Test("HTTP/3 QPACK decode failure does not reach route selection", ClientHttp3PreviewTests.QpackDecodeFailureDoesNotReachRouteSelection, TestTaxonomy.Http3, TestTaxonomy.Routing, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/3 protocol error budget closes abusive connection", ClientHttp3PreviewTests.ProtocolErrorBudgetClosesAbusiveConnection, TestTaxonomy.Http3),
    Test("HTTP/3 malformed pseudo headers are rejected", Sync(ClientHttp3PreviewTests.MalformedPseudoHeadersAreRejected), TestTaxonomy.Http3, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/3 pseudo header after regular header is rejected", Sync(ClientHttp3PreviewTests.PseudoHeaderAfterRegularHeaderIsRejected), TestTaxonomy.Http3, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/3 forbidden pseudo header is rejected", Sync(ClientHttp3PreviewTests.ForbiddenPseudoHeaderIsRejected), TestTaxonomy.Http3, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/3 missing pseudo headers are rejected", Sync(ClientHttp3PreviewTests.MissingPseudoHeadersAreRejected), TestTaxonomy.Http3, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/3 forbidden connection headers are rejected", Sync(ClientHttp3PreviewTests.ForbiddenConnectionHeadersAreRejected), TestTaxonomy.Http3, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/3 invalid regular header name is rejected", Sync(ClientHttp3PreviewTests.InvalidRegularHeaderNameIsRejected), TestTaxonomy.Http3, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/3 invalid pseudo header values are rejected", Sync(ClientHttp3PreviewTests.InvalidPseudoHeaderValuesAreRejected), TestTaxonomy.Http3, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/3 malformed authority and path are rejected", Sync(ClientHttp3PreviewTests.MalformedAuthorityAndPathAreRejected), TestTaxonomy.Http3, TestTaxonomy.Routing, TestTaxonomy.Admin, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/3 CONNECT pseudo header rules are enforced", Sync(ClientHttp3PreviewTests.ConnectSpecificPseudoHeaderRulesAreEnforced), TestTaxonomy.Http3, TestTaxonomy.Headers),
    Test("HTTP/3 oversized header block is rejected", Sync(ClientHttp3PreviewTests.OversizedHeaderBlockIsRejected), TestTaxonomy.Http3, TestTaxonomy.Headers, TestTaxonomy.Limits, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/3 QPACK header block exact boundary is accepted", Sync(ClientHttp3PreviewTests.QpackHeaderBlockAtExactLimitIsAccepted), TestTaxonomy.Http3, TestTaxonomy.Headers),
    Test("HTTP/3 unsupported QPACK dynamic table usage is rejected", Sync(ClientHttp3PreviewTests.UnsupportedQpackDynamicTableUsageIsRejected), TestTaxonomy.Http3, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/3 invalid QPACK static table reference is rejected", Sync(ClientHttp3PreviewTests.InvalidQpackStaticTableReferenceIsRejected), TestTaxonomy.Http3, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/3 unsupported QPACK dynamic table prefix is rejected", Sync(ClientHttp3PreviewTests.UnsupportedQpackDynamicTablePrefixIsRejected), TestTaxonomy.Http3, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/3 QPACK Huffman static name reference decodes", Sync(ClientHttp3PreviewTests.QpackHuffmanStaticNameReferenceDecodes), TestTaxonomy.Http3, TestTaxonomy.Headers),
    Test("Metrics include HTTP/3 counters", Sync(ClientHttp3PreviewTests.MetricsIncludeHttp3PreviewCounters), TestTaxonomy.Http3, TestTaxonomy.Metrics),
    Test("Config lint reports HTTP/3 default readiness issues", Sync(ClientHttp3PreviewTests.ConfigLintReportsHttp3DefaultReadinessIssues), TestTaxonomy.Http3, TestTaxonomy.Config),
    Test("Resilience disabled preserves existing behavior", ResilienceTests.ExistingBehaviorUnchangedWhenResilienceDisabled, TestTaxonomy.RetryCircuit),
    Test("GET retry occurs on connect failure when enabled", ResilienceTests.GetRetryOccursOnConnectFailureWhenEnabled, TestTaxonomy.RetryCircuit, TestTaxonomy.SecurityNegativePaths),
    Test("GET retry occurs on configured status when enabled", ResilienceTests.GetRetryOccursOnConfiguredStatusWhenEnabled, TestTaxonomy.RetryCircuit),
    Test("POST is not retried by default", ResilienceTests.PostIsNotRetriedByDefault, TestTaxonomy.RetryCircuit, TestTaxonomy.SecurityNegativePaths),
    Test("Upgrade requests are not retried", ResilienceTests.UpgradeIsNotRetried, TestTaxonomy.Http1, TestTaxonomy.RetryCircuit, TestTaxonomy.SecurityNegativePaths),
    Test("Request is not retried after response streaming starts", ResilienceTests.RequestIsNotRetriedAfterResponseStreamingStarts, TestTaxonomy.RetryCircuit, TestTaxonomy.SecurityNegativePaths),
    Test("Partial response failure does not retry second upstream after downstream bytes are sent", ResilienceTests.PartialResponseFailureDoesNotRetrySecondUpstreamAfterDownstreamBytesAreSent, TestTaxonomy.RetryCircuit, TestTaxonomy.SecurityNegativePaths),
    Test("Retry status does not bypass unsafe POST method", ResilienceTests.RetryStatusDoesNotBypassUnsafePostMethod, TestTaxonomy.RetryCircuit, TestTaxonomy.SecurityNegativePaths),
    Test("Retry maxAttempts is enforced", ResilienceTests.RetryMaxAttemptsIsEnforced, TestTaxonomy.RetryCircuit),
    Test("Retry exhausted returns clear failure", ResilienceTests.RetryExhaustedReturnsClearFailure, TestTaxonomy.RetryCircuit, TestTaxonomy.SecurityNegativePaths),
    Test("Circuit opens after threshold failures", Sync(ResilienceTests.CircuitOpensAfterThresholdFailures), TestTaxonomy.RetryCircuit, TestTaxonomy.SecurityNegativePaths),
    Test("Circuit rejects traffic while open", Sync(ResilienceTests.CircuitRejectsTrafficWhileOpen), TestTaxonomy.RetryCircuit, TestTaxonomy.SecurityNegativePaths),
    Test("Circuit transitions to half-open after open duration", Sync(ResilienceTests.CircuitTransitionsToHalfOpenAfterOpenDuration), TestTaxonomy.RetryCircuit),
    Test("Circuit half-open probe count is bounded", Sync(ResilienceTests.HalfOpenProbeCountIsBounded), TestTaxonomy.RetryCircuit, TestTaxonomy.Limits),
    Test("Half-open success closes circuit", Sync(ResilienceTests.HalfOpenSuccessClosesCircuit), TestTaxonomy.RetryCircuit),
    Test("Half-open failure reopens circuit", Sync(ResilienceTests.HalfOpenFailureReopensCircuit), TestTaxonomy.RetryCircuit, TestTaxonomy.SecurityNegativePaths),
    Test("Weighted round-robin honors weights", Sync(ResilienceTests.WeightedRoundRobinHonorsWeights), TestTaxonomy.RetryCircuit),
    Test("Equal weights preserve round-robin order", Sync(ResilienceTests.EqualWeightRoundRobinPreservesExistingOrder), TestTaxonomy.RetryCircuit),
    Test("Unhealthy and open-circuit upstreams are skipped", Sync(ResilienceTests.UnhealthyAndOpenCircuitUpstreamsAreSkipped), TestTaxonomy.RetryCircuit, TestTaxonomy.HealthChecks, TestTaxonomy.SecurityNegativePaths),
    Test("Mixed protocol upstream failures isolate circuit state", Sync(ResilienceTests.MixedProtocolUpstreamFailuresIsolateCircuitState), TestTaxonomy.RetryCircuit, TestTaxonomy.SecurityNegativePaths),
    Test("All unavailable upstreams return no selection", Sync(ResilienceTests.AllUpstreamsUnavailableReturnsNoSelection), TestTaxonomy.Routing, TestTaxonomy.RetryCircuit, TestTaxonomy.SecurityNegativePaths),
    Test("All unavailable upstreams return safe failure", ResilienceTests.AllUnavailableUpstreamsReturnSafeFailure, TestTaxonomy.RetryCircuit, TestTaxonomy.SecurityNegativePaths),
    Test("Metrics include retry circuit and balancing counters", Sync(ResilienceTests.MetricsIncludeRetryCircuitAndBalancingCounters), TestTaxonomy.RetryCircuit, TestTaxonomy.Metrics),
    Test("Effective and status projections show resilience state", Sync(ResilienceTests.EffectiveAndStatusProjectionsShowSafeResilienceState), TestTaxonomy.Config, TestTaxonomy.RetryCircuit),
    Test("Host startup succeeds from fresh data directory", StartupSmokeTests.StartsFromFreshDataDirectory, TestTaxonomy.Config, TestTaxonomy.Headers),
    Test("Host startup fails when existing site config is invalid", StartupSmokeTests.FailsStartupWhenExistingSiteConfigIsInvalid, TestTaxonomy.Config, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("Host startup succeeds with valid site config", StartupSmokeTests.StartsWithValidSiteConfig, TestTaxonomy.Config, TestTaxonomy.Headers),
    Test("Proxy dataplane proxies one GET request end to end", ProxyIntegrationTests.ProxiesSingleGetToUpstream, TestTaxonomy.Http1),
    Test("Proxy dataplane proxies fixed-length request and response", ProxyIntegrationTests.ProxiesFixedLengthRequestAndResponse, TestTaxonomy.Http1),
    Test("Proxy dataplane proxies chunked request and response", ProxyIntegrationTests.ProxiesChunkedRequestAndResponse, TestTaxonomy.Http1, TestTaxonomy.Headers),
    Test("HTTP/1.1 chunk extensions are accepted and forwarded", ProxyIntegrationTests.AcceptsChunkExtensionsAndForwardsChunkedBody, TestTaxonomy.Http1, TestTaxonomy.Headers),
    Test("HTTP/1.1 declared chunked request trailer is forwarded", ProxyIntegrationTests.ForwardsDeclaredChunkedRequestTrailer, TestTaxonomy.Http1, TestTaxonomy.Headers),
    Test("Proxy dataplane does not relay HEAD response body", ProxyIntegrationTests.DoesNotRelayHeadResponseBody, TestTaxonomy.Http1),
    Test("Proxy dataplane proxies 204 without response body", ProxyIntegrationTests.ProxiesNoContentWithoutBody, TestTaxonomy.Http1),
    Test("Proxy dataplane proxies 304 without response body", ProxyIntegrationTests.ProxiesNotModifiedWithoutBody, TestTaxonomy.Http1),
    Test("Proxy dataplane rejects invalid request framing", ProxyIntegrationTests.RejectsInvalidRequestFraming, TestTaxonomy.Http1, TestTaxonomy.SecurityNegativePaths),
    Test("Proxy dataplane rejects malformed chunked request body", ProxyIntegrationTests.RejectsMalformedChunkedRequestBody, TestTaxonomy.Http1, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("Proxy dataplane filters hop-by-hop request headers", ProxyIntegrationTests.FiltersHopByHopRequestHeaders, TestTaxonomy.Http1, TestTaxonomy.Headers),
    Test("Proxy dataplane preserves Host header", ProxyIntegrationTests.PreservesHostHeader, TestTaxonomy.Http1, TestTaxonomy.Headers),
    Test("Proxy dataplane emits generated response request ID", ProxyIntegrationTests.ResponseIncludesGeneratedRequestId, TestTaxonomy.Http1),
    Test("Proxy dataplane preserves external request ID in diagnostics", ProxyIntegrationTests.ExternalRequestIdIsPreservedInDiagnostics, TestTaxonomy.Http1, TestTaxonomy.Metrics),
    Test("Proxy dataplane records successful diagnostics", ProxyIntegrationTests.SuccessfulRequestProducesDiagnosticRouteAndUpstream, TestTaxonomy.Http1, TestTaxonomy.Metrics),
    Test("Proxy dataplane records upstream connect failure diagnostics", ProxyIntegrationTests.UpstreamConnectFailureProducesDiagnosticClassification, TestTaxonomy.Http1, TestTaxonomy.UpstreamHttp1, TestTaxonomy.Metrics, TestTaxonomy.SecurityNegativePaths),
    Test("Proxy dataplane can disable access logs while keeping diagnostics", ProxyIntegrationTests.AccessLoggingCanBeDisabledWhileDiagnosticsRemainEnabled, TestTaxonomy.Http1, TestTaxonomy.Metrics),
    Test("HTTP to HTTPS redirect preserves path and query", ProxyIntegrationTests.HttpToHttpsRedirectPreservesPathAndQuery, TestTaxonomy.Routing, TestTaxonomy.Tls),
    Test("Canonical host redirect works", ProxyIntegrationTests.CanonicalHostRedirectWorks, TestTaxonomy.Routing, TestTaxonomy.Headers),
    Test("Canonical host redirect does not loop", ProxyIntegrationTests.CanonicalHostRedirectDoesNotLoop, TestTaxonomy.Routing, TestTaxonomy.Headers),
    Test("Forwarded headers generated for untrusted direct client", ProxyIntegrationTests.ForwardedHeadersGeneratedForUntrustedDirectClient, TestTaxonomy.Headers),
    Test("Trusted proxy accepts prior forwarded chain", ProxyIntegrationTests.TrustedProxyAcceptsPriorForwardedChain, TestTaxonomy.Config),
    Test("Malformed trusted Forwarded headers are sanitized before upstream", ProxyIntegrationTests.MalformedTrustedForwardedHeadersAreSanitizedBeforeUpstream, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("Untrusted client forwarded headers are stripped and replaced", ProxyIntegrationTests.UntrustedClientForwardedHeadersAreStrippedAndReplaced, TestTaxonomy.Headers),
    Test("Request header set/remove rules apply upstream", ProxyIntegrationTests.RequestHeaderSetAndRemoveRulesApplyUpstream, TestTaxonomy.Headers),
    Test("Response header set/remove rules apply downstream", ProxyIntegrationTests.ResponseHeaderSetAndRemoveRulesApplyDownstream, TestTaxonomy.Headers),
    Test("Path prefix stripping preserves query string", ProxyIntegrationTests.PathPrefixStrippingPreservesQueryString, TestTaxonomy.Routing),
    Test("Path prefix replacement works", ProxyIntegrationTests.PathPrefixReplacementWorks, TestTaxonomy.Routing),
    Test("Path rewrite no-match forwards original target", ProxyIntegrationTests.PathRewriteNoMatchForwardsOriginalTarget, TestTaxonomy.Routing),
    Test("Redirect route returns configured redirect", ProxyIntegrationTests.RedirectRouteReturnsConfiguredRedirect, TestTaxonomy.Routing),
    Test("Static response route returns configured response", ProxyIntegrationTests.StaticResponseRouteReturnsConfiguredResponse, TestTaxonomy.Routing),
    Test("Maintenance mode returns 503 and does not contact upstream", ProxyIntegrationTests.MaintenanceModeReturns503AndDoesNotContactUpstream, TestTaxonomy.Routing),
    Test("Per-route body-size override works", ProxyIntegrationTests.PerRouteBodySizeOverrideWorks, TestTaxonomy.Routing),
    Test("Per-route access-log disable is reflected in diagnostics", ProxyIntegrationTests.PerRouteAccessLogDisableIsReflectedInDiagnostics, TestTaxonomy.Routing, TestTaxonomy.Metrics),
    Test("Proxy dataplane records no-route diagnostics and status summary", ProxyIntegrationTests.NoMatchingRouteProducesDiagnosticClassification, TestTaxonomy.Http1, TestTaxonomy.Routing, TestTaxonomy.Metrics),
    Test("Failed reload while proxy active preserves old snapshot and traffic", ProxyIntegrationTests.FailedReloadWhileProxyActivePreservesOldSnapshotAndTraffic, TestTaxonomy.SecurityNegativePaths),
    Test("Proxy dataplane rejects oversized request head", ProxyIntegrationTests.OversizedRequestHeadIsRejected, TestTaxonomy.Http1, TestTaxonomy.Limits, TestTaxonomy.SecurityNegativePaths),
    Test("Proxy dataplane rejects excessive header count", ProxyIntegrationTests.ExcessiveHeaderCountIsRejected, TestTaxonomy.Http1, TestTaxonomy.Headers, TestTaxonomy.Limits, TestTaxonomy.SecurityNegativePaths),
    Test("Proxy dataplane rejects excessive header line", ProxyIntegrationTests.ExcessiveHeaderLineIsRejected, TestTaxonomy.Http1, TestTaxonomy.Headers, TestTaxonomy.Limits, TestTaxonomy.SecurityNegativePaths),
    Test("Proxy dataplane rejects excessive request body size", ProxyIntegrationTests.ExcessiveRequestBodySizeIsRejected, TestTaxonomy.Http1, TestTaxonomy.Limits, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/1.1 request body exactly at configured limit is accepted", ProxyIntegrationTests.RequestBodyExactlyAtConfiguredMaxIsAccepted, TestTaxonomy.Http1, TestTaxonomy.Limits),
    Test("HTTP/1.1 chunked request body exactly at configured limit is accepted", ProxyIntegrationTests.ChunkedRequestBodyExactlyAtConfiguredMaxIsAccepted, TestTaxonomy.Http1, TestTaxonomy.Headers, TestTaxonomy.Limits),
    Test("HTTP/1.1 request body configured limit plus one is rejected", ProxyIntegrationTests.RequestBodyConfiguredMaxPlusOneIsRejected, TestTaxonomy.Http1, TestTaxonomy.Limits, TestTaxonomy.SecurityNegativePaths),
    Test("Proxy dataplane rejects oversized chunked request body", ProxyIntegrationTests.ChunkedRequestBodySizeIsRejected, TestTaxonomy.Http1, TestTaxonomy.Headers, TestTaxonomy.Limits, TestTaxonomy.SecurityNegativePaths),
    Test("Proxy dataplane enforces per-IP request rate limit", ProxyIntegrationTests.PerIpRequestRateLimitIsEnforced, TestTaxonomy.Http1, TestTaxonomy.Limits),
    Test("Concurrent client admission limit rejects limit plus one and recovers", ProxyIntegrationTests.ConcurrentClientAdmissionLimitRejectsLimitPlusOneAndRecovers, TestTaxonomy.Http1, TestTaxonomy.Limits, TestTaxonomy.SecurityNegativePaths),
    Test("Concurrent per-IP rate limit allows only configured boundary", ProxyIntegrationTests.ConcurrentPerIpRateLimitAllowsOnlyConfiguredBoundary, TestTaxonomy.Http1, TestTaxonomy.Limits),
    Test("Proxy dataplane times out incomplete request head", ProxyIntegrationTests.TimesOutIncompleteRequestHead, TestTaxonomy.Http1),
    Test("Proxy dataplane times out incomplete Content-Length request body", ProxyIntegrationTests.TimesOutIncompleteContentLengthRequestBody, TestTaxonomy.Http1, TestTaxonomy.Headers),
    Test("Proxy dataplane times out incomplete chunked request body", ProxyIntegrationTests.TimesOutIncompleteChunkedRequestBody, TestTaxonomy.Http1, TestTaxonomy.Headers),
    Test("HTTP/1.1 missing terminating chunk times out safely", ProxyIntegrationTests.TimesOutMissingTerminatingChunkAfterCompleteChunk, TestTaxonomy.Http1, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("Proxy dataplane maps unavailable upstream to 502", ProxyIntegrationTests.UnavailableUpstreamProducesBadGateway, TestTaxonomy.Http1, TestTaxonomy.RetryCircuit, TestTaxonomy.SecurityNegativePaths),
    Test("Proxy dataplane maps upstream response-head timeout to 504", ProxyIntegrationTests.UpstreamResponseHeadTimeoutProducesGatewayTimeout, TestTaxonomy.Http1, TestTaxonomy.RetryCircuit, TestTaxonomy.Limits, TestTaxonomy.SecurityNegativePaths),
    Test("Proxy dataplane closes after started response on upstream early close", ProxyIntegrationTests.UpstreamContentLengthEarlyCloseClosesAfterStartedResponse, TestTaxonomy.Http1, TestTaxonomy.UpstreamHttp1),
    Test("HTTP/1.1 upstream chunked early close is contained", ProxyIntegrationTests.UpstreamChunkedEarlyCloseClosesAfterStartedResponse, TestTaxonomy.Http1, TestTaxonomy.Headers),
    Test("HTTPS listener proxies GET to upstream", ProxyIntegrationTests.HttpsListenerProxiesGetToUpstream, TestTaxonomy.Tls),
    Test("HTTPS listener selects certificate by SNI", ProxyIntegrationTests.HttpsListenerSelectsCertificateBySni, TestTaxonomy.Tls),
    Test("HTTPS listener selects certificate by case-insensitive SNI", ProxyIntegrationTests.HttpsListenerSelectsCertificateByCaseInsensitiveSni, TestTaxonomy.Tls),
    Test("HTTPS listener uses default certificate for unmatched SNI", ProxyIntegrationTests.HttpsListenerUsesDefaultCertificateForUnmatchedSni, TestTaxonomy.Tls),
    Test("HTTPS listener uses default certificate without SNI", ProxyIntegrationTests.HttpsListenerUsesDefaultCertificateWithoutSni, TestTaxonomy.Tls),
    Test("HTTPS listener fails handshake when no certificate matches", ProxyIntegrationTests.HttpsListenerFailsHandshakeWhenNoCertificateMatches, TestTaxonomy.Tls, TestTaxonomy.SecurityNegativePaths),
    Test("HTTPS listener times out incomplete TLS handshake", ProxyIntegrationTests.HttpsListenerTimesOutIncompleteTlsHandshake, TestTaxonomy.Tls),
    Test("Persistent client processes two sequential GETs and reuses upstream", ProxyIntegrationTests.PersistentClientProcessesTwoSequentialGetsAndReusesUpstream, TestTaxonomy.Http1),
    Test("Client Connection close header closes after response", ProxyIntegrationTests.ClientConnectionCloseHeaderClosesAfterResponse, TestTaxonomy.Http1, TestTaxonomy.Headers),
    Test("HTTP/1.0 client closes by default", ProxyIntegrationTests.Http10ClientClosesByDefault, TestTaxonomy.Http1),
    Test("Max requests per client connection is enforced", ProxyIntegrationTests.MaxRequestsPerClientConnectionIsEnforced, TestTaxonomy.Limits),
    Test("Client keep-alive idle timeout closes connection", ProxyIntegrationTests.ClientKeepAliveIdleTimeoutClosesConnection, TestTaxonomy.Http1, TestTaxonomy.Limits, TestTaxonomy.SecurityNegativePaths),
    Test("Malformed second request closes connection", ProxyIntegrationTests.MalformedSecondRequestClosesConnection, TestTaxonomy.Http1, TestTaxonomy.SecurityNegativePaths),
    Test("HTTP/1.1 pipelined valid then malformed request does not reach upstream twice", ProxyIntegrationTests.PipelinedValidThenMalformedRequestDoesNotReachUpstreamTwice, TestTaxonomy.Http1, TestTaxonomy.SecurityNegativePaths),
    Test("Persistent client proxies Content-Length POST", ProxyIntegrationTests.PersistentClientProxiesContentLengthPost, TestTaxonomy.Http1, TestTaxonomy.Headers),
    Test("Persistent client proxies chunked POST", ProxyIntegrationTests.PersistentClientProxiesChunkedPost, TestTaxonomy.Http1, TestTaxonomy.Headers),
    Test("Upstream connection is not reused after response Connection close", ProxyIntegrationTests.UpstreamConnectionIsNotReusedAfterResponseConnectionClose, TestTaxonomy.UpstreamHttp1, TestTaxonomy.Headers),
    Test("Upstream connection is not reused after premature disconnect", ProxyIntegrationTests.UpstreamConnectionIsNotReusedAfterPrematureDisconnect, TestTaxonomy.UpstreamHttp1),
    Test("Upstream connection is not reused after framing error", ProxyIntegrationTests.UpstreamConnectionIsNotReusedAfterFramingError, TestTaxonomy.UpstreamHttp1),
    Test("WebSocket Upgrade over plaintext returns 101", ProxyIntegrationTests.WebSocketUpgradeOverPlaintextReturnsSwitchingProtocols, TestTaxonomy.Http1),
    Test("WebSocket Upgrade produces tunnel diagnostic", ProxyIntegrationTests.WebSocketUpgradeProducesTunnelDiagnostic, TestTaxonomy.Http1, TestTaxonomy.Metrics),
    Test("WebSocket tunnel relays client bytes upstream", ProxyIntegrationTests.WebSocketTunnelRelaysClientBytesToUpstream, TestTaxonomy.Http1),
    Test("WebSocket tunnel relays upstream bytes client", ProxyIntegrationTests.WebSocketTunnelRelaysUpstreamBytesToClient, TestTaxonomy.Http1),
    Test("WebSocket tunnel closes when client closes", ProxyIntegrationTests.WebSocketTunnelClosesWhenClientCloses, TestTaxonomy.Http1),
    Test("WebSocket tunnel closes when upstream closes", ProxyIntegrationTests.WebSocketTunnelClosesWhenUpstreamCloses, TestTaxonomy.Http1),
    Test("WebSocket tunnel idle timeout closes tunnel", ProxyIntegrationTests.WebSocketTunnelIdleTimeoutClosesTunnel, TestTaxonomy.Http1, TestTaxonomy.Limits, TestTaxonomy.SecurityNegativePaths),
    Test("WebSocket Upgrade over HTTPS returns 101", ProxyIntegrationTests.WebSocketUpgradeOverHttpsReturnsSwitchingProtocols, TestTaxonomy.Http1, TestTaxonomy.Tls),
    Test("Upgrade does not use normal upstream pool", ProxyIntegrationTests.UpgradeDoesNotUseNormalUpstreamPool, TestTaxonomy.Http1, TestTaxonomy.UpstreamHttp1),
    Test("Missing WebSocket headers are rejected", ProxyIntegrationTests.MissingWebSocketHeadersAreRejected, TestTaxonomy.Http1, TestTaxonomy.Headers, TestTaxonomy.SecurityNegativePaths),
    Test("Upstream non-101 Upgrade response is forwarded and closed", ProxyIntegrationTests.UpstreamNon101UpgradeResponseIsForwardedAndClosed, TestTaxonomy.Http1),
    Test("Malformed 101 Upgrade response produces bad gateway", ProxyIntegrationTests.MalformedSwitchingProtocolsResponseProducesBadGateway, TestTaxonomy.Http1, TestTaxonomy.SecurityNegativePaths),
    Test("Health check 2xx response is healthy", HealthCheckTests.HealthCheck2xxIsHealthy, TestTaxonomy.HealthChecks),
    Test("Health check 3xx response is healthy", HealthCheckTests.HealthCheck3xxIsHealthy, TestTaxonomy.HealthChecks),
    Test("Health check 4xx response is unhealthy", HealthCheckTests.HealthCheck4xxIsUnhealthy, TestTaxonomy.HealthChecks, TestTaxonomy.SecurityNegativePaths),
    Test("Health check 5xx response is unhealthy", HealthCheckTests.HealthCheck5xxIsUnhealthy, TestTaxonomy.HealthChecks, TestTaxonomy.SecurityNegativePaths),
    Test("Health check timeout is unhealthy", HealthCheckTests.HealthCheckTimeoutIsUnhealthy, TestTaxonomy.HealthChecks, TestTaxonomy.Limits, TestTaxonomy.SecurityNegativePaths),
    Test("Health state transitions to unhealthy after threshold", Sync(HealthCheckTests.HealthStateTransitionsToUnhealthyAfterThreshold), TestTaxonomy.HealthChecks, TestTaxonomy.SecurityNegativePaths),
    Test("Health state transitions to healthy after recovery threshold", Sync(HealthCheckTests.HealthStateTransitionsToHealthyAfterRecoveryThreshold), TestTaxonomy.HealthChecks),
    Test("Round-robin distributes sequential requests across two upstreams", ProxyIntegrationTests.RoundRobinDistributesSequentialRequestsAcrossTwoUpstreams, TestTaxonomy.Routing),
    Test("Unhealthy upstream is not selected", ProxyIntegrationTests.UnhealthyUpstreamIsNotSelected, TestTaxonomy.HealthChecks, TestTaxonomy.SecurityNegativePaths),
    Test("All unhealthy upstreams return service unavailable", ProxyIntegrationTests.AllUnhealthyUpstreamsReturnServiceUnavailable, TestTaxonomy.RetryCircuit, TestTaxonomy.HealthChecks, TestTaxonomy.SecurityNegativePaths),
    Test("WebSocket Upgrade uses round-robin upstream selection", ProxyIntegrationTests.WebSocketUpgradeUsesRoundRobinUpstreamSelection, TestTaxonomy.Http1, TestTaxonomy.Routing),
    Test("Upstream pool uses distinct endpoint keys", ProxyIntegrationTests.UpstreamPoolUsesDistinctEndpointKeys, TestTaxonomy.UpstreamHttp1),
    Test("Recent diagnostics store is bounded", Sync(ObservabilityTests.RecentDiagnosticsStoreIsBounded), TestTaxonomy.Limits, TestTaxonomy.Metrics),
    Test("Diagnostics controller honors safe limit", Sync(ObservabilityTests.DiagnosticsControllerHonorsSafeLimit), TestTaxonomy.Limits, TestTaxonomy.Metrics),
    Test("Diagnostics event omits bodies and secrets", Sync(ObservabilityTests.DiagnosticsEventDoesNotCarryBodiesOrSecrets), TestTaxonomy.Metrics),
    Test("Admission controller enforces client limit", Sync(HardeningTests.AdmissionControllerEnforcesClientLimit), TestTaxonomy.Limits),
    Test("Admission lease disposal releases client slot", Sync(HardeningTests.AdmissionLeaseDisposalReleasesClientSlot), TestTaxonomy.Limits),
    Test("Admission controller enforces TLS handshake limit", Sync(HardeningTests.AdmissionControllerEnforcesTlsHandshakeLimit), TestTaxonomy.Tls, TestTaxonomy.Limits),
    Test("Rate limiter enforces request limit and refill", Sync(HardeningTests.RateLimiterEnforcesRequestLimitAndRefills), TestTaxonomy.Limits),
    Test("Concurrent rate limiter boundary allows only configured limit", Sync(HardeningTests.ConcurrentRateLimiterBoundaryAllowsOnlyConfiguredLimit), TestTaxonomy.Limits),
    Test("Rate limiter enforces upgrade limit", Sync(HardeningTests.RateLimiterEnforcesUpgradeLimit), TestTaxonomy.Limits),
    Test("Rate limiter cleans stale entries", Sync(HardeningTests.RateLimiterCleansStaleEntries), TestTaxonomy.Limits),
    Test("Shutdown coordinator exposes grace deadline", Sync(HardeningTests.ShutdownCoordinatorExposesGraceDeadlineAndCancels), TestTaxonomy.Limits)
};

TestRunOptions options;
try
{
    options = TestRunOptions.Parse(args);
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine("Use --list-categories to see supported categories.");
    Environment.ExitCode = 2;
    return;
}

if (options.ListCategories)
{
    foreach (var category in TestTaxonomy.Categories)
    {
        var count = tests.Count(test => test.Categories.Contains(category));
        Console.WriteLine($"{category} {count}");
    }

    return;
}

if (options.CheckMetadata)
{
    var metadataErrors = TestMetadataIntegrity.Validate(tests);
    if (metadataErrors.Count > 0)
    {
        Console.Error.WriteLine("Test metadata integrity check failed.");
        foreach (var error in metadataErrors)
        {
            Console.Error.WriteLine(error);
        }

        Environment.ExitCode = 1;
        return;
    }

    Console.WriteLine("Test metadata integrity check passed.");
    foreach (var category in TestTaxonomy.Categories)
    {
        var count = tests.Count(test => test.Categories.Contains(category));
        Console.WriteLine($"{category} {count}");
    }

    return;
}

var selectedTests = options.Categories.Count == 0
    ? tests
    : tests.Where(test => test.Categories.Any(options.Categories.Contains)).ToArray();

if (selectedTests.Length == 0)
{
    Console.Error.WriteLine($"No tests matched categories: {string.Join(", ", options.Categories)}");
    Environment.ExitCode = 2;
    return;
}

if (options.Categories.Count > 0)
{
    Console.WriteLine($"Running {selectedTests.Length} of {tests.Length} tests for categories: {string.Join(", ", options.Categories)}.");
}

var failures = 0;
List<string> failureNames = [];

foreach (var test in selectedTests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failures++;
        failureNames.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine(exception);
    }
}

WriteCorrectnessSummary(options, tests.Length, selectedTests.Length, failures, failureNames);

if (failures > 0)
{
    Environment.ExitCode = 1;
    return;
}

Console.WriteLine($"Passed {selectedTests.Length} tests.");

static TestCase Test(string name, Func<Task> run, params string[] categories)
{
    return new TestCase(name, run, TestTaxonomy.CanonicalCategories(categories));
}

static Func<Task> Sync(Action test)
{
    return () =>
    {
        test();
        return Task.CompletedTask;
    };
}

static void WriteCorrectnessSummary(
    TestRunOptions options,
    int totalTests,
    int selectedTests,
    int failures,
    IReadOnlyList<string> failureNames)
{
    if (string.IsNullOrWhiteSpace(options.SummaryFile))
    {
        return;
    }

    var directory = Path.GetDirectoryName(options.SummaryFile);
    if (!string.IsNullOrWhiteSpace(directory))
    {
        Directory.CreateDirectory(directory);
    }

    var summary = new
    {
        kind = "correctness",
        status = failures == 0 ? "passed" : "failed",
        categories = options.Categories,
        totalTests,
        selectedTests,
        passedTests = selectedTests - failures,
        failedTests = failures,
        failures = failureNames
    };

    File.WriteAllText(
        options.SummaryFile,
        JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
}

internal sealed record TestCase(string Name, Func<Task> Run, IReadOnlySet<string> Categories);

internal sealed record TestRunOptions(IReadOnlySet<string> Categories, bool ListCategories, bool CheckMetadata, string? SummaryFile)
{
    public static TestRunOptions Parse(string[] args)
    {
        HashSet<string> categories = new(StringComparer.OrdinalIgnoreCase);
        var listCategories = false;
        var checkMetadata = false;
        string? summaryFile = null;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (string.Equals(arg, "--list-categories", StringComparison.OrdinalIgnoreCase))
            {
                listCategories = true;
                continue;
            }

            if (string.Equals(arg, "--check-test-metadata", StringComparison.OrdinalIgnoreCase))
            {
                checkMetadata = true;
                continue;
            }

            if (string.Equals(arg, "--summary-file", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException($"{arg} requires a file path.");
                }

                summaryFile = args[++index];
                continue;
            }

            if (string.Equals(arg, "--category", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "--categories", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException($"{arg} requires a category value.");
                }

                AddCategories(args[++index], categories);
                continue;
            }

            const string categoryPrefix = "--category=";
            const string categoriesPrefix = "--categories=";
            const string summaryFilePrefix = "--summary-file=";
            if (arg.StartsWith(categoryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                AddCategories(arg[categoryPrefix.Length..], categories);
                continue;
            }

            if (arg.StartsWith(categoriesPrefix, StringComparison.OrdinalIgnoreCase))
            {
                AddCategories(arg[categoriesPrefix.Length..], categories);
                continue;
            }

            if (arg.StartsWith(summaryFilePrefix, StringComparison.OrdinalIgnoreCase))
            {
                summaryFile = arg[summaryFilePrefix.Length..];
                continue;
            }

            throw new ArgumentException($"Unknown test runner argument: {arg}");
        }

        var canonical = categories
            .Select(TestTaxonomy.CanonicalCategory)
            .OrderBy(static category => category, StringComparer.Ordinal)
            .ToArray();
        return new TestRunOptions(canonical.ToHashSet(StringComparer.Ordinal), listCategories, checkMetadata, summaryFile);
    }

    private static void AddCategories(string value, HashSet<string> categories)
    {
        foreach (var category in value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TestTaxonomy.IsKnownCategory(category))
            {
                throw new ArgumentException($"Unknown test category: {category}");
            }

            categories.Add(category);
        }
    }
}

internal static class TestTaxonomy
{
    public const string Http1 = "HTTP1";
    public const string Http2 = "HTTP2";
    public const string Http3 = "HTTP3";
    public const string UpstreamHttp1 = "UpstreamHTTP1";
    public const string UpstreamHttp2 = "UpstreamHTTP2";
    public const string UpstreamHttp3 = "UpstreamHTTP3";
    public const string Config = "Config";
    public const string Routing = "Routing";
    public const string Tls = "TLS";
    public const string Headers = "Headers";
    public const string Caching = "Caching";
    public const string RetryCircuit = "RetryCircuit";
    public const string HealthChecks = "HealthChecks";
    public const string Limits = "Limits";
    public const string Admin = "Admin";
    public const string Metrics = "Metrics";
    public const string SecurityNegativePaths = "SecurityNegativePaths";

    public static readonly string[] Categories =
    [
        Http1,
        Http2,
        Http3,
        UpstreamHttp1,
        UpstreamHttp2,
        UpstreamHttp3,
        Config,
        Routing,
        Tls,
        Headers,
        Caching,
        RetryCircuit,
        HealthChecks,
        Limits,
        Admin,
        Metrics,
        SecurityNegativePaths
    ];

    private static readonly Dictionary<string, string> CategoryLookup = Categories.ToDictionary(
        static category => category,
        static category => category,
        StringComparer.OrdinalIgnoreCase);

    public static bool IsKnownCategory(string category)
    {
        return CategoryLookup.ContainsKey(category);
    }

    public static string CanonicalCategory(string category)
    {
        return CategoryLookup.TryGetValue(category, out var canonical)
            ? canonical
            : throw new ArgumentException($"Unknown test category: {category}");
    }

    public static IReadOnlySet<string> CanonicalCategories(params string[] categories)
    {
        HashSet<string> canonical = new(StringComparer.Ordinal);
        foreach (var category in categories)
        {
            canonical.Add(CanonicalCategory(category));
        }

        if (canonical.Count == 0)
        {
            throw new ArgumentException("Each test registration must declare at least one correctness category.");
        }

        return canonical;
    }
}

internal static class TestMetadataIntegrity
{
    public static IReadOnlyList<string> Validate(IReadOnlyList<TestCase> tests)
    {
        List<string> errors = [];

        foreach (var test in tests)
        {
            if (test.Categories.Count == 0)
            {
                errors.Add($"Test has no correctness category: {test.Name}");
            }

            foreach (var category in test.Categories)
            {
                if (!TestTaxonomy.IsKnownCategory(category))
                {
                    errors.Add($"Test uses unknown correctness category '{category}': {test.Name}");
                }
            }
        }

        foreach (var category in TestTaxonomy.Categories)
        {
            if (!tests.Any(test => test.Categories.Contains(category)))
            {
                errors.Add($"Correctness category has zero tests: {category}");
            }
        }

        var duplicateNames = tests
            .GroupBy(static test => test.Name, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key);
        foreach (var name in duplicateNames)
        {
            errors.Add($"Duplicate test name: {name}");
        }

        return errors;
    }
}

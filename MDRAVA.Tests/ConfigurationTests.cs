using System.Security.Cryptography.X509Certificates;
using MDRAVA.API.Controllers;
using MDRAVA.INF.Configuration;
using MDRAVA.INF.Configuration.Loading;
using MDRAVA.INF.Configuration.Paths;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace MDRAVA.Tests;

internal static class ConfigurationTests
{
    public static void ConfigurationFileErrorNamesGlobalAndPathErrors()
    {
        var global = ProxyConfigurationFileError.Global("global failure");
        var path = ProxyConfigurationFileError.ForPath("config/site.json", "path failure");

        AssertEx.Equal<string?>(null, global.Path);
        AssertEx.Equal("global failure", global.Message);
        AssertEx.Equal("config/site.json", path.Path);
        AssertEx.Equal("path failure", path.Message);
    }

    public static void NormalizeSiteParseResultNamesParsedAndFailedStates()
    {
        var site = new SiteOptions { Name = "parsed" };
        var parsed = ProxyConfigurationNormalizeSiteParseResult.Parsed(site, "{}");
        var failed = ProxyConfigurationNormalizeSiteParseResult.Failed("parse failed");

        AssertEx.True(parsed is ProxyConfigurationNormalizeSiteParseResult.ParsedResult);
        var parsedResult = (ProxyConfigurationNormalizeSiteParseResult.ParsedResult)parsed;
        AssertEx.Equal(site, parsedResult.Site);
        AssertEx.Equal("{}", parsedResult.CanonicalJson);
        AssertEx.True(failed is ProxyConfigurationNormalizeSiteParseResult.FailedResult);
        AssertEx.Equal("parse failed", ((ProxyConfigurationNormalizeSiteParseResult.FailedResult)failed).Error);
    }

    public static void SiteConfigurationSourceFactoriesOwnPathsAndValidateInputs()
    {
        var site = new SiteOptions { Name = "main" };
        var file = SiteConfigurationSource.FromFile("sites/main.json", site);
        var normalize = SiteConfigurationSource.FromNormalizeInput(site);
        var lint = SiteConfigurationSource.FromLintInput(site);

        AssertEx.Equal("sites/main.json", file.Path);
        AssertEx.Equal(site, file.Site);
        AssertEx.Equal(SiteConfigurationSource.NormalizeInputPath, normalize.Path);
        AssertEx.Equal(site, normalize.Site);
        AssertEx.Equal(SiteConfigurationSource.LintInputPath, lint.Path);
        AssertEx.Equal(site, lint.Site);
        AssertEx.Throws<ArgumentException>(() => SiteConfigurationSource.FromFile(" ", site));
        AssertEx.Throws<ArgumentNullException>(() => SiteConfigurationSource.FromFile("sites/main.json", null!));
        AssertEx.Throws<ArgumentNullException>(() => SiteConfigurationSource.FromNormalizeInput(null!));
        AssertEx.Throws<ArgumentNullException>(() => SiteConfigurationSource.FromLintInput(null!));
    }

    public static void ConfigurationNormalizeResultNamesNormalizedAndFailedOutcomes()
    {
        var normalized = ProxyConfigurationNormalizeResult.Normalized("json", "{}");
        var failed = ProxyConfigurationNormalizeResult.Failed(
            "yaml",
            [ProxyConfigurationFileError.Global("parse failed")]);

        AssertEx.True(normalized is ProxyConfigurationNormalizeResult.NormalizedResult);
        AssertEx.Equal("json", normalized.Format);
        AssertEx.Equal("{}", ((ProxyConfigurationNormalizeResult.NormalizedResult)normalized).CanonicalJson);
        AssertEx.Equal(0, normalized.Errors.Count);
        AssertEx.Equal(0, normalized.FileErrors.Count);
        AssertEx.True(failed is ProxyConfigurationNormalizeResult.FailedResult);
        AssertEx.Equal("yaml", failed.Format);
        AssertEx.Equal("parse failed", failed.Errors[0]);
        AssertEx.Equal("parse failed", failed.FileErrors[0].Message);
    }

    public static void ConfigurationReloadResultNamesReloadOutcomes()
    {
        var attemptedAtUtc = DateTimeOffset.UnixEpoch.AddMinutes(1);
        var loadedAtUtc = DateTimeOffset.UnixEpoch.AddMinutes(2);
        var discovery = new ProxyConfigurationDiscovery(
            new ProxyFilesystemLayout("tests", "tests/config", "tests/config/sites", "tests/logs", "tests/certs", "tests/state", "tests/config/proxy.json"),
            [],
            [],
            []);
        var active = new TestConfigurationProjection("active");
        var listenerReload = ProxyListenerReloadResult.Applied(
            attemptedAtUtc,
            added: 1,
            removed: 0,
            changed: 0,
            unchanged: 0,
            changes: [],
            errors: []);
        var failedListenerReload = ProxyListenerReloadResult.Failed(
            attemptedAtUtc,
            added: 1,
            removed: 0,
            changed: 0,
            unchanged: 0,
            changes: [],
            errors: ["listener failed"]);

        AssertEx.True(listenerReload is ProxyListenerReloadResult.AppliedResult);
        AssertEx.True(failedListenerReload is ProxyListenerReloadResult.FailedResult);

        var loadFailed = ProxyConfigurationReloadResult<TestConfigurationProjection>.LoadFailed(
            sourceDirectory: "data",
            attemptedAtUtc: attemptedAtUtc,
            activeVersion: 7,
            loadedAtUtc: loadedAtUtc,
            discovery: discovery,
            errors: ["parse failed"],
            fileErrors: [ProxyConfigurationFileError.ForPath("sites/broken.json", "parse failed")],
            activeConfiguration: active);
        var listenerFailed = ProxyConfigurationReloadResult<TestConfigurationProjection>.ListenerReloadFailed(
            sourceDirectory: "data",
            attemptedAtUtc: attemptedAtUtc,
            activeVersion: 7,
            loadedAtUtc: loadedAtUtc,
            discovery: discovery,
            listenerReload: failedListenerReload,
            activeConfiguration: active);
        var reloaded = ProxyConfigurationReloadResult<TestConfigurationProjection>.Reloaded(
            sourceDirectory: "data",
            attemptedAtUtc: attemptedAtUtc,
            activeVersion: 8,
            loadedAtUtc: loadedAtUtc,
            discovery: discovery,
            listenerReload: listenerReload,
            activeConfiguration: active);

        var loadFailedResult = ProxyConfigurationReloadResultAssertions.LoadFailed(loadFailed);
        AssertEx.Equal(7, loadFailed.ActiveVersion!.Value);
        AssertEx.Equal(loadedAtUtc, loadFailed.LastSuccessfulLoadAtUtc!.Value);
        AssertEx.Equal("parse failed", loadFailed.Errors[0]);
        AssertEx.Equal("sites/broken.json", loadFailed.FileErrors[0].Path);
        AssertEx.Equal(active, AssertEx.NotNull(loadFailedResult.ActiveConfiguration));
        var listenerFailedResult = ProxyConfigurationReloadResultAssertions.ListenerReloadFailed(listenerFailed);
        AssertEx.Equal(failedListenerReload, listenerFailedResult.ListenerReload);
        AssertEx.Equal("listener failed", listenerFailed.Errors[0]);
        AssertEx.Equal("listener failed", listenerFailed.FileErrors[0].Message);
        var reloadedResult = ProxyConfigurationReloadResultAssertions.Reloaded(reloaded);
        AssertEx.Equal(8, reloaded.ActiveVersion!.Value);
        AssertEx.Equal(0, reloaded.Errors.Count);
        AssertEx.Equal(0, reloaded.FileErrors.Count);
        AssertEx.Equal(listenerReload, reloadedResult.ListenerReload);
        AssertEx.Equal(active, reloadedResult.ActiveConfiguration);

    }

    public static void ListenerReloadResultCopiesChangesAndErrors()
    {
        var changes = new List<ProxyListenerReloadChange>
        {
            new("changed", "main", "main|tcp", "127.0.0.1|18080|tcp", "active")
        };
        var errors = new List<string> { "bind failed" };

        var result = ProxyListenerReloadResult.Failed(
            DateTimeOffset.UnixEpoch,
            added: 0,
            removed: 0,
            changed: 1,
            unchanged: 0,
            changes,
            errors);

        changes.Clear();
        errors.Clear();

        AssertEx.Equal(1, result.Changes.Count);
        AssertEx.Equal("changed", result.Changes[0].Action);
        AssertEx.Equal("main", result.Changes[0].Name);
        AssertEx.False(result.Changes is ProxyListenerReloadChange[], "Listener reload changes should not expose a mutable array.");
        AssertEx.Equal(1, result.Errors.Count);
        AssertEx.Equal("bind failed", result.Errors[0]);
        AssertEx.False(result.Errors is string[], "Listener reload errors should not expose a mutable array.");
        var response = ProxyListenerReloadResponse.FromResult(result);
        AssertEx.False(response.Changes is ProxyListenerReloadChangeResponse[], "Listener reload API changes should not expose a mutable array.");
        AssertEx.False(response.Errors is string[], "Listener reload API errors should not expose a mutable array.");
    }

    public static void ApiResultResponseMappersRejectNullResults()
    {
        AssertEx.Throws<ArgumentNullException>(() => ProxyListenerReloadResponse.FromResult(null!));
        AssertEx.Throws<ArgumentNullException>(() => ConfigLintResponse.FromResult(null!));
    }

    public static void ConfigurationManagementResultsCopyInputCollections()
    {
        var discoveredFiles = new List<ProxyConfigurationFileDiscovery>
        {
            new("sites/home.json", "json", "loaded", null)
        };
        var createdPaths = new List<string> { "tests/config" };
        var existingPaths = new List<string> { "tests/config/sites" };
        var discovery = new ProxyConfigurationDiscovery(
            new ProxyFilesystemLayout("tests", "tests/config", "tests/config/sites", "tests/logs", "tests/certs", "tests/state", "tests/config/proxy.json"),
            discoveredFiles,
            createdPaths,
            existingPaths);
        var sourceFiles = new List<string> { "sites/home.json" };
        var errors = new List<string> { "parse failed" };
        var fileErrors = new List<ProxyConfigurationFileError>
        {
            ProxyConfigurationFileError.ForPath("sites/home.json", "parse failed")
        };

        var normalize = ProxyConfigurationNormalizeResult.Failed("json", fileErrors);
        var valid = ProxyConfigurationValidationResult.Valid(
            sourceDirectory: "data",
            attemptedAtUtc: DateTimeOffset.UnixEpoch,
            activeVersion: 1,
            lastSuccessfulLoadAtUtc: DateTimeOffset.UnixEpoch,
            wouldBeVersion: 2,
            sourceFiles,
            discovery);
        var invalid = ProxyConfigurationValidationResult.Invalid(
            sourceDirectory: "data",
            attemptedAtUtc: DateTimeOffset.UnixEpoch,
            activeVersion: 1,
            lastSuccessfulLoadAtUtc: DateTimeOffset.UnixEpoch,
            wouldBeVersion: null,
            sourceFiles,
            discovery,
            errors,
            fileErrors);
        var loadFailed = new ProxyConfigurationLoadResult.FailedResult(
            sourceDirectory: "data",
            attemptedAtUtc: DateTimeOffset.UnixEpoch,
            sourceFiles,
            discovery,
            fileErrors,
            wouldBeVersion: null);
        var loadValidated = new ProxyConfigurationLoadResult.ValidatedResult(
            sourceDirectory: "data",
            attemptedAtUtc: DateTimeOffset.UnixEpoch,
            sourceFiles,
            discovery,
            wouldBeVersion: 2);
        var reloadFailed = ProxyConfigurationReloadResult<TestConfigurationProjection>.LoadFailed(
            sourceDirectory: "data",
            attemptedAtUtc: DateTimeOffset.UnixEpoch,
            activeVersion: 1,
            loadedAtUtc: DateTimeOffset.UnixEpoch,
            discovery,
            errors,
            fileErrors,
            activeConfiguration: null);
        var apiReloadFailed = ProxyConfigurationReloadResult<ProxyConfigurationProjection>.LoadFailed(
            sourceDirectory: "data",
            attemptedAtUtc: DateTimeOffset.UnixEpoch,
            activeVersion: 1,
            loadedAtUtc: DateTimeOffset.UnixEpoch,
            discovery,
            errors,
            fileErrors,
            activeConfiguration: null);

        discoveredFiles.Clear();
        createdPaths.Clear();
        existingPaths.Clear();
        sourceFiles.Clear();
        errors.Clear();
        fileErrors.Clear();

        AssertEx.Equal("sites/home.json", discovery.Files[0].Path);
        AssertEx.Equal("tests/config", discovery.CreatedPaths[0]);
        AssertEx.Equal("tests/config/sites", discovery.ExistingPaths[0]);
        AssertEx.False(discovery.Files is ProxyConfigurationFileDiscovery[], "Discovery files should not expose a mutable array.");
        AssertEx.False(discovery.CreatedPaths is string[], "Discovery created paths should not expose a mutable array.");
        AssertEx.False(discovery.ExistingPaths is string[], "Discovery existing paths should not expose a mutable array.");
        AssertEx.Equal("sites/home.json", valid.SourceFiles[0]);
        AssertEx.False(valid.SourceFiles is string[], "Validation source files should not expose a mutable array.");
        AssertEx.Equal("sites/home.json", invalid.SourceFiles[0]);
        AssertEx.Equal("parse failed", invalid.Errors[0]);
        AssertEx.Equal("sites/home.json", invalid.FileErrors[0].Path);
        AssertEx.False(invalid.SourceFiles is string[], "Invalid validation source files should not expose a mutable array.");
        AssertEx.False(invalid.Errors is string[], "Invalid validation errors should not expose a mutable array.");
        AssertEx.False(invalid.FileErrors is ProxyConfigurationFileError[], "Invalid validation file errors should not expose a mutable array.");
        AssertEx.Equal("sites/home.json: parse failed", normalize.Errors[0]);
        AssertEx.Equal("sites/home.json", normalize.FileErrors[0].Path);
        AssertEx.False(normalize.Errors is string[], "Normalize errors should not expose a mutable array.");
        AssertEx.False(normalize.FileErrors is ProxyConfigurationFileError[], "Normalize file errors should not expose a mutable array.");
        AssertEx.Equal("sites/home.json", loadFailed.SourceFiles[0]);
        AssertEx.Equal("sites/home.json", loadFailed.FileErrors[0].Path);
        AssertEx.Equal("sites/home.json: parse failed", loadFailed.Errors[0]);
        AssertEx.False(loadFailed.SourceFiles is string[], "Failed load source files should not expose a mutable array.");
        AssertEx.False(loadFailed.Errors is string[], "Failed load errors should not expose a mutable array.");
        AssertEx.False(loadFailed.FileErrors is ProxyConfigurationFileError[], "Failed load file errors should not expose a mutable array.");
        AssertEx.Equal("sites/home.json", loadValidated.SourceFiles[0]);
        AssertEx.False(loadValidated.SourceFiles is string[], "Validated load source files should not expose a mutable array.");
        AssertEx.Equal("parse failed", reloadFailed.Errors[0]);
        AssertEx.Equal("sites/home.json", reloadFailed.FileErrors[0].Path);
        AssertEx.False(reloadFailed.Errors is string[], "Reload errors should not expose a mutable array.");
        AssertEx.False(reloadFailed.FileErrors is ProxyConfigurationFileError[], "Reload file errors should not expose a mutable array.");
        var normalizeResponse = ProxyConfigurationNormalizeResponse.FromResult(normalize);
        AssertEx.False(normalizeResponse.Errors is string[], "Normalize API errors should not expose a mutable array.");
        AssertEx.False(normalizeResponse.FileErrors is ProxyConfigurationFileErrorResponse[], "Normalize API file errors should not expose a mutable array.");
        var validationResponse = ProxyConfigurationValidationResponse.FromResult(invalid);
        AssertEx.False(validationResponse.SourceFiles is string[], "Validation API source files should not expose a mutable array.");
        AssertEx.False(validationResponse.Errors is string[], "Validation API errors should not expose a mutable array.");
        AssertEx.False(validationResponse.FileErrors is ProxyConfigurationFileErrorResponse[], "Validation API file errors should not expose a mutable array.");
        var reloadResponse = ProxyConfigurationReloadResponse.FromResult(apiReloadFailed);
        AssertEx.False(reloadResponse.Errors is string[], "Reload API errors should not expose a mutable array.");
        AssertEx.False(reloadResponse.FileErrors is ProxyConfigurationFileErrorResponse[], "Reload API file errors should not expose a mutable array.");
        var discoveryResponse = ProxyConfigurationDiscoveryResponse.FromDiscovery(discovery);
        AssertEx.False(discoveryResponse.Files is ProxyConfigurationFileDiscoveryResponse[], "Discovery API files should not expose a mutable array.");
        AssertEx.False(discoveryResponse.CreatedPaths is string[], "Discovery API created paths should not expose a mutable array.");
        AssertEx.False(discoveryResponse.ExistingPaths is string[], "Discovery API existing paths should not expose a mutable array.");
    }

    public static void RuntimeConfigurationPolicyRecordsCopyInputCollections()
    {
        var acmeDomains = new List<string> { "home.test" };
        var acmeContacts = new List<string> { "admin@home.test" };
        var acmeCertificates = new List<RuntimeAcmeCertificateOptions>
        {
            new("home-cert", true, acmeDomains, 21)
        };
        var acmeProjectionDomains = new List<string> { "api.home.test" };
        var acmeProjectionContacts = new List<string> { "ops@home.test" };
        var acmeProjectionCertificates = new List<RuntimeAcmeCertificateProjection>
        {
            new("api-cert", true, acmeProjectionDomains, 14)
        };
        var adminUrls = new List<string> { "http://127.0.0.1:18081" };
        var projectionUrls = new List<string> { "http://127.0.0.1:18082" };
        var trustedProxies = new List<string> { "127.0.0.1" };
        var cacheVaryHeaders = new List<string> { "X-Tenant" };
        var cacheStatusCodes = new List<int> { 200 };
        var cacheMethods = new List<string> { "GET" };
        var certificateDomains = new List<string> { "home.test" };
        var projectionDomains = new List<string> { "api.home.test" };
        var circuitBreakerCodes = new List<int> { 503 };
        var setRequestHeaders = new List<ProxyHeaderField> { new("X-Trace", "enabled") };
        var removeRequestHeaders = new List<string> { "X-Remove-Request" };
        var setResponseHeaders = new List<ProxyHeaderField> { new("X-Frame-Options", "DENY") };
        var removeResponseHeaders = new List<string> { "Server" };
        var setRequestHeaderProjections = new List<RuntimeHeaderFieldProjection> { new("X-Trace", "enabled") };
        var setResponseHeaderProjections = new List<RuntimeHeaderFieldProjection> { new("X-Frame-Options", "DENY") };
        var retryStatusCodes = new List<int> { 502 };
        var retryMethods = new List<string> { "GET" };

        using var certificate = X509CertificateLoader.LoadPkcs12(
            TestCertificates.CreateSelfSignedPfxBytes("home.test"),
            password: null);
        var acme = new RuntimeAcmeOptions(
            true,
            false,
            "https://acme.test/directory",
            acmeContacts,
            true,
            "acme",
            21,
            60,
            10,
            acmeCertificates);
        var acmeProjection = new RuntimeAcmeProjection(
            true,
            false,
            "https://acme.test/directory",
            acmeProjectionContacts,
            true,
            "acme",
            14,
            60,
            10,
            acmeProjectionCertificates);
        var admin = new RuntimeAdminSecurityOptions(
            adminUrls,
            RequireAuthentication: true,
            HasConfiguredToken: true,
            Token: "secret",
            TokenEnvironmentVariable: "MDRAVA_ADMIN_TOKEN",
            TokenSource: "environment",
            RecentAuditCapacity: 128);
        var adminProjection = new RuntimeAdminSecurityProjection(
            projectionUrls,
            RequireAuthentication: true,
            HasConfiguredToken: true,
            Token: "***",
            TokenEnvironmentVariable: "MDRAVA_ADMIN_TOKEN",
            TokenSource: "environment",
            RecentAuditCapacity: 128);
        var forwardedHeaders = new RuntimeForwardedHeadersOptions(
            Enabled: true,
            TrustedProxies: trustedProxies);
        var forwardedHeadersProjection = new RuntimeForwardedHeadersProjection(
            Enabled: true,
            TrustedProxies: trustedProxies);
        var cache = new RuntimeCachePolicy(
            Enabled: true,
            MaxEntryBytes: 1024,
            MaxTotalBytes: 4096,
            DefaultTtl: TimeSpan.FromSeconds(60),
            RespectOriginCacheControl: true,
            VaryByHeaders: cacheVaryHeaders.Select(static header => header),
            CacheableStatusCodes: cacheStatusCodes.Select(static statusCode => statusCode),
            Methods: cacheMethods.Select(static method => method));
        var cacheProjection = new RuntimeCacheProjection(
            Enabled: true,
            MaxEntryBytes: 1024,
            MaxTotalBytes: 4096,
            DefaultTtl: TimeSpan.FromSeconds(60),
            RespectOriginCacheControl: true,
            VaryByHeaders: cacheVaryHeaders.Select(static header => header),
            CacheableStatusCodes: cacheStatusCodes.Select(static statusCode => statusCode),
            Methods: cacheMethods.Select(static method => method));
        var runtimeCertificate = new RuntimeCertificate(
            "home-cert",
            "certs/home.pfx",
            "pfx",
            HasConfiguredPassword: false,
            certificate,
            "manual",
            certificateDomains);
        var certificateProjection = new RuntimeCertificateProjection(
            "api-cert",
            "certs/api.pfx",
            "pfx",
            "manual",
            projectionDomains,
            HasConfiguredPassword: false,
            Subject: "CN=api.home.test",
            Thumbprint: "thumbprint",
            NotBefore: DateTime.UnixEpoch,
            NotAfter: DateTime.UnixEpoch.AddDays(30));
        var circuitBreaker = new RuntimeCircuitBreakerPolicy(
            Enabled: true,
            FailureThreshold: 5,
            SamplingWindow: TimeSpan.FromSeconds(60),
            OpenDuration: TimeSpan.FromSeconds(30),
            HalfOpenMaxAttempts: 1,
            FailureStatusCodes: circuitBreakerCodes);
        var headerPolicy = new RuntimeHeaderPolicy(
            setRequestHeaders.Select(static header => header),
            removeRequestHeaders.Select(static header => header),
            setResponseHeaders.Select(static header => header),
            removeResponseHeaders.Select(static header => header));
        var headerPolicyProjection = new RuntimeHeaderPolicyProjection(
            setRequestHeaderProjections.Select(static header => header),
            removeRequestHeaders.Select(static header => header),
            setResponseHeaderProjections.Select(static header => header),
            removeResponseHeaders.Select(static header => header));
        var retry = new RuntimeRetryPolicy(
            Enabled: true,
            MaxAttempts: 2,
            PerAttemptTimeout: TimeSpan.FromSeconds(1),
            RetryOnConnectFailure: true,
            RetryOnUpstreamResponseHeadTimeout: true,
            RetryOnStatusCodes: retryStatusCodes.Select(static statusCode => statusCode),
            RetryMethods: retryMethods.Select(static method => method),
            RetryBackoff: TimeSpan.FromMilliseconds(50));
        var retryProjection = new RuntimeRetryProjection(
            Enabled: true,
            MaxAttempts: 2,
            PerAttemptTimeout: TimeSpan.FromSeconds(1),
            RetryOnConnectFailure: true,
            RetryOnUpstreamResponseHeadTimeout: true,
            RetryOnStatusCodes: retryStatusCodes.Select(static statusCode => statusCode),
            RetryMethods: retryMethods.Select(static method => method),
            RetryBackoff: TimeSpan.FromMilliseconds(50));

        acmeDomains.Clear();
        acmeContacts.Clear();
        acmeCertificates.Clear();
        acmeProjectionDomains.Clear();
        acmeProjectionContacts.Clear();
        acmeProjectionCertificates.Clear();
        adminUrls.Clear();
        projectionUrls.Clear();
        trustedProxies.Clear();
        cacheVaryHeaders.Clear();
        cacheStatusCodes.Clear();
        cacheMethods.Clear();
        certificateDomains.Clear();
        projectionDomains.Clear();
        circuitBreakerCodes.Clear();
        setRequestHeaders.Clear();
        removeRequestHeaders.Clear();
        setResponseHeaders.Clear();
        removeResponseHeaders.Clear();
        setRequestHeaderProjections.Clear();
        setResponseHeaderProjections.Clear();
        retryStatusCodes.Clear();
        retryMethods.Clear();

        AssertEx.Equal("home.test", acme.Certificates[0].Domains[0]);
        AssertEx.Equal("admin@home.test", acme.ContactEmails[0]);
        AssertEx.Equal("api.home.test", acmeProjection.Certificates[0].Domains[0]);
        AssertEx.Equal("ops@home.test", acmeProjection.ContactEmails[0]);
        AssertEx.Equal("http://127.0.0.1:18081", admin.Urls[0]);
        AssertEx.Equal("http://127.0.0.1:18082", adminProjection.Urls[0]);
        AssertEx.Equal("127.0.0.1", forwardedHeaders.TrustedProxies[0]);
        AssertEx.Equal("127.0.0.1", forwardedHeadersProjection.TrustedProxies[0]);
        AssertEx.Equal("X-Tenant", cache.VaryByHeaders[0]);
        AssertEx.Equal(200, cache.CacheableStatusCodes[0]);
        AssertEx.Equal("GET", cache.Methods[0]);
        AssertEx.Equal("X-Tenant", cacheProjection.VaryByHeaders[0]);
        AssertEx.Equal(200, cacheProjection.CacheableStatusCodes[0]);
        AssertEx.Equal("GET", cacheProjection.Methods[0]);
        AssertEx.Equal("home.test", runtimeCertificate.Domains[0]);
        AssertEx.Equal("api.home.test", certificateProjection.Domains[0]);
        AssertEx.Equal(503, circuitBreaker.FailureStatusCodes[0]);
        AssertEx.Equal("X-Trace", headerPolicy.SetRequestHeaders[0].Name);
        AssertEx.Equal("X-Remove-Request", headerPolicy.RemoveRequestHeaders[0]);
        AssertEx.Equal("X-Frame-Options", headerPolicy.SetResponseHeaders[0].Name);
        AssertEx.Equal("Server", headerPolicy.RemoveResponseHeaders[0]);
        AssertEx.Equal("X-Trace", headerPolicyProjection.SetRequestHeaders[0].Name);
        AssertEx.Equal("X-Remove-Request", headerPolicyProjection.RemoveRequestHeaders[0]);
        AssertEx.Equal("X-Frame-Options", headerPolicyProjection.SetResponseHeaders[0].Name);
        AssertEx.Equal("Server", headerPolicyProjection.RemoveResponseHeaders[0]);
        AssertEx.Equal(502, retry.RetryOnStatusCodes[0]);
        AssertEx.Equal("GET", retry.RetryMethods[0]);
        AssertEx.Equal(502, retryProjection.RetryOnStatusCodes[0]);
        AssertEx.Equal("GET", retryProjection.RetryMethods[0]);
        AssertEx.False(acme.ContactEmails is string[]);
        AssertEx.False(acme.Certificates is RuntimeAcmeCertificateOptions[]);
        AssertEx.False(acme.Certificates[0].Domains is string[]);
        AssertEx.False(acmeProjection.ContactEmails is string[]);
        AssertEx.False(acmeProjection.Certificates is RuntimeAcmeCertificateProjection[]);
        AssertEx.False(acmeProjection.Certificates[0].Domains is string[]);
        AssertEx.False(admin.Urls is string[]);
        AssertEx.False(adminProjection.Urls is string[]);
        AssertEx.False(forwardedHeaders.TrustedProxies is string[]);
        AssertEx.False(forwardedHeadersProjection.TrustedProxies is string[]);
        AssertEx.False(cache.VaryByHeaders is string[]);
        AssertEx.False(cache.CacheableStatusCodes is int[]);
        AssertEx.False(cache.Methods is string[]);
        AssertEx.False(cacheProjection.VaryByHeaders is string[]);
        AssertEx.False(cacheProjection.CacheableStatusCodes is int[]);
        AssertEx.False(cacheProjection.Methods is string[]);
        AssertEx.False(runtimeCertificate.Domains is string[]);
        AssertEx.False(certificateProjection.Domains is string[]);
        AssertEx.False(circuitBreaker.FailureStatusCodes is int[]);
        AssertEx.False(headerPolicy.SetRequestHeaders is ProxyHeaderField[]);
        AssertEx.False(headerPolicy.RemoveRequestHeaders is string[]);
        AssertEx.False(headerPolicy.SetResponseHeaders is ProxyHeaderField[]);
        AssertEx.False(headerPolicy.RemoveResponseHeaders is string[]);
        AssertEx.False(headerPolicyProjection.SetRequestHeaders is RuntimeHeaderFieldProjection[]);
        AssertEx.False(headerPolicyProjection.RemoveRequestHeaders is string[]);
        AssertEx.False(headerPolicyProjection.SetResponseHeaders is RuntimeHeaderFieldProjection[]);
        AssertEx.False(headerPolicyProjection.RemoveResponseHeaders is string[]);
        AssertEx.False(retry.RetryOnStatusCodes is int[]);
        AssertEx.False(retry.RetryMethods is string[]);
        AssertEx.False(retryProjection.RetryOnStatusCodes is int[]);
        AssertEx.False(retryProjection.RetryMethods is string[]);
        var adminResponse = RuntimeAdminSecurityResponse.FromProjection(adminProjection);
        AssertEx.False(adminResponse.Urls is string[], "Admin security API URLs should not expose a mutable array.");
        var forwardedHeadersResponse = RuntimeForwardedHeadersResponse.FromProjection(forwardedHeadersProjection);
        AssertEx.False(forwardedHeadersResponse.TrustedProxies is string[], "Forwarded headers API trusted proxies should not expose a mutable array.");
        var headerPolicyResponse = RuntimeHeaderPolicyResponse.FromProjection(headerPolicyProjection);
        AssertEx.False(headerPolicyResponse.SetRequestHeaders is RuntimeHeaderFieldResponse[], "Header policy API set request headers should not expose a mutable array.");
        AssertEx.False(headerPolicyResponse.RemoveRequestHeaders is string[], "Header policy API remove request headers should not expose a mutable array.");
        AssertEx.False(headerPolicyResponse.SetResponseHeaders is RuntimeHeaderFieldResponse[], "Header policy API set response headers should not expose a mutable array.");
        AssertEx.False(headerPolicyResponse.RemoveResponseHeaders is string[], "Header policy API remove response headers should not expose a mutable array.");
        var cacheResponse = RuntimeCachePolicyResponse.FromProjection(cacheProjection);
        AssertEx.False(cacheResponse.VaryByHeaders is string[], "Cache API vary headers should not expose a mutable array.");
        AssertEx.False(cacheResponse.CacheableStatusCodes is int[], "Cache API cacheable status codes should not expose a mutable array.");
        AssertEx.False(cacheResponse.Methods is string[], "Cache API methods should not expose a mutable array.");
        var retryResponse = RuntimeRetryPolicyResponse.FromProjection(retryProjection);
        AssertEx.False(retryResponse.RetryOnStatusCodes is int[], "Retry API status codes should not expose a mutable array.");
        AssertEx.False(retryResponse.RetryMethods is string[], "Retry API methods should not expose a mutable array.");
        var acmeResponse = RuntimeAcmeResponse.FromProjection(acmeProjection);
        AssertEx.False(acmeResponse.ContactEmails is string[], "ACME API contact emails should not expose a mutable array.");
        AssertEx.False(acmeResponse.Certificates is RuntimeAcmeCertificateResponse[], "ACME API certificates should not expose a mutable array.");
        AssertEx.False(acmeResponse.Certificates[0].Domains is string[], "ACME API certificate domains should not expose a mutable array.");
        var certificateResponses = RuntimeCertificateResponse.FromCertificates([certificateProjection]);
        AssertEx.False(certificateResponses is RuntimeCertificateResponse[], "Configuration API certificates should not expose a mutable array.");
        AssertEx.False(certificateResponses[0].Domains is string[], "Configuration API certificate domains should not expose a mutable array.");
    }

    public static void RuntimeConfigurationGraphRecordsCopyInputCollections()
    {
        var sourceFiles = new List<string> { "sites/home.json" };
        var sniCertificates = new List<RuntimeSniCertificateBinding>
        {
            new("home.test", "home-cert")
        };
        var upstreams = new List<RuntimeUpstream>
        {
            new("home", "local", "http", RuntimeUpstreamProtocol.Http1, "127.0.0.1", 5000, 1, RuntimeUpstreamTlsOptions.Default)
        };

        using var certificate = X509CertificateLoader.LoadPkcs12(
            TestCertificates.CreateSelfSignedPfxBytes("home.test"),
            password: null);
        var runtimeCertificate = new RuntimeCertificate(
            "home-cert",
            "certs/home.pfx",
            "pfx",
            HasConfiguredPassword: false,
            certificate,
            "manual",
            ["home.test"]);
        var certificates = new Dictionary<string, RuntimeCertificate>(StringComparer.OrdinalIgnoreCase)
        {
            ["home-cert"] = runtimeCertificate
        };

        var listener = new RuntimeListener(
            "web",
            "127.0.0.1",
            18080,
            Enabled: true,
            RuntimeListenerTransport.Https,
            DefaultCertificateId: "home-cert",
            sniCertificates,
            Backlog: 512,
            MaxRequestHeadBytes: 32768,
            MaxResponseHeadBytes: 32768,
            MaxChunkLineBytes: 8192,
            ForwardingBufferBytes: 8192);
        var route = new RuntimeRoute(
            "home",
            "home.test",
            "/",
            RuntimeRouteAction.Proxy,
            "round-robin",
            new RuntimeHealthCheckOptions(false, "/health", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), 1, 1),
            upstreams,
            new RuntimeHttpsRedirectPolicy(false, 308, null),
            new RuntimeCanonicalHostPolicy(false, "", 308),
            RuntimeHeaderPolicy.Empty,
            new RuntimePathRewritePolicy("", "", ""),
            new RuntimeRedirectPolicy(308, "", "", true),
            new RuntimeStaticResponse(200, "text/plain; charset=utf-8", "ok"),
            new RuntimeMaintenancePolicy(false, null, "text/plain; charset=utf-8", "Service Unavailable"),
            RuntimeCachePolicy.Disabled,
            new RuntimeRouteResolvedOptions(104857600, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), true))
        {
            SiteName = "home"
        };
        var listeners = new List<RuntimeListener> { listener };
        var routes = new List<RuntimeRoute> { route };
        var snapshot = new ProxyConfigurationSnapshot(
            1,
            DateTimeOffset.UnixEpoch,
            "tests",
            sourceFiles,
            new ProxyConfigurationDiscovery(
                new ProxyFilesystemLayout("tests", "tests/config", "tests/config/sites", "tests/logs", "tests/certs", "tests/state", "tests/config/proxy.json"),
                [],
                [],
                []),
            new RuntimeAdminSecurityOptions([], true, true, "secret", "MDRAVA_ADMIN_TOKEN", "configured", 100),
            new RuntimeAcmeOptions(false, true, "", [], false, "acme", 30, 720, 60, []),
            new RuntimeTimeouts(
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(10)),
            new RuntimeConnectionLimits(100, 16, 1024),
            new RuntimeObservabilityOptions(true, 100, new RuntimeLogPersistenceOptions(true, true, 1_048_576, 8)),
            new RuntimeLimits(4096, 128, 240, 30, 32768, 128, 8192, 104857600, 8192, TimeSpan.FromSeconds(15)),
            new RuntimeForwardedHeadersOptions(true, []),
            certificates,
            listeners,
            routes);

        sourceFiles.Clear();
        sniCertificates.Clear();
        upstreams.Clear();
        certificates.Clear();
        listeners.Clear();
        routes.Clear();

        AssertEx.Equal("home.test", listener.SniCertificates[0].HostName);
        AssertEx.Equal("local", route.Upstreams[0].Name);
        AssertEx.Equal("sites/home.json", snapshot.SourceFiles[0]);
        AssertEx.True(snapshot.Certificates.ContainsKey("HOME-CERT"));
        AssertEx.Equal("home-cert", snapshot.Certificates["HOME-CERT"].Id);
        AssertEx.Equal("web", snapshot.Listeners[0].Name);
        AssertEx.Equal("home", snapshot.Routes[0].Name);
        AssertEx.False(listener.SniCertificates is RuntimeSniCertificateBinding[]);
        AssertEx.False(route.Upstreams is RuntimeUpstream[]);
        AssertEx.False(snapshot.SourceFiles is string[]);
        AssertEx.False(snapshot.Certificates is Dictionary<string, RuntimeCertificate>);
        AssertEx.False(snapshot.Listeners is RuntimeListener[]);
        AssertEx.False(snapshot.Routes is RuntimeRoute[]);
        var projection = ProxyConfigurationProjectionMapper.ToProjection(
            snapshot,
            new RuntimeHttp3SupportProjection(
                "unknown",
                QuicListenerSupported: false,
                QuicConnectionSupported: false,
                "disabled",
                "disabled",
                EnabledForTraffic: false,
                QuicListenerReady: false,
                AltSvcConfigured: false,
                AltSvcActive: false,
                AltSvcMaxAgeSeconds: null,
                "not_configured",
                UdpQuicListenerIdentityModeled: true,
                "not_ready"));
        AssertEx.Equal("sites/home.json", projection.SourceFiles[0]);
        AssertEx.Equal("home-cert", projection.Certificates[0].Id);
        AssertEx.Equal("web", projection.Listeners[0].Name);
        AssertEx.Equal("home", projection.Routes[0].Name);
        AssertEx.False(projection.SourceFiles is string[]);
        AssertEx.False(projection.Certificates is RuntimeCertificateProjection[]);
        AssertEx.False(projection.Listeners is RuntimeListenerProjection[]);
        AssertEx.False(projection.Routes is RuntimeRouteProjection[]);
        var response = ProxyConfigurationResponse.FromProjection(projection);
        AssertEx.False(response.SourceFiles is string[], "Configuration API source files should not expose a mutable array.");
        var listenerResponses = RuntimeListenerResponse.FromListeners(projection.Listeners);
        AssertEx.False(listenerResponses is RuntimeListenerResponse[], "Configuration API listeners should not expose a mutable array.");
        AssertEx.False(listenerResponses[0].SniCertificates is RuntimeSniCertificateBindingResponse[], "Configuration API listener SNI certificates should not expose a mutable array.");
        AssertEx.Throws<ArgumentNullException>(() => RuntimeRouteResponse.FromRoutes(null!));
        var routeResponses = RuntimeRouteResponse.FromRoutes(projection.Routes.Select(static route => route));
        AssertEx.False(routeResponses is RuntimeRouteResponse[], "Configuration API routes should not expose a mutable array.");
        AssertEx.False(routeResponses[0].Upstreams is RuntimeUpstreamResponse[], "Configuration API route upstreams should not expose a mutable array.");
        AssertEx.False(routeResponses[0].Upstreams[0].CircuitBreaker.FailureStatusCodes is int[], "Configuration API circuit breaker status codes should not expose a mutable array.");
    }

    public static void ConfigurationValidationResultNamesValidationOutcomes()
    {
        var attemptedAtUtc = DateTimeOffset.UnixEpoch.AddMinutes(3);
        var loadedAtUtc = DateTimeOffset.UnixEpoch.AddMinutes(2);
        var discovery = new ProxyConfigurationDiscovery(
            new ProxyFilesystemLayout("tests", "tests/config", "tests/config/sites", "tests/logs", "tests/certs", "tests/state", "tests/config/proxy.json"),
            [],
            [],
            []);
        var valid = ProxyConfigurationValidationResult.Valid(
            sourceDirectory: "data",
            attemptedAtUtc: attemptedAtUtc,
            activeVersion: 4,
            lastSuccessfulLoadAtUtc: loadedAtUtc,
            wouldBeVersion: 5,
            sourceFiles: ["sites/home.json"],
            discovery: discovery);
        var invalid = ProxyConfigurationValidationResult.Invalid(
            sourceDirectory: "data",
            attemptedAtUtc: attemptedAtUtc,
            activeVersion: 4,
            lastSuccessfulLoadAtUtc: loadedAtUtc,
            wouldBeVersion: null,
            sourceFiles: ["sites/broken.json"],
            discovery: discovery,
            errors: ["parse failed"],
            fileErrors: [ProxyConfigurationFileError.ForPath("sites/broken.json", "parse failed")]);

        AssertEx.True(valid is ProxyConfigurationValidationResult.ValidResult);
        AssertEx.Equal(4, valid.ActiveVersion!.Value);
        AssertEx.Equal(loadedAtUtc, valid.LastSuccessfulLoadAtUtc!.Value);
        AssertEx.Equal(5, valid.WouldBeVersion!.Value);
        AssertEx.Equal("sites/home.json", valid.SourceFiles[0]);
        AssertEx.Equal(0, valid.Errors.Count);
        AssertEx.Equal(0, valid.FileErrors.Count);
        AssertEx.True(invalid is ProxyConfigurationValidationResult.InvalidResult);
        AssertEx.Equal(4, invalid.ActiveVersion!.Value);
        AssertEx.Equal(loadedAtUtc, invalid.LastSuccessfulLoadAtUtc!.Value);
        AssertEx.Equal<int?>(null, invalid.WouldBeVersion);
        AssertEx.Equal("parse failed", invalid.Errors[0]);
        AssertEx.Equal("sites/broken.json", invalid.FileErrors[0].Path);
    }

    public static void ConfigurationReadResultNamesAvailableAndMissingOutcomes()
    {
        var projection = new TestConfigurationProjection("current");
        var available = ProxyConfigurationReadResult<TestConfigurationProjection>.Available(projection);
        var missing = ProxyConfigurationReadResult<TestConfigurationProjection>.MissingConfiguration;

        AssertEx.True(available is ProxyConfigurationReadResult<TestConfigurationProjection>.AvailableResult);
        AssertEx.Equal(
            projection,
            ((ProxyConfigurationReadResult<TestConfigurationProjection>.AvailableResult)available).Configuration);
        AssertEx.True(missing is not ProxyConfigurationReadResult<TestConfigurationProjection>.AvailableResult);
    }

    public static async Task ConfigurationLoadResultNamesLoadedValidatedAndFailedOutcomes()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        var load = await CreateLoader(temp.Path).LoadAsync(CancellationToken.None);
        var snapshot = ProxyConfigurationLoadResultAssertions.AssertLoadedSnapshot(load);
        var loaded = ProxyConfigurationLoadResult.Loaded(load.SourceDirectory, snapshot, load.Discovery);
        var validated = ProxyConfigurationLoadResult.Validated(
            load.SourceDirectory,
            DateTimeOffset.UnixEpoch,
            load.SourceFiles,
            load.Discovery,
            wouldBeVersion: snapshot.Version + 1);
        var failed = ProxyConfigurationLoadResult.Failed(
            load.SourceDirectory,
            DateTimeOffset.UnixEpoch,
            load.SourceFiles,
            load.Discovery,
            [ProxyConfigurationFileError.ForPath("sites/broken.json", "parse failed")],
            wouldBeVersion: null);

        AssertEx.True(loaded is ProxyConfigurationLoadResult.LoadedResult);
        var loadedResult = (ProxyConfigurationLoadResult.LoadedResult)loaded;
        AssertEx.Equal(snapshot, loadedResult.Snapshot);
        AssertEx.Equal(snapshot.Version, loadedResult.WouldBeVersion!.Value);
        AssertEx.Equal(0, loadedResult.Errors.Count);
        AssertEx.True(validated is ProxyConfigurationLoadResult.ValidatedResult);
        var validatedResult = (ProxyConfigurationLoadResult.ValidatedResult)validated;
        AssertEx.Equal(snapshot.Version + 1, validatedResult.WouldBeVersion!.Value);
        AssertEx.Equal(0, validatedResult.Errors.Count);
        AssertEx.True(failed is ProxyConfigurationLoadResult.FailedResult);
        var failedResult = (ProxyConfigurationLoadResult.FailedResult)failed;
        AssertEx.Equal<int?>(null, failedResult.WouldBeVersion);
        AssertEx.Equal("sites/broken.json: parse failed", failedResult.Errors[0]);
        AssertEx.Equal("sites/broken.json", failedResult.FileErrors[0].Path);
    }

    public static void DataDirectoryUsesConfiguredOverride()
    {
        var expected = Path.Combine(Path.GetTempPath(), $"mdrava-test-{Guid.NewGuid():N}");
        var provider = new MdravaDataDirectoryProvider(new MdravaDataDirectoryOptions
        {
            DataDirectory = expected
        });

        AssertEx.Equal(Path.GetFullPath(expected), provider.GetDataDirectory());
        AssertEx.Equal(Path.Combine(Path.GetFullPath(expected), "config"), provider.GetProxyConfigDirectory());
        AssertEx.Equal(Path.Combine(Path.GetFullPath(expected), "config", "sites"), provider.GetSitesConfigDirectory());
        AssertEx.Equal(Path.Combine(Path.GetFullPath(expected), "logs"), provider.GetLogsDirectory());
        AssertEx.Equal(Path.Combine(Path.GetFullPath(expected), "certs"), provider.GetCertificatesDirectory());
        AssertEx.Equal(Path.Combine(Path.GetFullPath(expected), "state"), provider.GetStateDirectory());
    }

    public static void DataDirectoryUsesEnvironmentOverride()
    {
        var previous = Environment.GetEnvironmentVariable(MdravaDataDirectoryProvider.EnvironmentVariableName);
        var expected = Path.Combine(Path.GetTempPath(), $"mdrava-env-{Guid.NewGuid():N}");

        try
        {
            Environment.SetEnvironmentVariable(MdravaDataDirectoryProvider.EnvironmentVariableName, expected);
            var provider = new MdravaDataDirectoryProvider(new MdravaDataDirectoryOptions
            {
                DataDirectory = Path.Combine(Path.GetTempPath(), "ignored")
            });

            AssertEx.Equal(Path.GetFullPath(expected), provider.GetDataDirectory());
        }
        finally
        {
            Environment.SetEnvironmentVariable(MdravaDataDirectoryProvider.EnvironmentVariableName, previous);
        }
    }

    public static void DataDirectoryDefaultsUnderLocalApplicationDataWhenAvailable()
    {
        var previous = Environment.GetEnvironmentVariable(MdravaDataDirectoryProvider.EnvironmentVariableName);

        try
        {
            Environment.SetEnvironmentVariable(MdravaDataDirectoryProvider.EnvironmentVariableName, null);
            var provider = new MdravaDataDirectoryProvider(new MdravaDataDirectoryOptions());
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                AssertEx.Equal(Path.Combine(localAppData, "MDRAVA"), provider.GetDataDirectory());
            }
            else
            {
                AssertEx.Equal(Path.Combine(AppContext.BaseDirectory, "MDRAVA"), provider.GetDataDirectory());
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(MdravaDataDirectoryProvider.EnvironmentVariableName, previous);
        }
    }

    public static async Task LoaderLoadsValidSiteFiles()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        var loadedAtUtc = new DateTimeOffset(2026, 6, 10, 11, 15, 0, TimeSpan.Zero);
        var loader = CreateLoader(temp.Path, new FixedTimeProvider(loadedAtUtc));

        var result = await loader.LoadAsync(CancellationToken.None);

        var snapshot = ProxyConfigurationLoadResultAssertions.AssertLoadedSnapshot(result);
        AssertEx.Equal(loadedAtUtc, result.AttemptedAtUtc);
        AssertEx.Equal(loadedAtUtc, snapshot.LoadedAtUtc);
        AssertEx.Equal(Path.Combine(temp.Path, "config", "sites"), result.SourceDirectory);
        AssertEx.Equal(1, snapshot.Listeners.Count);
        AssertEx.Equal(1, snapshot.Routes.Count);
        AssertEx.Equal("home", snapshot.Routes[0].Name);
        AssertEx.Equal(1, snapshot.SourceFiles.Count);
        AssertEx.Equal(TimeSpan.FromSeconds(10), snapshot.Timeouts.ClientRequestHeadTimeout);
    }

    public static async Task LoaderLoadsEquivalentJsonAndYamlSiteFiles()
    {
        using var jsonTemp = TemporaryDirectory.Create();
        using var yamlTemp = TemporaryDirectory.Create();
        WriteSite(jsonTemp.Path, "home.json", port: 18080, upstreamPort: 15000);
        WriteYamlSite(yamlTemp.Path, "home.yml", port: 18080, upstreamPort: 15000);

        var jsonResult = await CreateLoader(jsonTemp.Path).LoadAsync(CancellationToken.None);
        var yamlResult = await CreateLoader(yamlTemp.Path).LoadAsync(CancellationToken.None);

        var jsonSnapshot = ProxyConfigurationLoadResultAssertions.AssertLoadedSnapshot(jsonResult);
        var yamlSnapshot = ProxyConfigurationLoadResultAssertions.AssertLoadedSnapshot(yamlResult);
        AssertEx.Equal(jsonSnapshot.Listeners[0].Name, yamlSnapshot.Listeners[0].Name);
        AssertEx.Equal(jsonSnapshot.Listeners[0].Port, yamlSnapshot.Listeners[0].Port);
        AssertEx.Equal(jsonSnapshot.Routes[0].Name, yamlSnapshot.Routes[0].Name);
        AssertEx.Equal(jsonSnapshot.Routes[0].Upstreams[0].Endpoint, yamlSnapshot.Routes[0].Upstreams[0].Endpoint);
        AssertEx.True(yamlSnapshot.Discovery.Files.Any(static file => file.Format == "yaml" && file.Status == "loaded"));
    }

    public static async Task LoaderReportsYamlParseErrorsWithPerFileDiagnostics()
    {
        using var temp = TemporaryDirectory.Create();
        var sites = Directory.CreateDirectory(Path.Combine(temp.Path, "config", "sites")).FullName;
        var yamlPath = Path.Combine(sites, "broken.yaml");
        File.WriteAllText(yamlPath, "name: broken\nlisteners:\n  - name: main\n    port: [not-closed\n");
        var attemptedAtUtc = new DateTimeOffset(2026, 6, 10, 11, 20, 0, TimeSpan.Zero);
        var loader = CreateLoader(temp.Path, new FixedTimeProvider(attemptedAtUtc));

        var result = await loader.LoadAsync(CancellationToken.None);

        ProxyConfigurationLoadResultAssertions.AssertFailed(result);
        AssertEx.Equal(attemptedAtUtc, result.AttemptedAtUtc);
        AssertEx.True(result.FileErrors.Any(error => string.Equals(error.Path, yamlPath, StringComparison.OrdinalIgnoreCase)));
        AssertEx.True(result.Errors.Any(static error => error.Contains("YAML", StringComparison.OrdinalIgnoreCase)), string.Join("; ", result.Errors));
        AssertEx.True(result.Discovery.Files.Any(file =>
            string.Equals(file.Path, yamlPath, StringComparison.OrdinalIgnoreCase)
            && file.Format == "yaml"
            && file.Status == "failed"));
    }

    public static async Task LoaderLoadsRouteLoadBalancingAndHealthCheckSettings()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSiteWithTwoUpstreams(temp.Path, "pool.json", port: 18080, firstUpstreamPort: 15000, secondUpstreamPort: 15001, healthCheckEnabled: true);
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        var route = ProxyConfigurationLoadResultAssertions.AssertLoadedSnapshot(result).Routes[0];
        AssertEx.Equal("round-robin", route.LoadBalancingPolicy);
        AssertEx.True(route.HealthCheck.Enabled);
        AssertEx.Equal(2, route.Upstreams.Count);
        AssertEx.Equal(2, route.Upstreams[1].Weight);
    }

    public static async Task LoaderCreatesMissingConfigDirectoriesAndLoadsEmptySnapshot()
    {
        using var temp = TemporaryDirectory.Create();
        var configDirectory = Path.Combine(temp.Path, "config");
        var sitesDirectory = Path.Combine(configDirectory, "sites");
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        var snapshot = ProxyConfigurationLoadResultAssertions.AssertLoadedSnapshot(result);
        AssertEx.True(Directory.Exists(configDirectory));
        AssertEx.True(Directory.Exists(sitesDirectory));
        AssertEx.True(Directory.Exists(Path.Combine(temp.Path, "logs")));
        AssertEx.True(Directory.Exists(Path.Combine(temp.Path, "certs")));
        AssertEx.True(Directory.Exists(Path.Combine(temp.Path, "state")));
        AssertEx.True(File.Exists(Path.Combine(configDirectory, "proxy.json")));
        AssertEx.True(File.Exists(Path.Combine(sitesDirectory, "example.site.yaml")));
        AssertEx.Equal(0, snapshot.Listeners.Count);
        AssertEx.Equal(0, snapshot.Routes.Count);
        AssertEx.Equal(0, snapshot.SourceFiles.Count);
        AssertEx.True(snapshot.Discovery.CreatedPaths.Count > 0);
        AssertEx.True(snapshot.Discovery.Files.Any(static file => file.Status == "skipped" && file.Format == "yaml"));
    }

    public static async Task LoaderDoesNotOverwriteExistingPlaceholderFiles()
    {
        using var temp = TemporaryDirectory.Create();
        var config = Directory.CreateDirectory(Path.Combine(temp.Path, "config")).FullName;
        var sites = Directory.CreateDirectory(Path.Combine(config, "sites")).FullName;
        var proxyPath = Path.Combine(config, "proxy.json");
        var examplePath = Path.Combine(sites, "example.site.yaml");
        File.WriteAllText(proxyPath, "{ \"observability\": { \"accessLogEnabled\": false } }");
        File.WriteAllText(examplePath, "# custom example");
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        var snapshot = ProxyConfigurationLoadResultAssertions.AssertLoadedSnapshot(result);
        AssertEx.Equal("{ \"observability\": { \"accessLogEnabled\": false } }", File.ReadAllText(proxyPath));
        AssertEx.Equal("# custom example", File.ReadAllText(examplePath));
        AssertEx.False(snapshot.Observability.AccessLogEnabled);
    }

    public static async Task LoaderLoadsExistingEmptySitesDirectory()
    {
        using var temp = TemporaryDirectory.Create();
        Directory.CreateDirectory(Path.Combine(temp.Path, "config", "sites"));
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        var snapshot = ProxyConfigurationLoadResultAssertions.AssertLoadedSnapshot(result);
        AssertEx.Equal(0, snapshot.Listeners.Count);
        AssertEx.Equal(0, snapshot.Routes.Count);
    }

    public static async Task LoaderUsesDefaultsWhenOperationalConfigIsMissing()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        var snapshot = ProxyConfigurationLoadResultAssertions.AssertLoadedSnapshot(result);
        AssertEx.True(File.Exists(Path.Combine(temp.Path, "config", "proxy.json")));
        AssertEx.Equal(TimeSpan.FromSeconds(10), snapshot.Timeouts.ClientRequestHeadTimeout);
    }

    public static async Task LoaderLoadsExplicitOperationalTimeouts()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        WriteOperationalConfig(temp.Path, clientRequestHeadTimeoutMs: 250, tunnelIdleTimeoutMs: 750);
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        var snapshot = ProxyConfigurationLoadResultAssertions.AssertLoadedSnapshot(result);
        AssertEx.Equal(TimeSpan.FromMilliseconds(250), snapshot.Timeouts.ClientRequestHeadTimeout);
        AssertEx.Equal(TimeSpan.FromMilliseconds(750), snapshot.Timeouts.TunnelIdleTimeout);
    }

    public static async Task LoaderLoadsObservabilityDefaults()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        var observability = ProxyConfigurationLoadResultAssertions.AssertLoadedSnapshot(result).Observability;
        AssertEx.True(observability.AccessLogEnabled);
        AssertEx.Equal(500, observability.RecentDiagnosticsCapacity);
        AssertEx.True(observability.LogPersistence.AccessLogEnabled);
        AssertEx.True(observability.LogPersistence.AdminAuditEnabled);
        AssertEx.Equal(1_048_576L, observability.LogPersistence.MaxFileBytes);
        AssertEx.Equal(8, observability.LogPersistence.MaxFiles);
    }

    public static async Task LoaderLoadsExplicitObservabilitySettings()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        WriteOperationalConfig(
            temp.Path,
            accessLogEnabled: false,
            recentDiagnosticsCapacity: 12,
            accessLogFileEnabled: false,
            adminAuditLogFileEnabled: false,
            logMaxFileBytes: 8192,
            logMaxFiles: 3);
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        var observability = ProxyConfigurationLoadResultAssertions.AssertLoadedSnapshot(result).Observability;
        AssertEx.False(observability.AccessLogEnabled);
        AssertEx.Equal(12, observability.RecentDiagnosticsCapacity);
        AssertEx.False(observability.LogPersistence.AccessLogEnabled);
        AssertEx.False(observability.LogPersistence.AdminAuditEnabled);
        AssertEx.Equal(8192L, observability.LogPersistence.MaxFileBytes);
        AssertEx.Equal(3, observability.LogPersistence.MaxFiles);
    }

    public static async Task LoaderRejectsInvalidObservabilityCapacity()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        WriteOperationalConfig(temp.Path, recentDiagnosticsCapacity: 0);
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        ProxyConfigurationLoadResultAssertions.AssertFailed(result);
        AssertEx.True(result.Errors.Any(static error => error.Contains("RecentDiagnosticsCapacity", StringComparison.Ordinal)));
    }

    public static async Task LoaderRejectsInvalidLogPersistenceSettings()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        WriteOperationalConfig(temp.Path, logMaxFileBytes: 1024, logMaxFiles: 0);
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        ProxyConfigurationLoadResultAssertions.AssertFailed(result);
        AssertEx.True(result.Errors.Any(static error => error.Contains("MaxFileBytes", StringComparison.Ordinal)), string.Join("; ", result.Errors));
        AssertEx.True(result.Errors.Any(static error => error.Contains("MaxFiles", StringComparison.Ordinal)), string.Join("; ", result.Errors));
    }

    public static async Task LoaderLoadsLimitDefaults()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        var limits = ProxyConfigurationLoadResultAssertions.AssertLoadedSnapshot(result).Limits;
        AssertEx.Equal(4096, limits.MaxActiveClientConnections);
        AssertEx.Equal(128, limits.MaxConcurrentTlsHandshakes);
        AssertEx.Equal(240, limits.RequestsPerMinutePerIp);
        AssertEx.Equal(TimeSpan.FromSeconds(15), limits.ShutdownGracePeriod);
    }

    public static async Task LoaderRejectsInvalidLimitSettings()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        WriteOperationalConfig(temp.Path, maxActiveClientConnections: 0);
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        ProxyConfigurationLoadResultAssertions.AssertFailed(result);
        AssertEx.True(result.Errors.Any(static error => error.Contains("MaxActiveClientConnections", StringComparison.Ordinal)));
    }

    public static async Task LoaderRejectsInvalidOperationalTimeouts()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        WriteOperationalConfig(temp.Path, clientRequestHeadTimeoutMs: 1);
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        ProxyConfigurationLoadResultAssertions.AssertFailed(result);
        AssertEx.True(result.Errors.Count > 0);
    }

    public static async Task LoaderRejectsInvalidTunnelLimit()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        WriteOperationalConfig(temp.Path, maxActiveUpgradedTunnels: 0);
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        ProxyConfigurationLoadResultAssertions.AssertFailed(result);
        AssertEx.True(result.Errors.Any(static error => error.Contains("MaxActiveUpgradedTunnels", StringComparison.Ordinal)));
    }

    public static async Task LoaderLoadsHttpsListenerWithCertificate()
    {
        using var temp = TemporaryDirectory.Create();
        var certificatePath = Path.Combine(temp.Path, "certs", "home.pfx");
        TestCertificates.WriteSelfSignedPfx(certificatePath, "home.test", "secret");
        WriteHttpsSite(temp.Path, "home.json", port: 18443, upstreamPort: 15000, certificateId: "home-cert");
        WriteOperationalConfig(temp.Path, certificateId: "home-cert", certificatePath: "certs/home.pfx", certificatePassword: "secret");
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        var snapshot = ProxyConfigurationLoadResultAssertions.AssertLoadedSnapshot(result);
        AssertEx.Equal(RuntimeListenerTransport.Https, snapshot.Listeners[0].Transport);
        AssertEx.Equal(1, snapshot.Certificates.Count);
        var projection = ProxyConfigurationProjectionMapper.ToProjection(
            snapshot,
            TestHttp3PlatformSupport.Project(snapshot));
        AssertEx.Equal(1, projection.Certificates.Count);
        AssertEx.Equal(true, projection.Certificates[0].HasConfiguredPassword);
    }

    public static async Task LoaderRejectsHttpsListenerWithMissingCertificateReference()
    {
        using var temp = TemporaryDirectory.Create();
        WriteHttpsSite(temp.Path, "home.json", port: 18443, upstreamPort: 15000, certificateId: "missing-cert");
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        ProxyConfigurationLoadResultAssertions.AssertFailed(result);
        AssertEx.True(result.Errors.Any(static error => error.Contains("unknown certificate", StringComparison.OrdinalIgnoreCase)));
    }

    public static async Task LoaderRejectsInvalidCertificatePath()
    {
        using var temp = TemporaryDirectory.Create();
        WriteHttpsSite(temp.Path, "home.json", port: 18443, upstreamPort: 15000, certificateId: "home-cert");
        WriteOperationalConfig(temp.Path, certificateId: "home-cert", certificatePath: "certs/missing.pfx");
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        ProxyConfigurationLoadResultAssertions.AssertFailed(result);
        AssertEx.True(result.Errors.Any(static error => error.Contains("file does not exist", StringComparison.OrdinalIgnoreCase)));
    }

    public static async Task LoaderRejectsInvalidCertificatePassword()
    {
        using var temp = TemporaryDirectory.Create();
        var certificatePath = Path.Combine(temp.Path, "certs", "home.pfx");
        TestCertificates.WriteSelfSignedPfx(certificatePath, "home.test", "correct");
        WriteHttpsSite(temp.Path, "home.json", port: 18443, upstreamPort: 15000, certificateId: "home-cert");
        WriteOperationalConfig(temp.Path, certificateId: "home-cert", certificatePath: "certs/home.pfx", certificatePassword: "wrong");
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        ProxyConfigurationLoadResultAssertions.AssertFailed(result);
        AssertEx.True(result.Errors.Any(static error => error.Contains("could not be loaded", StringComparison.OrdinalIgnoreCase)));
    }

    public static async Task LoaderRejectsDuplicateSniCertificateMapping()
    {
        using var temp = TemporaryDirectory.Create();
        var certificatePath = Path.Combine(temp.Path, "certs", "home.pfx");
        TestCertificates.WriteSelfSignedPfx(certificatePath, "home.test");
        WriteHttpsSite(
            temp.Path,
            "home.json",
            port: 18443,
            upstreamPort: 15000,
            certificateId: "home-cert",
            duplicateSni: true);
        WriteOperationalConfig(temp.Path, certificateId: "home-cert", certificatePath: "certs/home.pfx");
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        ProxyConfigurationLoadResultAssertions.AssertFailed(result);
        AssertEx.True(result.Errors.Any(static error => error.Contains("duplicated", StringComparison.OrdinalIgnoreCase)));
    }

    public static async Task LoaderMergesSniMappingsFromSharedHttpsListener()
    {
        using var temp = TemporaryDirectory.Create();
        var certificatePath = Path.Combine(temp.Path, "certs", "home.pfx");
        TestCertificates.WriteSelfSignedPfx(certificatePath, "home.test");
        WriteHttpsSite(temp.Path, "home.json", port: 18443, upstreamPort: 15000, certificateId: "home-cert", sniHost: "home.test");
        WriteHttpsSite(temp.Path, "alt.json", port: 18443, upstreamPort: 15001, certificateId: "home-cert", sniHost: "alt.test");
        WriteOperationalConfig(temp.Path, certificateId: "home-cert", certificatePath: "certs/home.pfx");
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        var snapshot = ProxyConfigurationLoadResultAssertions.AssertLoadedSnapshot(result);
        AssertEx.Equal(1, snapshot.Listeners.Count);
        AssertEx.Equal(2, snapshot.Listeners[0].SniCertificates.Count);
        AssertEx.Equal(2, snapshot.Routes.Count);
    }

    public static async Task LoaderRejectsInvalidSiteFile()
    {
        using var temp = TemporaryDirectory.Create();
        var sites = Directory.CreateDirectory(Path.Combine(temp.Path, "config", "sites")).FullName;
        File.WriteAllText(Path.Combine(sites, "broken.json"), "{ nope");
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        ProxyConfigurationLoadResultAssertions.AssertFailed(result);
        AssertEx.True(result.Errors.Count > 0);
    }

    public static async Task ReloadPreservesActiveSnapshotWhenLoadFails()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);

        var store = new ProxyConfigurationStore();
        var service = CreateReloadService(temp.Path, store);
        var first = await service.ReloadAsync(CancellationToken.None);
        ProxyConfigurationReloadResultAssertions.Reloaded(first);
        AssertEx.Equal(1, store.Snapshot.Version);

        File.WriteAllText(Path.Combine(temp.Path, "config", "sites", "broken.json"), "{ nope");
        var second = await service.ReloadAsync(CancellationToken.None);

        ProxyConfigurationReloadResultAssertions.Failed(second);
        AssertEx.Equal(1, store.Snapshot.Version);
    }

    public static async Task ReloadReplacesActiveSnapshotWhenLoadSucceeds()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);

        var store = new ProxyConfigurationStore();
        var service = CreateReloadService(temp.Path, store);
        var first = await service.ReloadAsync(CancellationToken.None);
        ProxyConfigurationReloadResultAssertions.Reloaded(first);

        File.WriteAllText(
            Path.Combine(temp.Path, "config", "sites", "home.json"),
            SiteJson("home", 18081, 15001));

        var second = await service.ReloadAsync(CancellationToken.None);

        ProxyConfigurationReloadResultAssertions.Reloaded(second, string.Join("; ", second.Errors));
        AssertEx.Equal(2, store.Snapshot.Version);
        AssertEx.Equal(18081, store.Snapshot.Listeners[0].Port);
    }

    public static async Task ReloadReplacesActiveSnapshotWithEmptySitesDirectory()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);

        var store = new ProxyConfigurationStore();
        var service = CreateReloadService(temp.Path, store);
        var first = await service.ReloadAsync(CancellationToken.None);
        ProxyConfigurationReloadResultAssertions.Reloaded(first);

        foreach (var siteFile in Directory.EnumerateFiles(Path.Combine(temp.Path, "config", "sites"), "*.json"))
        {
            File.Delete(siteFile);
        }

        var second = await service.ReloadAsync(CancellationToken.None);

        ProxyConfigurationReloadResultAssertions.Reloaded(second, string.Join("; ", second.Errors));
        AssertEx.Equal(2, store.Snapshot.Version);
        AssertEx.Equal(0, store.Snapshot.Listeners.Count);
        AssertEx.Equal(0, store.Snapshot.Routes.Count);
    }

    public static async Task ActiveInspectionProjectionReflectsStore()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);

        var store = new ProxyConfigurationStore();
        var service = CreateReloadService(temp.Path, store);
        var result = await service.ReloadAsync(CancellationToken.None);

        var reload = ProxyConfigurationReloadResultAssertions.Reloaded(result);
        var projection = reload.ActiveConfiguration;
        AssertEx.Equal(1, projection.Version);
        AssertEx.Equal("home", projection.Routes[0].Name);
        AssertEx.Equal(1, projection.SourceFiles.Count);
        object acme = projection.Acme;
        AssertEx.True(acme is RuntimeAcmeProjection);
        AssertEx.False(acme is RuntimeAcmeOptions);
        object acmeCertificates = projection.Acme.Certificates;
        AssertEx.False(acmeCertificates is RuntimeAcmeCertificateOptions[]);
        object timeouts = projection.Timeouts;
        AssertEx.True(timeouts is RuntimeTimeoutsProjection);
        AssertEx.False(timeouts is RuntimeTimeouts);
        object connectionLimits = projection.ConnectionLimits;
        AssertEx.True(connectionLimits is RuntimeConnectionLimitsProjection);
        AssertEx.False(connectionLimits is RuntimeConnectionLimits);
        object observability = projection.Observability;
        AssertEx.True(observability is RuntimeObservabilityProjection);
        AssertEx.False(observability is RuntimeObservabilityOptions);
        object logPersistence = projection.Observability.LogPersistence;
        AssertEx.True(logPersistence is RuntimeLogPersistenceProjection);
        AssertEx.False(logPersistence is RuntimeLogPersistenceOptions);
        object limits = projection.Limits;
        AssertEx.True(limits is RuntimeLimitsProjection);
        AssertEx.False(limits is RuntimeLimits);
        object forwardedHeaders = projection.ForwardedHeaders;
        AssertEx.True(forwardedHeaders is RuntimeForwardedHeadersProjection);
        AssertEx.False(forwardedHeaders is RuntimeForwardedHeadersOptions);
        object metrics = projection.Metrics;
        AssertEx.True(metrics is RuntimeMetricsProjection);
        AssertEx.False(metrics is RuntimeMetricsOptions);
    }

    public static async Task ActiveInspectionProjectionUsesListenerReadModels()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);

        var store = new ProxyConfigurationStore();
        var service = CreateReloadService(temp.Path, store);
        var result = await service.ReloadAsync(CancellationToken.None);

        var projection = ProxyConfigurationReloadResultAssertions.Reloaded(result).ActiveConfiguration;
        var listener = projection.Listeners[0];
        object listenerCollection = projection.Listeners;

        AssertEx.Equal("main", listener.Name);
        AssertEx.Equal(18080, listener.Port);
        AssertEx.Equal(RuntimeListenerTransport.Http, listener.Transport);
        AssertEx.Equal(listener.Name, listener.Identity.Name);
        AssertEx.Equal(listener.Address, listener.Identity.Address);
        AssertEx.Equal(listener.Port, listener.Identity.Port);
        object identity = listener.Identity;
        AssertEx.True(identity is RuntimeListenerIdentityProjection);
        AssertEx.False(identity is RuntimeListenerIdentity);
        object sniCertificates = listener.SniCertificates;
        AssertEx.True(sniCertificates is IReadOnlyList<RuntimeSniCertificateBindingProjection>);
        AssertEx.False(sniCertificates is IReadOnlyList<RuntimeSniCertificateBinding>);
        AssertEx.False(sniCertificates is RuntimeSniCertificateBinding[]);
        AssertEx.False(sniCertificates is RuntimeSniCertificateBindingProjection[]);
        object http3AltSvc = listener.Http3AltSvc;
        AssertEx.True(http3AltSvc is RuntimeHttp3AltSvcProjection);
        AssertEx.False(http3AltSvc is RuntimeHttp3AltSvcOptions);
        object http2Limits = listener.Http2Limits;
        AssertEx.True(http2Limits is RuntimeHttp2LimitsProjection);
        AssertEx.False(http2Limits is RuntimeHttp2Limits);
        object http3 = listener.Http3;
        AssertEx.True(http3 is RuntimeHttp3ListenerReadinessProjection);
        AssertEx.False(http3 is RuntimeHttp3ListenerReadiness);
        object? quicIdentity = listener.QuicIdentity;
        AssertEx.True(quicIdentity is null or RuntimeQuicListenerIdentityProjection);
        AssertEx.False(quicIdentity is RuntimeQuicListenerIdentity);
        AssertEx.False(listenerCollection is RuntimeListener[]);
        AssertEx.False(listenerCollection is RuntimeListenerProjection[]);
    }

    public static async Task ActiveInspectionProjectionUsesRouteReadModels()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);

        var store = new ProxyConfigurationStore();
        var service = CreateReloadService(temp.Path, store);
        var result = await service.ReloadAsync(CancellationToken.None);

        var projection = ProxyConfigurationReloadResultAssertions.Reloaded(result).ActiveConfiguration;
        var route = projection.Routes[0];
        var upstream = route.Upstreams[0];
        object routeCollection = projection.Routes;
        object upstreamCollection = route.Upstreams;

        AssertEx.Equal("home", route.Name);
        AssertEx.Equal("home", route.SiteName);
        AssertEx.Equal(1, route.Upstreams.Count);
        AssertEx.Equal("home", upstream.RouteName);
        AssertEx.Equal("local-test", upstream.Name);
        AssertEx.Equal("127.0.0.1:15000", upstream.Endpoint);
        AssertEx.False(routeCollection is RuntimeRoute[]);
        AssertEx.False(routeCollection is RuntimeRouteProjection[]);
        AssertEx.False(upstreamCollection is RuntimeUpstream[]);
        AssertEx.False(upstreamCollection is RuntimeUpstreamProjection[]);
    }

    public static async Task ConfigReloadControllerReturnsConfigurationResponse()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);

        var store = new ProxyConfigurationStore();
        var service = CreateReloadService(temp.Path, store);
        var controller = new ProxyConfigurationController(
            new ProxyConfigurationAdministrationService(CreateNormalizer(), service),
            CreateReadAdministration(store),
            new ProxyConfigurationReloadAdministrationService<ProxyConfigurationProjection>(service));

        var actionResult = await controller.Reload(CancellationToken.None);

        var ok = (OkObjectResult)AssertEx.NotNull(actionResult.Result);
        var response = (ProxyConfigurationReloadResponse)AssertEx.NotNull(ok.Value);
        var activeConfiguration = AssertEx.NotNull(response.ActiveConfiguration);
        AssertEx.True(response.Succeeded, string.Join("; ", response.Errors));
        AssertEx.Equal(1, activeConfiguration.Version);
        AssertEx.Equal("home", activeConfiguration.Routes[0].Name);
        AssertEx.Equal("home", activeConfiguration.Routes[0].Upstreams[0].RouteName);
        AssertEx.True(AssertEx.NotNull(response.ListenerReload).Succeeded);
    }

    public static void ConfigReadOperationsProjectActiveAndEffectiveFromCurrentSource()
    {
        var projection = new TestConfigurationProjection("current");
        var operations = new ProxyConfigurationReadOperations<TestConfigurationProjection>(
            new FixedConfigurationReadProjectionSource<TestConfigurationProjection>(projection));
        var missingOperations = new ProxyConfigurationReadOperations<TestConfigurationProjection>(
            new FixedConfigurationReadProjectionSource<TestConfigurationProjection>(null));

        var active = operations.ReadActive();
        var effective = operations.ReadEffective();
        var missingActive = missingOperations.ReadActive();
        var missingEffective = missingOperations.ReadEffective();

        AssertEx.True(active is ProxyConfigurationReadResult<TestConfigurationProjection>.AvailableResult);
        AssertEx.Equal(
            projection,
            ((ProxyConfigurationReadResult<TestConfigurationProjection>.AvailableResult)active).Configuration);
        AssertEx.True(effective is ProxyConfigurationReadResult<TestConfigurationProjection>.AvailableResult);
        AssertEx.Equal(
            projection,
            ((ProxyConfigurationReadResult<TestConfigurationProjection>.AvailableResult)effective).Configuration);
        AssertEx.True(missingActive is not ProxyConfigurationReadResult<TestConfigurationProjection>.AvailableResult);
        AssertEx.True(missingEffective is not ProxyConfigurationReadResult<TestConfigurationProjection>.AvailableResult);
    }

    public static async Task LoaderRejectsUnsafeHeaderRule()
    {
        using var temp = TemporaryDirectory.Create();
        WriteCustomSite(
            temp.Path,
            "unsafe.json",
            """
            {
              "name": "unsafe",
              "listeners": [
                {
                  "name": "main",
                  "address": "127.0.0.1",
                  "port": 18080
                }
              ],
              "host": "*",
              "routes": [
                {
                  "name": "app",
                  "pathPrefix": "/",
                  "action": "proxy",
                  "headerPolicy": {
                    "setRequestHeaders": [
                      {
                        "name": "Content-Length",
                        "value": "1"
                      }
                    ]
                  },
                  "upstreams": [
                    {
                      "name": "local",
                      "address": "127.0.0.1",
                      "port": 15000
                    }
                  ]
                }
              ]
            }
            """);
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        ProxyConfigurationLoadResultAssertions.AssertFailed(result);
        AssertEx.True(result.Errors.Any(static error => error.Contains("restricted", StringComparison.OrdinalIgnoreCase)), string.Join("; ", result.Errors));
    }

    public static async Task ResponseHeaderPolicyCannotEmitHopByHopHeaders()
    {
        using var temp = TemporaryDirectory.Create();
        WriteCustomSite(
            temp.Path,
            "unsafe-response.json",
            """
            {
              "name": "unsafe-response",
              "listeners": [
                {
                  "name": "main",
                  "address": "127.0.0.1",
                  "port": 18080
                }
              ],
              "host": "*",
              "routes": [
                {
                  "name": "app",
                  "pathPrefix": "/",
                  "action": "proxy",
                  "headerPolicy": {
                    "setResponseHeaders": [
                      {
                        "name": "Connection",
                        "value": "keep-alive"
                      }
                    ]
                  },
                  "upstreams": [
                    {
                      "name": "local",
                      "address": "127.0.0.1",
                      "port": 15000
                    }
                  ]
                }
              ]
            }
            """);

        var result = await CreateLoader(temp.Path).LoadAsync(CancellationToken.None);

        ProxyConfigurationLoadResultAssertions.AssertFailed(result);
        AssertEx.True(result.Errors.Any(static error => error.Contains("restricted", StringComparison.OrdinalIgnoreCase)), string.Join("; ", result.Errors));
    }

    public static async Task MultiFileConfigConflictReportingIsDeterministic()
    {
        using var temp = TemporaryDirectory.Create();
        TestCertificates.WriteSelfSignedPfx(Path.Combine(temp.Path, "certs", "home.pfx"), "home.test");
        TestCertificates.WriteSelfSignedPfx(Path.Combine(temp.Path, "certs", "alt.pfx"), "alt.test");
        File.WriteAllText(
            Path.Combine(Directory.CreateDirectory(Path.Combine(temp.Path, "config")).FullName, "proxy.json"),
            """
            {
              "certificates": [
                {
                  "id": "home-cert",
                  "format": "pfx",
                  "path": "certs/home.pfx"
                },
                {
                  "id": "alt-cert",
                  "format": "pfx",
                  "path": "certs/alt.pfx"
                }
              ]
            }
            """);
        WriteHttpsSite(temp.Path, "home-a.json", port: 18443, upstreamPort: 15000, certificateId: "home-cert");
        WriteHttpsSite(temp.Path, "home-b.json", port: 18443, upstreamPort: 15001, certificateId: "alt-cert");

        var first = await CreateLoader(temp.Path).LoadAsync(CancellationToken.None);
        var second = await CreateLoader(temp.Path).LoadAsync(CancellationToken.None);

        ProxyConfigurationLoadResultAssertions.AssertFailed(first);
        ProxyConfigurationLoadResultAssertions.AssertFailed(second);
        AssertEx.Equal(string.Join("\n", first.Errors), string.Join("\n", second.Errors));
        AssertEx.True(first.Errors.Any(static error => error.Contains("default certificate", StringComparison.OrdinalIgnoreCase)), string.Join("; ", first.Errors));
    }

    public static async Task ConfigValidateReportsValidWithoutApplying()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        var store = new ProxyConfigurationStore();
        var service = CreateReloadService(temp.Path, store);
        var first = await service.ReloadAsync(CancellationToken.None);
        ProxyConfigurationReloadResultAssertions.Reloaded(first);

        File.WriteAllText(
            Path.Combine(temp.Path, "config", "sites", "home.json"),
            SiteJson("home", 18081, 15001));

        var validation = await service.ValidateAsync(CancellationToken.None);

        AssertEx.True(validation is ProxyConfigurationValidationResult.ValidResult, string.Join("; ", validation.Errors));
        AssertEx.Equal(1, store.Snapshot.Version);
        AssertEx.Equal(2, validation.WouldBeVersion);
        AssertEx.Equal(18080, store.Snapshot.Listeners[0].Port);

        var controller = new ProxyConfigurationController(
            new ProxyConfigurationAdministrationService(CreateNormalizer(), service),
            CreateReadAdministration(store),
            new ProxyConfigurationReloadAdministrationService<ProxyConfigurationProjection>(service));
        var actionResult = await controller.Validate(CancellationToken.None);
        var ok = (OkObjectResult)AssertEx.NotNull(actionResult.Result);
        var response = (ProxyConfigurationValidationResponse)AssertEx.NotNull(ok.Value);
        AssertEx.True(response.Succeeded, string.Join("; ", response.Errors));
        AssertEx.Equal(2, response.WouldBeVersion);
    }

    public static async Task ConfigValidateReportsInvalidWithoutReplacingActiveConfig()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        var store = new ProxyConfigurationStore();
        var service = CreateReloadService(temp.Path, store);
        var first = await service.ReloadAsync(CancellationToken.None);
        ProxyConfigurationReloadResultAssertions.Reloaded(first);

        File.WriteAllText(Path.Combine(temp.Path, "config", "sites", "broken.json"), "{ nope");
        var validation = await service.ValidateAsync(CancellationToken.None);

        AssertEx.True(validation is ProxyConfigurationValidationResult.InvalidResult);
        AssertEx.Equal(1, store.Snapshot.Version);
        AssertEx.True(validation.FileErrors.Any(error => error.Path?.EndsWith("broken.json", StringComparison.OrdinalIgnoreCase) == true));

        var controller = new ProxyConfigurationController(
            new ProxyConfigurationAdministrationService(CreateNormalizer(), service),
            CreateReadAdministration(store),
            new ProxyConfigurationReloadAdministrationService<ProxyConfigurationProjection>(service));
        var actionResult = await controller.Validate(CancellationToken.None);
        var badRequest = (BadRequestObjectResult)AssertEx.NotNull(actionResult.Result);
        var response = (ProxyConfigurationValidationResponse)AssertEx.NotNull(badRequest.Value);
        AssertEx.False(response.Succeeded);
        AssertEx.True(response.FileErrors.Any(error => error.Path?.EndsWith("broken.json", StringComparison.OrdinalIgnoreCase) == true));
    }

    public static async Task ConfigNormalizeConvertsYamlToJsonWithoutApplying()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        var store = new ProxyConfigurationStore();
        var service = CreateReloadService(temp.Path, store);
        var first = await service.ReloadAsync(CancellationToken.None);
        ProxyConfigurationReloadResultAssertions.Reloaded(first);
        var normalizer = CreateNormalizer();
        var reloadAdministration = new ProxyConfigurationReloadAdministrationService<ProxyConfigurationProjection>(
            service);
        var controller = new ProxyConfigurationController(
            new ProxyConfigurationAdministrationService(normalizer, service),
            CreateReadAdministration(store),
            reloadAdministration);

        var actionResult = controller.Normalize(new ProxyConfigurationNormalizeSubmissionRequest(
            "yaml",
            YamlSiteText("normalized", port: 18081, upstreamPort: 15001)));

        var ok = (OkObjectResult)AssertEx.NotNull(actionResult.Result);
        var normalize = (ProxyConfigurationNormalizeResponse)AssertEx.NotNull(ok.Value);
        AssertEx.True(normalize.Succeeded, string.Join("; ", normalize.Errors));
        AssertEx.True(AssertEx.NotNull(normalize.CanonicalJson).Contains("\"Name\": \"normalized\"", StringComparison.Ordinal));
        AssertEx.Equal(1, store.Snapshot.Version);
        AssertEx.Equal(18080, store.Snapshot.Listeners[0].Port);
    }

    public static void ConfigNormalizerShapesValidationFailuresFromParsedSite()
    {
        var parser = new FixedNormalizeSiteParser(
            ProxyConfigurationNormalizeSiteParseResult.Parsed(
                new SiteOptions
                {
                    Name = "broken",
                    Host = "*",
                    PathPrefix = "/"
                },
                "{}"));
        var normalizer = new ProxyConfigurationNormalizer(
            parser,
            new ProxyEndpointAddressPolicy(),
            new ProxyUrlSyntaxPolicy());

        var result = normalizer.Normalize(new ProxyConfigurationNormalizeRequest("yml", "ignored"));

        AssertEx.True(result is ProxyConfigurationNormalizeResult.FailedResult);
        AssertEx.Equal(ProxyConfigurationNormalizeFormat.Yaml, parser.LastFormat);
        AssertEx.Equal("yaml", result.Format);
        AssertEx.True(result.Errors.Any(static error => error.Contains("Proxy:Listeners", StringComparison.Ordinal)), string.Join("; ", result.Errors));
        AssertEx.True(result.FileErrors.All(static error => error.Path is null));
    }

    public static void ConfigNormalizerRejectsMissingRequestBody()
    {
        var normalizer = CreateNormalizer();

        var result = normalizer.Normalize(null);

        AssertEx.True(result is ProxyConfigurationNormalizeResult.FailedResult);
        AssertEx.Equal("unknown", result.Format);
        AssertEx.True(result.Errors.Any(static error => error.Contains("request body is required", StringComparison.Ordinal)), string.Join("; ", result.Errors));
        AssertEx.True(result.FileErrors.All(static error => error.Path is null));
    }

    public static void ConfigNormalizerRejectsIncompleteRequestFields()
    {
        var normalizer = CreateNormalizer();

        var missingFormat = normalizer.Normalize(new ProxyConfigurationNormalizeRequest(null, "ignored"));
        var missingText = normalizer.Normalize(new ProxyConfigurationNormalizeRequest("json", null));

        AssertEx.True(missingFormat is ProxyConfigurationNormalizeResult.FailedResult);
        AssertEx.Equal("unknown", missingFormat.Format);
        AssertEx.True(missingFormat.Errors.Any(static error => error.Contains("Format must be", StringComparison.Ordinal)), string.Join("; ", missingFormat.Errors));
        AssertEx.True(missingText is ProxyConfigurationNormalizeResult.FailedResult);
        AssertEx.Equal("json", missingText.Format);
        AssertEx.True(missingText.Errors.Any(static error => error.Contains("config text is required", StringComparison.Ordinal)), string.Join("; ", missingText.Errors));
        AssertEx.True(missingText.FileErrors.All(static error => error.Path is null));
    }

    public static void ConfigNormalizeControllerRejectsMissingRequestBody()
    {
        using var temp = TemporaryDirectory.Create();
        var store = new ProxyConfigurationStore();
        var service = CreateReloadService(temp.Path, store);
        var reloadAdministration = new ProxyConfigurationReloadAdministrationService<ProxyConfigurationProjection>(
            service);
        var controller = new ProxyConfigurationController(
            new ProxyConfigurationAdministrationService(
                CreateNormalizer(),
                service),
            CreateReadAdministration(store),
            reloadAdministration);

        var actionResult = controller.Normalize(null);

        var badRequest = (BadRequestObjectResult)AssertEx.NotNull(actionResult.Result);
        var normalize = (ProxyConfigurationNormalizeResponse)AssertEx.NotNull(badRequest.Value);
        AssertEx.False(normalize.Succeeded);
        AssertEx.Equal("unknown", normalize.Format);
        AssertEx.True(normalize.Errors.Any(static error => error.Contains("request body is required", StringComparison.Ordinal)), string.Join("; ", normalize.Errors));
    }

    public static async Task EffectiveConfigResponseRedactsCertificateSecrets()
    {
        using var temp = TemporaryDirectory.Create();
        var certificatePath = Path.Combine(temp.Path, "certs", "home.pfx");
        TestCertificates.WriteSelfSignedPfx(certificatePath, "home.test", "secret");
        WriteHttpsSite(temp.Path, "home.json", port: 18443, upstreamPort: 15000, certificateId: "home-cert");
        WriteOperationalConfig(temp.Path, certificateId: "home-cert", certificatePath: "certs/home.pfx", certificatePassword: "secret");
        var store = new ProxyConfigurationStore();
        var service = CreateReloadService(temp.Path, store);
        var result = await service.ReloadAsync(CancellationToken.None);
        ProxyConfigurationReloadResultAssertions.Reloaded(result, string.Join("; ", result.Errors));

        var reloadAdministration = new ProxyConfigurationReloadAdministrationService<ProxyConfigurationProjection>(
            service);
        var controller = new ProxyConfigurationController(
            new ProxyConfigurationAdministrationService(
                CreateNormalizer(),
                service),
            CreateReadAdministration(store),
            reloadAdministration);
        var actionResult = controller.Effective();
        var ok = (OkObjectResult)AssertEx.NotNull(actionResult.Result);
        var projection = (ProxyConfigurationResponse)AssertEx.NotNull(ok.Value);

        AssertEx.Equal(true, projection.Certificates[0].HasConfiguredPassword);
        AssertEx.False(projection.ToString().Contains("secret", StringComparison.OrdinalIgnoreCase));
    }

    public static async Task ExpiredCertificateProjectionKeepsValidityWindowVisible()
    {
        using var temp = TemporaryDirectory.Create();
        var notBefore = DateTimeOffset.UtcNow.AddDays(-10);
        var notAfter = DateTimeOffset.UtcNow.AddDays(-1);
        var certificatePath = Path.Combine(temp.Path, "certs", "expired.pfx");
        Directory.CreateDirectory(Path.GetDirectoryName(certificatePath)!);
        File.WriteAllBytes(
            certificatePath,
            TestCertificates.CreateSelfSignedPfxBytesForValidity("expired.test", null, notBefore, notAfter));
        WriteHttpsSite(temp.Path, "expired.json", port: 18443, upstreamPort: 15000, certificateId: "expired-cert");
        WriteOperationalConfig(temp.Path, certificateId: "expired-cert", certificatePath: "certs/expired.pfx");

        var result = await CreateLoader(temp.Path).LoadAsync(CancellationToken.None);

        var snapshot = ProxyConfigurationLoadResultAssertions.AssertLoadedSnapshot(result);
        var projection = ProxyConfigurationProjectionMapper.ToProjection(
            snapshot,
            TestHttp3PlatformSupport.Project(snapshot));
        AssertEx.True(projection.Certificates[0].NotAfter < DateTime.UtcNow);
        AssertEx.True(projection.Certificates[0].NotBefore < projection.Certificates[0].NotAfter);
    }

    public static async Task NotYetValidCertificateProjectionKeepsValidityWindowVisible()
    {
        using var temp = TemporaryDirectory.Create();
        var notBefore = DateTimeOffset.UtcNow.AddDays(2);
        var notAfter = DateTimeOffset.UtcNow.AddDays(30);
        var certificatePath = Path.Combine(temp.Path, "certs", "future.pfx");
        Directory.CreateDirectory(Path.GetDirectoryName(certificatePath)!);
        File.WriteAllBytes(
            certificatePath,
            TestCertificates.CreateSelfSignedPfxBytesForValidity("future.test", null, notBefore, notAfter));
        WriteHttpsSite(temp.Path, "future.json", port: 18443, upstreamPort: 15000, certificateId: "future-cert");
        WriteOperationalConfig(temp.Path, certificateId: "future-cert", certificatePath: "certs/future.pfx");

        var result = await CreateLoader(temp.Path).LoadAsync(CancellationToken.None);

        var snapshot = ProxyConfigurationLoadResultAssertions.AssertLoadedSnapshot(result);
        var projection = ProxyConfigurationProjectionMapper.ToProjection(
            snapshot,
            TestHttp3PlatformSupport.Project(snapshot));
        AssertEx.True(projection.Certificates[0].NotBefore > DateTime.UtcNow);
        AssertEx.True(projection.Certificates[0].NotAfter > projection.Certificates[0].NotBefore);
    }

    public static async Task ReloadFailureReportsPerFileErrorAndPreservesActiveConfig()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        var store = new ProxyConfigurationStore();
        var service = CreateReloadService(temp.Path, store);
        var first = await service.ReloadAsync(CancellationToken.None);
        ProxyConfigurationReloadResultAssertions.Reloaded(first);
        var loadedAt = store.Snapshot.LoadedAtUtc;

        File.WriteAllText(Path.Combine(temp.Path, "config", "sites", "broken.json"), "{ nope");
        var second = await service.ReloadAsync(CancellationToken.None);

        ProxyConfigurationReloadResultAssertions.Failed(second);
        AssertEx.Equal(1, store.Snapshot.Version);
        AssertEx.Equal(1, second.ActiveVersion);
        AssertEx.Equal(loadedAt, second.LastSuccessfulLoadAtUtc);
        AssertEx.True(second.FileErrors.Any(error => error.Path?.EndsWith("broken.json", StringComparison.OrdinalIgnoreCase) == true));
    }

    public static async Task ReloadWithInvalidLogPersistenceConfigPreservesActiveSnapshot()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        WriteOperationalConfig(temp.Path, logMaxFileBytes: 8192, logMaxFiles: 2);
        var store = new ProxyConfigurationStore();
        var service = CreateReloadService(temp.Path, store);
        var first = await service.ReloadAsync(CancellationToken.None);
        ProxyConfigurationReloadResultAssertions.Reloaded(first);

        WriteOperationalConfig(temp.Path, logMaxFileBytes: 1024, logMaxFiles: 0);
        var second = await service.ReloadAsync(CancellationToken.None);

        ProxyConfigurationReloadResultAssertions.Failed(second);
        AssertEx.Equal(1, store.Snapshot.Version);
        AssertEx.Equal(8192L, store.Snapshot.Observability.LogPersistence.MaxFileBytes);
        AssertEx.Equal(2, store.Snapshot.Observability.LogPersistence.MaxFiles);
        AssertEx.True(second.Errors.Any(static error => error.Contains("MaxFileBytes", StringComparison.Ordinal)), string.Join("; ", second.Errors));
        AssertEx.True(second.Errors.Any(static error => error.Contains("MaxFiles", StringComparison.Ordinal)), string.Join("; ", second.Errors));
    }

    internal static void WriteSite(string dataDirectory, string fileName, int port, int upstreamPort)
    {
        var sites = Directory.CreateDirectory(Path.Combine(dataDirectory, "config", "sites")).FullName;
        File.WriteAllText(Path.Combine(sites, fileName), SiteJson(Path.GetFileNameWithoutExtension(fileName), port, upstreamPort));
    }

    internal static void WriteCustomSite(string dataDirectory, string fileName, string json)
    {
        var sites = Directory.CreateDirectory(Path.Combine(dataDirectory, "config", "sites")).FullName;
        File.WriteAllText(Path.Combine(sites, fileName), json);
    }

    internal static void WriteYamlSite(string dataDirectory, string fileName, int port, int upstreamPort)
    {
        var sites = Directory.CreateDirectory(Path.Combine(dataDirectory, "config", "sites")).FullName;
        File.WriteAllText(Path.Combine(sites, fileName), YamlSiteText(Path.GetFileNameWithoutExtension(fileName), port, upstreamPort));
    }

    private static string YamlSiteText(string name, int port, int upstreamPort)
    {
        return $$"""
        name: {{name}}
        listeners:
          - name: main
            address: 127.0.0.1
            port: {{port}}
        host: "*"
        pathPrefix: /
        upstreams:
          - name: local-test
            address: 127.0.0.1
            port: {{upstreamPort}}
        """;
    }

    internal static void WriteSiteWithTwoUpstreams(
        string dataDirectory,
        string fileName,
        int port,
        int firstUpstreamPort,
        int secondUpstreamPort,
        bool healthCheckEnabled = false,
        int healthIntervalSeconds = 1,
        int healthTimeoutSeconds = 1,
        int healthyThreshold = 1,
        int unhealthyThreshold = 1)
    {
        var sites = Directory.CreateDirectory(Path.Combine(dataDirectory, "config", "sites")).FullName;
        File.WriteAllText(
            Path.Combine(sites, fileName),
            $$"""
            {
              "name": "{{Path.GetFileNameWithoutExtension(fileName)}}",
              "listeners": [
                {
                  "name": "main",
                  "address": "127.0.0.1",
                  "port": {{port}}
                }
              ],
              "host": "*",
              "pathPrefix": "/",
              "loadBalancingPolicy": "round-robin",
              "healthCheck": {
                "enabled": {{healthCheckEnabled.ToString().ToLowerInvariant()}},
                "path": "/health",
                "intervalSeconds": {{healthIntervalSeconds}},
                "timeoutSeconds": {{healthTimeoutSeconds}},
                "healthyThreshold": {{healthyThreshold}},
                "unhealthyThreshold": {{unhealthyThreshold}}
              },
              "upstreams": [
                {
                  "name": "first",
                  "address": "127.0.0.1",
                  "port": {{firstUpstreamPort}},
                  "weight": 1
                },
                {
                  "name": "second",
                  "address": "127.0.0.1",
                  "port": {{secondUpstreamPort}},
                  "weight": 2
                }
              ]
            }
            """);
    }

    internal static void WriteHttpsSite(
        string dataDirectory,
        string fileName,
        int port,
        int upstreamPort,
        string certificateId,
        string host = "*",
        string sniHost = "home.test",
        bool includeDefault = true,
        bool duplicateSni = false)
    {
        var sites = Directory.CreateDirectory(Path.Combine(dataDirectory, "config", "sites")).FullName;
        var defaultCertificateLine = includeDefault
            ? $"""              "defaultCertificateId": "{certificateId}","""
            : "";
        var duplicateSniBlock = duplicateSni
            ? $$"""
                    ,
                    {
                      "hostName": "{{sniHost}}",
                      "certificateId": "{{certificateId}}"
                    }
            """
            : "";

        File.WriteAllText(
            Path.Combine(sites, fileName),
            $$"""
            {
              "name": "{{Path.GetFileNameWithoutExtension(fileName)}}",
              "listeners": [
                {
                  "name": "main",
                  "address": "127.0.0.1",
                  "port": {{port}},
                  "transport": "https",
                  {{defaultCertificateLine}}
                  "sniCertificates": [
                    {
                      "hostName": "{{sniHost}}",
                      "certificateId": "{{certificateId}}"
                    }{{duplicateSniBlock}}
                  ]
                }
              ],
              "host": "{{host}}",
              "pathPrefix": "/",
              "upstreams": [
                {
                  "name": "local-test",
                  "address": "127.0.0.1",
                  "port": {{upstreamPort}}
                }
              ]
            }
            """);
    }

    internal static void WriteOperationalConfig(
        string dataDirectory,
        int clientRequestHeadTimeoutMs = 1000,
        int clientRequestBodyIdleTimeoutMs = 1000,
        int upstreamConnectTimeoutMs = 1000,
        int upstreamResponseHeadTimeoutMs = 1000,
        int upstreamResponseBodyIdleTimeoutMs = 1000,
        int downstreamWriteTimeoutMs = 1000,
        int tlsHandshakeTimeoutMs = 1000,
        int clientKeepAliveIdleTimeoutMs = 1000,
        int upstreamIdleConnectionLifetimeMs = 1000,
        int tunnelIdleTimeoutMs = 1000,
        int maxRequestsPerClientConnection = 100,
        int maxIdleUpstreamConnectionsPerUpstream = 16,
        int maxActiveUpgradedTunnels = 1024,
        bool accessLogEnabled = true,
        int recentDiagnosticsCapacity = 500,
        bool accessLogFileEnabled = true,
        bool adminAuditLogFileEnabled = true,
        long logMaxFileBytes = 1_048_576,
        int logMaxFiles = 8,
        int maxActiveClientConnections = 4096,
        int maxConcurrentTlsHandshakes = 128,
        int requestsPerMinutePerIp = 240,
        int upgradeRequestsPerMinutePerIp = 30,
        int maxRequestHeadBytes = 32768,
        int maxHeaderCount = 128,
        int maxHeaderLineBytes = 8192,
        long maxRequestBodyBytes = 104857600,
        int maxPathBytes = 8192,
        int shutdownGracePeriodSeconds = 15,
        bool forwardedHeadersEnabled = true,
        string[]? trustedProxies = null,
        string? certificateId = null,
        string? certificatePath = null,
        string? certificatePassword = null,
        string? certificatePasswordEnvironmentVariable = null)
    {
        var configDirectory = Directory.CreateDirectory(Path.Combine(dataDirectory, "config")).FullName;
        var certificatesJson = certificateId is null
            ? "[]"
            : $$"""
            [
                {
                  "id": "{{certificateId}}",
                  "format": "pfx",
                  "path": "{{certificatePath}}"
                  {{(certificatePassword is null ? "" : $""","password": "{certificatePassword}" """)}}
                  {{(certificatePasswordEnvironmentVariable is null ? "" : $""","passwordEnvironmentVariable": "{certificatePasswordEnvironmentVariable}" """)}}
                }
              ]
            """;
        var trustedProxiesJson = trustedProxies is null
            ? "[]"
            : "[" + string.Join(", ", trustedProxies.Select(static proxy => $@"""{proxy}""")) + "]";

        File.WriteAllText(
            Path.Combine(configDirectory, "proxy.json"),
            $$"""
            {
              "timeouts": {
                "clientRequestHeadTimeoutMs": {{clientRequestHeadTimeoutMs}},
                "clientRequestBodyIdleTimeoutMs": {{clientRequestBodyIdleTimeoutMs}},
                "upstreamConnectTimeoutMs": {{upstreamConnectTimeoutMs}},
                "upstreamResponseHeadTimeoutMs": {{upstreamResponseHeadTimeoutMs}},
                "upstreamResponseBodyIdleTimeoutMs": {{upstreamResponseBodyIdleTimeoutMs}},
                "downstreamWriteTimeoutMs": {{downstreamWriteTimeoutMs}},
                "tlsHandshakeTimeoutMs": {{tlsHandshakeTimeoutMs}},
                "clientKeepAliveIdleTimeoutMs": {{clientKeepAliveIdleTimeoutMs}},
                "upstreamIdleConnectionLifetimeMs": {{upstreamIdleConnectionLifetimeMs}},
                "tunnelIdleTimeoutMs": {{tunnelIdleTimeoutMs}}
              },
              "connections": {
                "maxRequestsPerClientConnection": {{maxRequestsPerClientConnection}},
                "maxIdleUpstreamConnectionsPerUpstream": {{maxIdleUpstreamConnectionsPerUpstream}},
                "maxActiveUpgradedTunnels": {{maxActiveUpgradedTunnels}}
              },
              "observability": {
                "accessLogEnabled": {{accessLogEnabled.ToString().ToLowerInvariant()}},
                "recentDiagnosticsCapacity": {{recentDiagnosticsCapacity}},
                "logPersistence": {
                  "accessLogEnabled": {{accessLogFileEnabled.ToString().ToLowerInvariant()}},
                  "adminAuditEnabled": {{adminAuditLogFileEnabled.ToString().ToLowerInvariant()}},
                  "maxFileBytes": {{logMaxFileBytes}},
                  "maxFiles": {{logMaxFiles}}
                }
              },
              "limits": {
                "maxActiveClientConnections": {{maxActiveClientConnections}},
                "maxConcurrentTlsHandshakes": {{maxConcurrentTlsHandshakes}},
                "requestsPerMinutePerIp": {{requestsPerMinutePerIp}},
                "upgradeRequestsPerMinutePerIp": {{upgradeRequestsPerMinutePerIp}},
                "maxRequestHeadBytes": {{maxRequestHeadBytes}},
                "maxHeaderCount": {{maxHeaderCount}},
                "maxHeaderLineBytes": {{maxHeaderLineBytes}},
                "maxRequestBodyBytes": {{maxRequestBodyBytes}},
                "maxPathBytes": {{maxPathBytes}},
                "shutdownGracePeriodSeconds": {{shutdownGracePeriodSeconds}}
              },
              "forwardedHeaders": {
                "enabled": {{forwardedHeadersEnabled.ToString().ToLowerInvariant()}},
                "trustedProxies": {{trustedProxiesJson}}
              },
              "certificates": {{certificatesJson}}
            }
            """);
    }

    private static ProxyConfigurationLoader CreateLoader(
        string dataDirectory,
        TimeProvider? timeProvider = null)
    {
        return new ProxyConfigurationLoader(
            new MdravaDataDirectoryProvider(new MdravaDataDirectoryOptions
            {
                DataDirectory = dataDirectory
            }),
            new ProxyDataDirectoryBootstrapper(new MdravaDataDirectoryProvider(new MdravaDataDirectoryOptions
            {
                DataDirectory = dataDirectory
            })),
            new SiteConfigurationParser(),
            new ProxyAdminUrlPolicy(),
            new ProxyEndpointAddressPolicy(),
            new ProxyRelativeStoragePathPolicy(),
            new ProxyUrlSyntaxPolicy(),
            new ProxyForwardedHeadersAddressPolicy(),
            NullLogger<ProxyConfigurationLoader>.Instance,
            timeProvider ?? TimeProvider.System);
    }

    private static ProxyConfigurationReloadService CreateReloadService(
        string dataDirectory,
        ProxyConfigurationStore store)
    {
        return new ProxyConfigurationReloadService(
            CreateLoader(dataDirectory),
            store,
            store,
            new ResponseCacheStore(TimeProvider.System),
            new ProxyMetrics(),
            ActivatingProxyListenerReloadApplier.Instance,
            SilentProxyConfigurationReloadEventSink.Instance,
            TestHttp3PlatformSupport.ProjectionSource);
    }

    private static ProxyConfigurationReadAdministrationService<ProxyConfigurationProjection> CreateReadAdministration(
        IProxyConfigurationStore store)
    {
        return new ProxyConfigurationReadAdministrationService<ProxyConfigurationProjection>(
            new ProxyConfigurationReadOperations<ProxyConfigurationProjection>(
                new ProxyConfigurationReadProjectionSource(
                    store,
                    TestHttp3PlatformSupport.ProjectionSource)));
    }

    private static ProxyConfigurationNormalizer CreateNormalizer()
    {
        return new ProxyConfigurationNormalizer(
            new ProxyConfigurationNormalizeSiteParser(new SiteConfigurationParser()),
            new ProxyEndpointAddressPolicy(),
            new ProxyUrlSyntaxPolicy());
    }

    private sealed record TestConfigurationProjection(string Name);

    private sealed class FixedNormalizeSiteParser : IProxyConfigurationNormalizeSiteParser
    {
        private readonly ProxyConfigurationNormalizeSiteParseResult _result;

        public FixedNormalizeSiteParser(ProxyConfigurationNormalizeSiteParseResult result)
        {
            _result = result;
        }

        public ProxyConfigurationNormalizeFormat? LastFormat { get; private set; }

        public ProxyConfigurationNormalizeSiteParseResult Parse(
            string text,
            ProxyConfigurationNormalizeFormat format)
        {
            _ = text;
            LastFormat = format;
            return _result;
        }
    }

    private sealed class FixedConfigurationReadProjectionSource<TConfiguration>
        : IProxyConfigurationReadProjectionSource<TConfiguration>
        where TConfiguration : class
    {
        private readonly ProxyConfigurationReadProjectionResult<TConfiguration> _result;

        public FixedConfigurationReadProjectionSource(TConfiguration? projection)
        {
            _result = projection is null
                ? ProxyConfigurationReadProjectionResult<TConfiguration>.MissingConfiguration
                : ProxyConfigurationReadProjectionResult<TConfiguration>.Available(projection);
        }

        public ProxyConfigurationReadProjectionResult<TConfiguration> ReadCurrent()
        {
            return _result;
        }
    }

    private static string SiteJson(string name, int port, int upstreamPort)
    {
        return $$"""
        {
          "name": "{{name}}",
          "listeners": [
            {
              "name": "main",
              "address": "127.0.0.1",
              "port": {{port}}
            }
          ],
          "host": "*",
          "pathPrefix": "/",
          "upstreams": [
            {
              "name": "local-test",
              "address": "127.0.0.1",
              "port": {{upstreamPort}}
            }
          ]
        }
        """;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mdrava-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}

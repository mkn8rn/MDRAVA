using System.Net;
using System.Reflection;
using MDRAVA.API.Controllers;
using MDRAVA.API.Proxy.Configuration;
using MDRAVA.API.Proxy.Configuration.Loading;
using MDRAVA.INF.Configuration.Paths;
using MDRAVA.API.Proxy.Security;
using MDRAVA.BLL.ControlPlane;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace MDRAVA.Tests;

internal static class AdminSecurityTests
{
    private const string AdminToken = "phase-13-admin-token";
    private static readonly string[] KnownAdminEndpointPaths =
    [
        "/admin/proxy/status",
        "/admin/proxy/config/normalize",
        "/admin/proxy/config/reload",
        "/admin/proxy/config/validate",
        "/admin/proxy/config/active",
        "/admin/proxy/config/effective",
        "/admin/proxy/config/lint",
        "/admin/proxy/routes/match",
        "/admin/proxy/diagnostics/recent",
        "/admin/proxy/metrics",
        "/admin/proxy/cache/status",
        "/admin/proxy/cache/clear",
        "/admin/proxy/acme/status",
        "/admin/proxy/audit/recent",
        "/admin/proxy/backup/manifest",
        "/admin/proxy/backup/validate"
    ];

    public static void DefaultAdminBindIsLocalhostOnly()
    {
        var configuration = new ConfigurationBuilder().Build();

        var resolution = AdminBindWebHostConfigurator.Resolve(
            configuration,
            new AdminStartupSecurityOptions([], false, false));

        AssertEx.True(resolution.IsLocalOnly);
        AssertEx.Equal(AdminBindPolicy.DefaultAdminUrl, resolution.Urls[0]);
        AssertEx.True(resolution.ApplyToWebHost);
    }

    public static void NonLocalAdminBindWithoutAuthIsRejected()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [AdminBindWebHostConfigurator.AspNetCoreUrlsConfigurationKey] = "http://0.0.0.0:5041"
            })
            .Build();

        try
        {
            AdminBindWebHostConfigurator.Resolve(configuration, new AdminStartupSecurityOptions([], false, false));
        }
        catch (InvalidOperationException exception)
        {
            AssertEx.True(exception.Message.Contains("authentication", StringComparison.OrdinalIgnoreCase));
            return;
        }

        throw new InvalidOperationException("Expected non-local admin bind without authentication to be rejected.");
    }

    public static void OperationalConfigRejectsNonLocalAdminUrlWithoutAuth()
    {
        var failures = ProxyOperationalOptionsValidationRules.Validate(
            new ProxyOperationalOptions
            {
                Admin = new ProxyAdminOptions
                {
                    Urls = ["http://0.0.0.0:5041"]
                }
            },
            static _ => null);

        AssertEx.True(failures.Any(static failure => failure.Contains("non-local", StringComparison.OrdinalIgnoreCase)));
    }

    public static async Task ProtectedEndpointRejectsMissingAuth()
    {
        var store = CreateStoreWithAdminAuthentication();
        var audit = new AdminAuditStore();
        var context = CreateAdminContext();
        var middleware = CreateMiddleware(store, audit, _ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        AssertEx.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        AssertEx.Equal("missing", audit.Recent(1)[0].AuthResult);
    }

    public static async Task ProtectedEndpointRejectsWrongAuth()
    {
        var store = CreateStoreWithAdminAuthentication();
        var audit = new AdminAuditStore();
        var context = CreateAdminContext();
        context.Request.Headers.Authorization = "Bearer wrong-token";
        var middleware = CreateMiddleware(store, audit, _ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        AssertEx.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        AssertEx.Equal("invalid", audit.Recent(1)[0].AuthResult);
    }

    public static async Task ProtectedEndpointAcceptsValidBearerToken()
    {
        var store = CreateStoreWithAdminAuthentication();
        var audit = new AdminAuditStore();
        var context = CreateAdminContext();
        var nextCalled = false;
        context.Request.Headers.Authorization = $"Bearer {AdminToken}";
        var middleware = CreateMiddleware(
            store,
            audit,
            httpContext =>
            {
                nextCalled = true;
                httpContext.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            });

        await middleware.InvokeAsync(context);

        AssertEx.True(nextCalled);
        AssertEx.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        AssertEx.Equal("valid", audit.Recent(1)[0].AuthResult);
    }

    public static async Task KnownAdminEndpointPathsRequireAuthentication()
    {
        foreach (var path in KnownAdminEndpointPaths)
        {
            var store = CreateStoreWithAdminAuthentication();
            var audit = new AdminAuditStore();
            var context = CreateAdminContext(path);
            var nextCalled = false;
            var middleware = CreateMiddleware(
                store,
                audit,
                _ =>
                {
                    nextCalled = true;
                    return Task.CompletedTask;
                });

            await middleware.InvokeAsync(context);

            AssertEx.False(nextCalled, path);
            AssertEx.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
            AssertEx.Equal("missing", audit.Recent(1)[0].AuthResult);
        }
    }

    public static void KnownAdminEndpointInventoryMatchesControllerRoutes()
    {
        var discovered = DiscoverAdminEndpointPaths();

        AssertEx.Equal(
            string.Join("\n", KnownAdminEndpointPaths.Order(StringComparer.Ordinal)),
            string.Join("\n", discovered.Order(StringComparer.Ordinal)));
    }

    public static async Task KnownAdminEndpointPathsAcceptBearerAndApiKey()
    {
        foreach (var path in KnownAdminEndpointPaths)
        {
            foreach (var useBearer in new[] { true, false })
            {
                var store = CreateStoreWithAdminAuthentication();
                var audit = new AdminAuditStore();
                var context = CreateAdminContext(path);
                if (useBearer)
                {
                    context.Request.Headers.Authorization = $"Bearer {AdminToken}";
                }
                else
                {
                    context.Request.Headers[ProxyAdminAuthenticationPolicy.AdminApiKeyHeaderName] = AdminToken;
                }

                var nextCalled = false;
                var middleware = CreateMiddleware(
                    store,
                    audit,
                    httpContext =>
                    {
                        nextCalled = true;
                        httpContext.Response.StatusCode = StatusCodes.Status204NoContent;
                        return Task.CompletedTask;
                    });

                await middleware.InvokeAsync(context);

                AssertEx.True(nextCalled, path);
                AssertEx.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
                AssertEx.Equal("valid", audit.Recent(1)[0].AuthResult);
            }
        }
    }

    public static async Task AdminAuthFailureResponseAndAuditDoNotExposePresentedSecrets()
    {
        const string badBearer = "bad-bearer-secret";
        const string badApiKey = "bad-api-key-secret";
        var store = CreateStoreWithAdminAuthentication();
        var audit = new AdminAuditStore();
        var context = CreateAdminContext("/admin/proxy/status");
        context.Request.QueryString = new QueryString("?token=query-secret");
        context.Request.Headers.Authorization = $"Bearer {badBearer}";
        context.Request.Headers[ProxyAdminAuthenticationPolicy.AdminApiKeyHeaderName] = badApiKey;
        var middleware = CreateMiddleware(store, audit, _ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        var responseBody = await reader.ReadToEndAsync();
        var auditText = string.Join(Environment.NewLine, audit.Recent(10));
        AssertEx.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        AssertEx.False(responseBody.Contains(badBearer, StringComparison.Ordinal), responseBody);
        AssertEx.False(responseBody.Contains(badApiKey, StringComparison.Ordinal), responseBody);
        AssertEx.False(responseBody.Contains("query-secret", StringComparison.Ordinal), responseBody);
        AssertEx.False(auditText.Contains(badBearer, StringComparison.Ordinal), auditText);
        AssertEx.False(auditText.Contains(badApiKey, StringComparison.Ordinal), auditText);
        AssertEx.False(auditText.Contains("query-secret", StringComparison.Ordinal), auditText);
    }

    public static void AdminAuditCapacityEvictsOldestEntries()
    {
        var audit = new AdminAuditStore();
        audit.Add(Event("/admin/proxy/status", 200), capacity: 2);
        audit.Add(Event("/admin/proxy/config/effective", 200), capacity: 2);
        audit.Add(Event("/admin/proxy/metrics", 200), capacity: 2);

        var recent = audit.Recent(10);

        AssertEx.Equal(2, recent.Count);
        AssertEx.Equal("/admin/proxy/metrics", recent[0].Path);
        AssertEx.Equal("/admin/proxy/config/effective", recent[1].Path);
        AssertEx.False(recent.Any(static item => item.Path == "/admin/proxy/status"));
    }

    public static async Task AdminAuditPathOmitsQuerySecrets()
    {
        var store = CreateStoreWithAdminAuthentication();
        var audit = new AdminAuditStore();
        var context = CreateAdminContext("/admin/proxy/status");
        context.Request.QueryString = new QueryString("?token=query-secret");
        context.Request.Headers.Authorization = $"Bearer {AdminToken}";
        var middleware = CreateMiddleware(
            store,
            audit,
            httpContext =>
            {
                httpContext.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            });

        await middleware.InvokeAsync(context);

        var auditText = string.Join(Environment.NewLine, audit.Recent(10));
        AssertEx.False(auditText.Contains("query-secret", StringComparison.Ordinal));
        AssertEx.Equal("/admin/proxy/status", audit.Recent(1)[0].Path);
    }

    public static void SensitiveProjectionRedactsConfiguredAdminSecrets()
    {
        var store = CreateStoreWithAdminAuthentication();

        var projection = ProxyConfigurationProjectionMapper.ToProjection(store.Snapshot);

        AssertEx.Equal(true, projection.AdminSecurity.RequireAuthentication);
        AssertEx.Equal(true, projection.AdminSecurity.HasConfiguredToken);
        AssertEx.Equal(SecretRedactor.RedactedValue, projection.AdminSecurity.Token);
        AssertEx.False(projection.ToString().Contains(AdminToken, StringComparison.Ordinal));
    }

    public static void EffectiveConfigDoesNotExposeAdminToken()
    {
        var store = CreateStoreWithAdminAuthentication();
        var reloadService = new NoopReloadService();
        var reloadAdministration = new ProxyConfigurationReloadAdministrationService<ProxyConfigurationProjection>(
            reloadService);
        var normalizer = new ProxyConfigurationNormalizer(
            new ProxyConfigurationNormalizeSiteParser(new SiteConfigurationParser()));
        var controller = new ProxyConfigurationController(
            new ProxyConfigurationAdministrationService(normalizer, reloadService),
            CreateReadAdministration(store),
            reloadAdministration);

        var actionResult = controller.Effective();
        var ok = (OkObjectResult)AssertEx.NotNull(actionResult.Result);
        var projection = (ProxyConfigurationProjection)AssertEx.NotNull(ok.Value);

        AssertEx.Equal(SecretRedactor.RedactedValue, projection.AdminSecurity.Token);
        AssertEx.False(projection.ToString().Contains(AdminToken, StringComparison.Ordinal));
    }

    public static void GeneratedPlaceholderConfigDoesNotContainRealSecret()
    {
        using var temp = TemporaryDirectory.Create();
        var provider = new MdravaDataDirectoryProvider(new MdravaDataDirectoryOptions
        {
            DataDirectory = temp.Path
        });
        var bootstrapper = new ProxyDataDirectoryBootstrapper(provider);

        bootstrapper.EnsureLayout();

        var proxyConfig = File.ReadAllText(provider.GetProxyOperationalConfigPath());
        AssertEx.False(proxyConfig.Contains("token", StringComparison.OrdinalIgnoreCase));
        AssertEx.False(proxyConfig.Contains(ProxyAdminSecurityTokenPolicy.DefaultTokenEnvironmentVariable, StringComparison.Ordinal));
    }

    public static async Task AdminAuditDoesNotLogTokenValues()
    {
        var store = CreateStoreWithAdminAuthentication();
        var audit = new AdminAuditStore();
        var context = CreateAdminContext();
        context.Request.Headers.Authorization = $"Bearer {AdminToken}";
        var middleware = CreateMiddleware(
            store,
            audit,
            httpContext =>
            {
                httpContext.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            });

        await middleware.InvokeAsync(context);

        var auditText = string.Join(Environment.NewLine, audit.Recent(10));
        AssertEx.False(auditText.Contains(AdminToken, StringComparison.Ordinal));
    }

    private static AdminAuthenticationMiddleware CreateMiddleware(
        IProxyConfigurationStore store,
        AdminAuditStore audit,
        RequestDelegate next)
    {
        return new AdminAuthenticationMiddleware(
            next,
            store,
            audit,
            NullLogger<AdminAuthenticationMiddleware>.Instance);
    }

    private static DefaultHttpContext CreateAdminContext()
    {
        return CreateAdminContext("/admin/proxy/status");
    }

    private static DefaultHttpContext CreateAdminContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        return context;
    }

    private static IReadOnlyList<string> DiscoverAdminEndpointPaths()
    {
        return typeof(ProxyStatusController).Assembly.GetTypes()
            .Where(static type => typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(static type =>
            {
                var controllerTemplates = type.GetCustomAttributes<RouteAttribute>()
                    .Select(static attribute => NormalizeRouteTemplate(attribute.Template))
                    .ToArray();
                if (controllerTemplates.Length == 0)
                {
                    return [];
                }

                return type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .SelectMany(method => method.GetCustomAttributes()
                        .OfType<IRouteTemplateProvider>()
                        .SelectMany(attribute => controllerTemplates.Select(controller =>
                            CombineRouteTemplates(controller, NormalizeRouteTemplate(attribute.Template)))))
                    .Where(static path => path.StartsWith("/admin/", StringComparison.OrdinalIgnoreCase));
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeRouteTemplate(string? template)
    {
        return string.IsNullOrWhiteSpace(template)
            ? ""
            : template.Trim().Trim('/');
    }

    private static string CombineRouteTemplates(string controllerTemplate, string methodTemplate)
    {
        var combined = string.IsNullOrWhiteSpace(methodTemplate)
            ? controllerTemplate
            : $"{controllerTemplate.TrimEnd('/')}/{methodTemplate.TrimStart('/')}";
        return "/" + combined.Trim('/');
    }

    private static ProxyAdminAuditEvent Event(string path, int statusCode)
    {
        return new ProxyAdminAuditEvent(
            DateTimeOffset.UtcNow,
            "GET",
            path,
            "127.0.0.1",
            "valid",
            statusCode,
            statusCode < 500);
    }

    private static ProxyConfigurationStore CreateStoreWithAdminAuthentication()
    {
        var operationalOptions = new ProxyOperationalOptions
        {
            Admin = new ProxyAdminOptions
            {
                RequireAuthentication = true,
                Token = AdminToken,
                RecentAuditCapacity = 20
            }
        };

        var snapshot = ProxyConfigurationRuntimeMapper.ToRuntimeSnapshot(
            new ProxyOptions(),
            operationalOptions,
            ProxyAdminSecurityTokenPolicy.Resolve(operationalOptions.Admin, static _ => null),
            new Dictionary<string, RuntimeCertificate>(StringComparer.OrdinalIgnoreCase),
            version: 1,
            loadedAtUtc: DateTimeOffset.UtcNow,
            sourceDirectory: "tests",
            sourceFiles: [],
            discovery: new ProxyConfigurationDiscovery(
                new ProxyFilesystemLayout("tests", "tests/config", "tests/config/sites", "tests/logs", "tests/certs", "tests/state", "tests/config/proxy.json"),
                [],
                [],
                []));

        var store = new ProxyConfigurationStore();
        store.Replace(snapshot);
        return store;
    }

    private static ProxyConfigurationReadAdministrationService<ProxyConfigurationProjection> CreateReadAdministration(
        IProxyConfigurationStore store)
    {
        return new ProxyConfigurationReadAdministrationService<ProxyConfigurationProjection>(
            new ProxyConfigurationReadOperations<ProxyConfigurationProjection>(
                new ProxyConfigurationReadProjectionSource(store)));
    }

    private sealed class NoopReloadService
        : IProxyConfigurationReloadOperations<ProxyConfigurationProjection>,
            IProxyConfigurationValidationOperations
    {
        public ValueTask<ProxyConfigurationReloadResult<ProxyConfigurationProjection>> ReloadAsync(
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ProxyConfigurationValidationResult> ValidateAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
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
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mdrava-admin-tests-{Guid.NewGuid():N}");
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

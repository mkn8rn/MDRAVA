using System.Net;
using MDRAVA.API.Controllers;
using MDRAVA.API.Proxy.Configuration;
using MDRAVA.API.Proxy.Configuration.Loading;
using MDRAVA.API.Proxy.Configuration.Paths;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Configuration.Storage;
using MDRAVA.API.Proxy.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MDRAVA.Tests;

internal static class AdminSecurityTests
{
    private const string AdminToken = "phase-13-admin-token";

    public static void DefaultAdminBindIsLocalhostOnly()
    {
        var configuration = new ConfigurationBuilder().Build();

        var resolution = AdminBindPolicy.Resolve(
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
                [AdminBindPolicy.AspNetCoreUrlsConfigurationKey] = "http://0.0.0.0:5041"
            })
            .Build();

        try
        {
            AdminBindPolicy.Resolve(configuration, new AdminStartupSecurityOptions([], false, false));
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
        var failures = ProxyOperationalOptionsValidator.Validate(new ProxyOperationalOptions
        {
            Admin = new ProxyAdminOptions
            {
                Urls = ["http://0.0.0.0:5041"]
            }
        });

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

    public static void SensitiveProjectionRedactsConfiguredAdminSecrets()
    {
        var store = CreateStoreWithAdminAuthentication();

        var projection = ProxyConfigurationMapper.ToProjection(store.Snapshot);

        AssertEx.Equal(true, projection.AdminSecurity.RequireAuthentication);
        AssertEx.Equal(true, projection.AdminSecurity.HasConfiguredToken);
        AssertEx.Equal(SecretRedactor.RedactedValue, projection.AdminSecurity.Token);
        AssertEx.False(projection.ToString().Contains(AdminToken, StringComparison.Ordinal));
    }

    public static void EffectiveConfigDoesNotExposeAdminToken()
    {
        var store = CreateStoreWithAdminAuthentication();
        var controller = new ProxyConfigurationController(
            new NoopReloadService(),
            store,
            new ProxyConfigurationNormalizer(new SiteConfigurationParser(), new ProxyOptionsValidator()));

        var actionResult = controller.Effective();
        var ok = (OkObjectResult)AssertEx.NotNull(actionResult.Result);
        var projection = (ProxyConfigurationProjection)AssertEx.NotNull(ok.Value);

        AssertEx.Equal(SecretRedactor.RedactedValue, projection.AdminSecurity.Token);
        AssertEx.False(projection.ToString().Contains(AdminToken, StringComparison.Ordinal));
    }

    public static void GeneratedPlaceholderConfigDoesNotContainRealSecret()
    {
        using var temp = TemporaryDirectory.Create();
        var provider = new MdravaDataDirectoryProvider(Options.Create(new MdravaDataDirectoryOptions
        {
            DataDirectory = temp.Path
        }));
        var bootstrapper = new ProxyDataDirectoryBootstrapper(provider);

        bootstrapper.EnsureLayout();

        var proxyConfig = File.ReadAllText(provider.GetProxyOperationalConfigPath());
        AssertEx.False(proxyConfig.Contains("token", StringComparison.OrdinalIgnoreCase));
        AssertEx.False(proxyConfig.Contains(AdminSecurityTokenResolver.DefaultTokenEnvironmentVariable, StringComparison.Ordinal));
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
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/admin/proxy/status";
        context.Response.Body = new MemoryStream();
        context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        return context;
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

        var snapshot = ProxyConfigurationMapper.ToRuntimeSnapshot(
            new ProxyOptions(),
            operationalOptions,
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

    private sealed class NoopReloadService : IProxyConfigurationReloadService
    {
        public ValueTask<ProxyConfigurationReloadResult> ReloadAsync(CancellationToken cancellationToken)
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

using MDRAVA.API.Proxy.Configuration.Paths;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Configuration.Storage;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.API.Proxy.Observability;
using MDRAVA.API.Proxy.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MDRAVA.Tests;

internal static class LogPersistenceTests
{
    private const string AdminToken = "phase-47-admin-token";

    public static void AccessLogPersistenceWritesRedactedJsonLine()
    {
        using var temp = TemporaryDirectory.Create();
        var store = CreateStore(temp.Path);
        var writer = CreateWriter(temp.Path, store);
        var metrics = new ProxyMetrics();
        var emitter = new AccessLogEmitter(
            new RecentRequestDiagnosticsStore(metrics),
            metrics,
            NullLogger<AccessLogEmitter>.Instance,
            writer);
        var context = CreateAccessContext("/run?token=query-secret", "Bearer external-secret");

        emitter.Complete(context, accessLogEnabled: true, diagnosticsCapacity: 10);

        var text = ReadLog(temp.Path, "access");
        AssertEx.True(text.Contains("\"kind\":\"access\"", StringComparison.Ordinal), text);
        AssertEx.True(text.Contains("\"targetPath\":\"/run\"", StringComparison.Ordinal), text);
        AssertEx.True(text.Contains("\"protocol\":\"http1\"", StringComparison.Ordinal), text);
        AssertEx.False(text.Contains("query-secret", StringComparison.Ordinal), text);
        AssertEx.False(text.Contains("external-secret", StringComparison.Ordinal), text);
        AssertEx.False(text.Contains("Authorization", StringComparison.OrdinalIgnoreCase), text);
        AssertEx.False(text.Contains("Cookie", StringComparison.OrdinalIgnoreCase), text);
    }

    public static void AccessLogPersistenceHonorsAccessLogDisable()
    {
        using var temp = TemporaryDirectory.Create();
        var store = CreateStore(temp.Path);
        var writer = CreateWriter(temp.Path, store);
        var metrics = new ProxyMetrics();
        var emitter = new AccessLogEmitter(
            new RecentRequestDiagnosticsStore(metrics),
            metrics,
            NullLogger<AccessLogEmitter>.Instance,
            writer);

        emitter.Complete(CreateAccessContext("/disabled"), accessLogEnabled: false, diagnosticsCapacity: 10);

        AssertEx.False(File.Exists(Path.Combine(temp.Path, "logs", "access.log")));
        AssertEx.Equal(0L, metrics.Snapshot().AccessLogsEmitted);
    }

    public static async Task AdminAuditPersistenceWritesFailedAuthWithoutSecrets()
    {
        const string badBearer = "bearer-secret-value";
        const string badApiKey = "api-key-secret-value";
        using var temp = TemporaryDirectory.Create();
        var store = CreateStore(temp.Path, new ProxyLogPersistenceOptions
        {
            AdminAuditEnabled = true,
            AccessLogEnabled = false,
            MaxFileBytes = 8192,
            MaxFiles = 2
        }, requireAdminAuth: true);
        var audit = new AdminAuditStore(CreateWriter(temp.Path, store));
        var context = CreateAdminContext("/admin/proxy/status");
        context.Request.QueryString = new QueryString("?token=query-secret");
        context.Request.Headers.Authorization = $"Bearer {badBearer}";
        context.Request.Headers[AdminAuthenticationMiddleware.AdminApiKeyHeaderName] = badApiKey;
        var middleware = new AdminAuthenticationMiddleware(
            _ => Task.CompletedTask,
            store,
            audit,
            NullLogger<AdminAuthenticationMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        var text = ReadLog(temp.Path, "audit");
        AssertEx.True(text.Contains("\"kind\":\"admin_audit\"", StringComparison.Ordinal), text);
        AssertEx.True(text.Contains("\"authResult\":\"invalid\"", StringComparison.Ordinal), text);
        AssertEx.True(text.Contains("\"status\":403", StringComparison.Ordinal), text);
        AssertEx.True(text.Contains("\"path\":\"/admin/proxy/status\"", StringComparison.Ordinal), text);
        AssertEx.False(text.Contains(badBearer, StringComparison.Ordinal), text);
        AssertEx.False(text.Contains(badApiKey, StringComparison.Ordinal), text);
        AssertEx.False(text.Contains("query-secret", StringComparison.Ordinal), text);
        AssertEx.False(text.Contains(AdminToken, StringComparison.Ordinal), text);
    }

    public static void LogPersistenceCreatesLogsDirectory()
    {
        using var temp = TemporaryDirectory.Create();
        var logsDirectory = Path.Combine(temp.Path, "logs");
        AssertEx.False(Directory.Exists(logsDirectory));
        var writer = CreateWriter(temp.Path, CreateStore(temp.Path));

        writer.WriteAdminAudit(AdminAudit("/admin/proxy/status", 200));

        AssertEx.True(Directory.Exists(logsDirectory));
        AssertEx.True(File.Exists(Path.Combine(logsDirectory, "audit.log")));
    }

    public static void LogPersistenceRotatesAndBoundsFiles()
    {
        using var temp = TemporaryDirectory.Create();
        var store = CreateStore(temp.Path, new ProxyLogPersistenceOptions
        {
            AccessLogEnabled = false,
            AdminAuditEnabled = true,
            MaxFileBytes = 4096,
            MaxFiles = 2
        });
        var writer = CreateWriter(temp.Path, store);

        for (var index = 0; index < 80; index++)
        {
            writer.WriteAdminAudit(AdminAudit($"/admin/proxy/status/{index:D2}", 200));
        }

        var files = Directory.GetFiles(Path.Combine(temp.Path, "logs"), "audit*.log");
        AssertEx.Equal(2, files.Length);
        AssertEx.True(files.Any(static file => Path.GetFileName(file) == "audit.log"));
        AssertEx.True(files.Any(static file => Path.GetFileName(file) == "audit.1.log"));
        AssertEx.False(files.Any(static file => Path.GetFileName(file) == "audit.2.log"));
    }

    public static void LogPersistenceTruncatesLongFields()
    {
        using var temp = TemporaryDirectory.Create();
        var writer = CreateWriter(temp.Path, CreateStore(temp.Path));
        var metrics = new ProxyMetrics();
        var emitter = new AccessLogEmitter(
            new RecentRequestDiagnosticsStore(metrics),
            metrics,
            NullLogger<AccessLogEmitter>.Instance,
            writer);
        var longSegment = new string('a', 700);
        var context = CreateAccessContext("/" + longSegment + "?token=query-secret");

        emitter.Complete(context, accessLogEnabled: true, diagnosticsCapacity: 10);

        var text = ReadLog(temp.Path, "access");
        AssertEx.True(text.Length < 2500, text);
        AssertEx.False(text.Contains("query-secret", StringComparison.Ordinal), text);
        AssertEx.False(text.Contains(new string('a', 600), StringComparison.Ordinal), text);
    }

    public static void LogPersistenceWriteFailureDoesNotCrash()
    {
        using var temp = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(temp.Path, "logs"), "not a directory");
        var writer = CreateWriter(temp.Path, CreateStore(temp.Path));

        writer.WriteAdminAudit(AdminAudit("/admin/proxy/status", 401));

        AssertEx.Equal("not a directory", File.ReadAllText(Path.Combine(temp.Path, "logs")));
    }

    private static ProxyRequestContext CreateAccessContext(string target, string? externalRequestId = null)
    {
        var context = new ProxyRequestContext(
            "request-1",
            "main",
            RuntimeListenerTransport.Http,
            "127.0.0.1:12345",
            1,
            "http1");
        context.SetRequest("GET", "logs.test", target, externalRequestId);
        context.SiteName = "logs";
        context.RouteName = "logs";
        context.RouteAction = "proxy";
        context.UpstreamName = "local";
        context.UpstreamEndpoint = "127.0.0.1:5000";
        context.ResponseStatusCode = 200;
        context.ResponseStarted = true;
        return context;
    }

    private static AdminAuditEvent AdminAudit(string path, int statusCode)
    {
        return new AdminAuditEvent(
            DateTimeOffset.UtcNow,
            "GET",
            path,
            "127.0.0.1",
            statusCode is 401 or 403 ? "missing" : "valid",
            statusCode,
            statusCode < 500);
    }

    private static DefaultHttpContext CreateAdminContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
        return context;
    }

    private static ProxyPersistentLogWriter CreateWriter(string dataDirectory, IProxyConfigurationStore store)
    {
        return new ProxyPersistentLogWriter(
            new MdravaDataDirectoryProvider(Options.Create(new MdravaDataDirectoryOptions
            {
                DataDirectory = dataDirectory
            })),
            store,
            NullLogger<ProxyPersistentLogWriter>.Instance);
    }

    private static ProxyConfigurationStore CreateStore(
        string dataDirectory,
        ProxyLogPersistenceOptions? logPersistence = null,
        bool requireAdminAuth = false)
    {
        var operationalOptions = new ProxyOperationalOptions
        {
            Admin = new ProxyAdminOptions
            {
                RequireAuthentication = requireAdminAuth,
                Token = requireAdminAuth ? AdminToken : null,
                RecentAuditCapacity = 20
            },
            Observability = new ProxyObservabilityOptions
            {
                LogPersistence = logPersistence ?? new ProxyLogPersistenceOptions()
            }
        };
        var snapshot = ProxyConfigurationMapper.ToRuntimeSnapshot(
            new ProxyOptions(),
            operationalOptions,
            new Dictionary<string, RuntimeCertificate>(StringComparer.OrdinalIgnoreCase),
            version: 1,
            loadedAtUtc: DateTimeOffset.UtcNow,
            sourceDirectory: Path.Combine(dataDirectory, "config", "sites"),
            sourceFiles: [],
            discovery: new ProxyConfigurationDiscovery(
                new ProxyFilesystemLayout(
                    dataDirectory,
                    Path.Combine(dataDirectory, "config"),
                    Path.Combine(dataDirectory, "config", "sites"),
                    Path.Combine(dataDirectory, "logs"),
                    Path.Combine(dataDirectory, "certs"),
                    Path.Combine(dataDirectory, "state"),
                    Path.Combine(dataDirectory, "config", "proxy.json")),
                [],
                [],
                []));
        var store = new ProxyConfigurationStore();
        store.Replace(snapshot);
        return store;
    }

    private static string ReadLog(string dataDirectory, string logName)
    {
        return File.ReadAllText(Path.Combine(dataDirectory, "logs", $"{logName}.log"));
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
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mdrava-log-tests-{Guid.NewGuid():N}");
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

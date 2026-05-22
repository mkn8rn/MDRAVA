using System.Text;
using System.Text.Json;
using MDRAVA.BLL.Infrastructure;
using MDRAVA.API.Proxy.Configuration.Storage;

namespace MDRAVA.API.Proxy.Observability;

public sealed class ProxyPersistentLogWriter
{
    private const int MaxTextLength = 256;
    private const int MaxPathLength = 512;
    private static readonly Encoding LogEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly IMdravaDataDirectoryProvider _dataDirectoryProvider;
    private readonly IProxyConfigurationStore _configurationStore;
    private readonly ILogger<ProxyPersistentLogWriter> _logger;
    private readonly object _accessGate = new();
    private readonly object _auditGate = new();
    private readonly object _statusGate = new();
    private DateTimeOffset? _lastSuccessfulWriteAtUtc;
    private ProxyLogPersistenceFailureStatus? _lastWriteFailure;

    public ProxyPersistentLogWriter(
        IMdravaDataDirectoryProvider dataDirectoryProvider,
        IProxyConfigurationStore configurationStore,
        ILogger<ProxyPersistentLogWriter> logger)
    {
        _dataDirectoryProvider = dataDirectoryProvider;
        _configurationStore = configurationStore;
        _logger = logger;
    }

    public void WriteAccess(ProxyRequestContext context, ProxyRequestDiagnosticEvent diagnostic)
    {
        if (!TryGetOptions(out var options) || !options.AccessLogEnabled)
        {
            return;
        }

        var entry = new
        {
            timestampUtc = diagnostic.TimestampUtc,
            kind = "access",
            requestId = SafeValue(diagnostic.RequestId),
            configVersion = diagnostic.ConfigVersion,
            listener = SafeValue(diagnostic.ListenerName),
            transport = SafeValue(diagnostic.Transport?.ToLowerInvariant()),
            protocol = SafeValue(context.Protocol),
            method = SafeValue(diagnostic.Method),
            host = SafeValue(diagnostic.Host),
            targetPath = SafeTargetPath(diagnostic.Target),
            site = SafeValue(context.SiteName),
            route = SafeValue(diagnostic.RouteName),
            action = SafeValue(context.RouteAction),
            upstream = SafeValue(diagnostic.UpstreamName),
            upstreamEndpoint = SafeValue(diagnostic.UpstreamEndpoint),
            status = diagnostic.ResponseStatusCode,
            durationMs = diagnostic.DurationMilliseconds,
            failure = SafeValue(diagnostic.FailureKind),
            responseStarted = diagnostic.ResponseStarted,
            keepAlive = diagnostic.KeepClientConnectionOpen,
            upgrade = diagnostic.IsUpgrade,
            tunnel = diagnostic.TunnelEstablished
        };

        WriteLine("access", JsonSerializer.Serialize(entry, JsonOptions), options, _accessGate);
    }

    public void WriteAdminAudit(AdminAuditEvent auditEvent)
    {
        if (!TryGetOptions(out var options) || !options.AdminAuditEnabled)
        {
            return;
        }

        var entry = new
        {
            timestampUtc = auditEvent.TimestampUtc,
            kind = "admin_audit",
            method = SafeValue(auditEvent.Method),
            path = SafeTargetPath(auditEvent.Path),
            authResult = SafeValue(auditEvent.AuthResult),
            status = auditEvent.StatusCode,
            succeeded = auditEvent.Succeeded
        };

        WriteLine("audit", JsonSerializer.Serialize(entry, JsonOptions), options, _auditGate);
    }

    public ProxyLogPersistenceStatus GetStatus()
    {
        var logDirectory = SafeValue(_dataDirectoryProvider.GetLogsDirectory(), MaxPathLength);
        var hasSnapshot = _configurationStore.TryGetSnapshot(out var snapshot) && snapshot is not null;
        var options = hasSnapshot
            ? snapshot!.Observability.LogPersistence
            : new RuntimeLogPersistenceOptions(
                AccessLogEnabled: false,
                AdminAuditEnabled: false,
                MaxFileBytes: 0,
                MaxFiles: 0);

        DateTimeOffset? lastSuccess;
        ProxyLogPersistenceFailureStatus? lastFailure;
        lock (_statusGate)
        {
            lastSuccess = _lastSuccessfulWriteAtUtc;
            lastFailure = _lastWriteFailure;
        }

        var state = ProxyStatusText.Healthy;
        var reason = ProxyStatusText.Ready;
        if (!hasSnapshot)
        {
            state = ProxyStatusText.Unknown;
            reason = ProxyStatusText.NoActiveConfig;
        }
        else if (!options.AccessLogEnabled && !options.AdminAuditEnabled)
        {
            state = ProxyStatusText.Disabled;
            reason = ProxyStatusText.Disabled;
        }
        else if (lastFailure is not null && (lastSuccess is null || lastFailure.TimestampUtc >= lastSuccess))
        {
            state = ProxyStatusText.Degraded;
            reason = ProxyStatusText.LastWriteFailed;
        }

        return new ProxyLogPersistenceStatus(
            options.AccessLogEnabled,
            options.AdminAuditEnabled,
            logDirectory,
            options.MaxFileBytes,
            options.MaxFiles,
            state,
            reason,
            lastSuccess,
            lastFailure);
    }

    private bool TryGetOptions(out RuntimeLogPersistenceOptions options)
    {
        if (_configurationStore.TryGetSnapshot(out var snapshot) && snapshot is not null)
        {
            options = snapshot.Observability.LogPersistence;
            return true;
        }

        options = new RuntimeLogPersistenceOptions(
            AccessLogEnabled: false,
            AdminAuditEnabled: false,
            MaxFileBytes: 1_048_576,
            MaxFiles: 8);
        return false;
    }

    private void WriteLine(
        string logName,
        string line,
        RuntimeLogPersistenceOptions options,
        object gate)
    {
        try
        {
            lock (gate)
            {
                var logsDirectory = _dataDirectoryProvider.GetLogsDirectory();
                Directory.CreateDirectory(logsDirectory);
                var path = Path.Combine(logsDirectory, $"{logName}.log");
                var lineBytes = LogEncoding.GetByteCount(line) + LogEncoding.GetByteCount(Environment.NewLine);
                RotateIfNeeded(path, options.MaxFileBytes, options.MaxFiles, lineBytes);
                File.AppendAllText(path, line + Environment.NewLine, LogEncoding);
                RecordWriteSuccess();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            RecordWriteFailure(logName, exception);
            _logger.LogWarning(exception, "Failed to persist {LogName} log entry.", logName);
        }
    }

    private void RecordWriteSuccess()
    {
        lock (_statusGate)
        {
            _lastSuccessfulWriteAtUtc = DateTimeOffset.UtcNow;
        }
    }

    private void RecordWriteFailure(string logName, Exception exception)
    {
        var reason = exception is UnauthorizedAccessException ? "access_denied" : "io_error";
        var category = string.Equals(logName, "audit", StringComparison.OrdinalIgnoreCase) ? "admin_audit" : "access";
        var failure = new ProxyLogPersistenceFailureStatus(DateTimeOffset.UtcNow, category, reason);
        lock (_statusGate)
        {
            _lastWriteFailure = failure;
        }
    }

    private static void RotateIfNeeded(string path, long maxFileBytes, int maxFiles, int nextLineBytes)
    {
        if (!File.Exists(path) || new FileInfo(path).Length + nextLineBytes <= maxFileBytes)
        {
            return;
        }

        if (maxFiles <= 1)
        {
            File.Delete(path);
            return;
        }

        for (var index = maxFiles - 1; index >= 1; index--)
        {
            var destination = RotatedPath(path, index);
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }

            var source = index == 1 ? path : RotatedPath(path, index - 1);
            if (File.Exists(source))
            {
                File.Move(source, destination);
            }
        }
    }

    private static string RotatedPath(string path, int index)
    {
        var directory = Path.GetDirectoryName(path)!;
        var fileName = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        return Path.Combine(directory, $"{fileName}.{index}{extension}");
    }

    private static string? SafeTargetPath(string? value)
    {
        var safe = SafeValue(value, MaxPathLength);
        if (string.IsNullOrWhiteSpace(safe))
        {
            return safe;
        }

        var queryIndex = safe.IndexOfAny(['?', '#']);
        return queryIndex >= 0 ? safe[..queryIndex] : safe;
    }

    private static string? SafeValue(string? value, int maxLength = MaxTextLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var builder = new StringBuilder(Math.Min(trimmed.Length, maxLength));
        foreach (var character in trimmed)
        {
            if (builder.Length >= maxLength)
            {
                break;
            }

            builder.Append(char.IsControl(character) ? ' ' : character);
        }

        return builder.Length == 0 ? null : builder.ToString();
    }
}

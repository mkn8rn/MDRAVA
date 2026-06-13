using MDRAVA.BLL.ControlPlane.Status;

using BusinessProxyLogPersistenceFailureStatus = MDRAVA.BLL.ControlPlane.Status.ProxyLogPersistenceFailureStatus;
using BusinessProxyLogPersistenceStatus = MDRAVA.BLL.ControlPlane.Status.ProxyLogPersistenceStatus;

namespace MDRAVA.API.Controllers;

public sealed record ProxyLogPersistenceStatusResponse
{
    private ProxyLogPersistenceStatusResponse(
        bool accessLogEnabled,
        bool adminAuditEnabled,
        string? logDirectory,
        long maxFileBytes,
        int maxFiles,
        string state,
        string reason,
        DateTimeOffset? lastSuccessfulWriteAtUtc,
        ProxyLogPersistenceFailureStatusResponse? lastWriteFailure)
    {
        AccessLogEnabled = accessLogEnabled;
        AdminAuditEnabled = adminAuditEnabled;
        LogDirectory = logDirectory;
        MaxFileBytes = maxFileBytes;
        MaxFiles = maxFiles;
        State = state;
        Reason = reason;
        LastSuccessfulWriteAtUtc = lastSuccessfulWriteAtUtc;
        LastWriteFailure = lastWriteFailure;
    }

    public bool AccessLogEnabled { get; }

    public bool AdminAuditEnabled { get; }

    public string? LogDirectory { get; }

    public long MaxFileBytes { get; }

    public int MaxFiles { get; }

    public string State { get; }

    public string Reason { get; }

    public DateTimeOffset? LastSuccessfulWriteAtUtc { get; }

    public ProxyLogPersistenceFailureStatusResponse? LastWriteFailure { get; }

    public static ProxyLogPersistenceStatusResponse FromStatus(BusinessProxyLogPersistenceStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return new ProxyLogPersistenceStatusResponse(
            status.AccessLogEnabled,
            status.AdminAuditEnabled,
            status.LogDirectory,
            status.MaxFileBytes,
            status.MaxFiles,
            status.State,
            status.Reason,
            status.LastSuccessfulWriteAtUtc,
            status.LastWriteFailure is null
                ? null
                : ProxyLogPersistenceFailureStatusResponse.FromStatus(status.LastWriteFailure));
    }
}

public sealed record ProxyLogPersistenceFailureStatusResponse(
    DateTimeOffset TimestampUtc,
    string Category,
    string Reason)
{
    public static ProxyLogPersistenceFailureStatusResponse FromStatus(
        BusinessProxyLogPersistenceFailureStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return new ProxyLogPersistenceFailureStatusResponse(
            status.TimestampUtc,
            status.Category,
            status.Reason);
    }
}

namespace MDRAVA.BLL.ControlPlane.Status;

public sealed record ProxyLogPersistenceStatus(
    bool AccessLogEnabled,
    bool AdminAuditEnabled,
    string? LogDirectory,
    long MaxFileBytes,
    int MaxFiles,
    string State,
    string Reason,
    DateTimeOffset? LastSuccessfulWriteAtUtc,
    ProxyLogPersistenceFailureStatus? LastWriteFailure)
{
    public static ProxyLogPersistenceStatus Unknown { get; } = new(
        AccessLogEnabled: false,
        AdminAuditEnabled: false,
        LogDirectory: null,
        MaxFileBytes: 0,
        MaxFiles: 0,
        ProxyStatusText.Unknown,
        ProxyStatusText.NotAvailable,
        LastSuccessfulWriteAtUtc: null,
        LastWriteFailure: null);
}

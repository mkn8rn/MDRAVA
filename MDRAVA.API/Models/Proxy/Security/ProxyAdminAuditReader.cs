namespace MDRAVA.API.Proxy.Security;

public sealed class ProxyAdminAuditReader : IProxyAdminAuditReader
{
    private readonly AdminAuditStore _auditStore;

    public ProxyAdminAuditReader(AdminAuditStore auditStore)
    {
        _auditStore = auditStore;
    }

    public IReadOnlyList<ProxyAdminAuditEvent> Recent(int limit)
    {
        return _auditStore.Recent(limit);
    }
}

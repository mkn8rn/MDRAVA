using MDRAVA.API.Proxy.Observability;

namespace MDRAVA.API.Proxy.Security;

public sealed class AdminAuditStore
{
    public const int MaximumReadLimit = 500;

    private readonly ProxyPersistentLogWriter? _persistentLogWriter;
    private readonly object _gate = new();
    private readonly LinkedList<AdminAuditEvent> _events = new();

    public AdminAuditStore(ProxyPersistentLogWriter? persistentLogWriter = null)
    {
        _persistentLogWriter = persistentLogWriter;
    }

    public void Add(AdminAuditEvent auditEvent, int capacity)
    {
        lock (_gate)
        {
            var boundedCapacity = Math.Max(1, capacity);
            _events.AddFirst(auditEvent);

            while (_events.Count > boundedCapacity)
            {
                _events.RemoveLast();
            }
        }

        _persistentLogWriter?.WriteAdminAudit(auditEvent);
    }

    public IReadOnlyList<AdminAuditEvent> Recent(int limit)
    {
        var boundedLimit = Math.Clamp(limit, 1, MaximumReadLimit);
        List<AdminAuditEvent> results = [];

        lock (_gate)
        {
            var current = _events.First;
            while (current is not null && results.Count < boundedLimit)
            {
                results.Add(current.Value);
                current = current.Next;
            }
        }

        return results;
    }
}

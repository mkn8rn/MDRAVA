namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed class ProxyRouteDiagnosticsRequestInput
{
    private readonly List<RouteMatchDryRunFinding> _findings;

    public ProxyRouteDiagnosticsRequestInput(
        string scheme,
        string? protocol,
        string? listenerName,
        int? port,
        string target,
        string path,
        ProxyRouteDiagnosticsRequestHead requestHead,
        bool isUpgradeRequest,
        IReadOnlyList<RouteMatchDryRunFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(requestHead);
        ArgumentNullException.ThrowIfNull(findings);

        Scheme = scheme;
        Protocol = protocol;
        ListenerName = listenerName;
        Port = port;
        Target = target;
        Path = path;
        RequestHead = requestHead;
        IsUpgradeRequest = isUpgradeRequest;
        _findings = new List<RouteMatchDryRunFinding>(findings);
        Findings = _findings.AsReadOnly();
    }

    public string Scheme { get; }

    public string? Protocol { get; }

    public string? ListenerName { get; }

    public int? Port { get; }

    public string Target { get; }

    public string Path { get; }

    public ProxyRouteDiagnosticsRequestHead RequestHead { get; }

    public bool IsUpgradeRequest { get; }

    public IReadOnlyList<RouteMatchDryRunFinding> Findings { get; }

    public void AddFinding(RouteMatchDryRunFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);

        _findings.Add(finding);
    }
}

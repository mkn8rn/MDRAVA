using MDRAVA.BLL.ControlPlane.Forwarding;

namespace MDRAVA.BLL.ControlPlane.RequestDiagnostics;

public sealed class ProxyRequestContext
{
    private readonly TimeProvider _timeProvider;
    private readonly long _startedTimestamp;

    public ProxyRequestContext(
        string requestId,
        string listenerName,
        string transport,
        string? clientEndpoint,
        int configVersion,
        TimeProvider timeProvider,
        string protocol = "http1")
    {
        _timeProvider = timeProvider;
        _startedTimestamp = timeProvider.GetTimestamp();
        RequestId = requestId;
        ListenerName = listenerName;
        Transport = transport;
        ClientEndpoint = clientEndpoint;
        ConfigVersion = configVersion;
        Protocol = protocol;
        StartedAtUtc = timeProvider.GetUtcNow();
    }

    public string RequestId { get; }

    public string? ExternalRequestId { get; set; }

    public string ListenerName { get; }

    public string Transport { get; }

    public string Protocol { get; }

    public string? ClientEndpoint { get; private set; }

    public int ConfigVersion { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public string? Method { get; set; }

    public string? Host { get; set; }

    public string? Target { get; set; }

    public string? RouteName { get; set; }

    public string? SiteName { get; set; }

    public string? RouteAction { get; set; }

    public string? UpstreamName { get; set; }

    public string? UpstreamEndpoint { get; set; }

    public int? ResponseStatusCode { get; set; }

    public bool ResponseStarted { get; set; }

    public bool KeepClientConnectionOpen { get; set; }

    public bool IsUpgrade { get; set; }

    public bool TunnelEstablished { get; set; }

    public string? TunnelCloseReason { get; set; }

    public long TunnelBytesClientToUpstream { get; set; }

    public long TunnelBytesUpstreamToClient { get; set; }

    public ProxyFailureKind FailureKind { get; set; } = ProxyFailureKind.None;

    public bool? AccessLogEnabled { get; set; }

    public TimeSpan Elapsed => _timeProvider.GetElapsedTime(_startedTimestamp);

    public void SetRequest(string method, string host, string target, string? externalRequestId)
    {
        Method = method;
        Host = host;
        Target = target;
        ExternalRequestId = externalRequestId;
    }

    public void SetRoute(ProxyRequestRoute route)
    {
        RouteName = route.Name;
        SiteName = route.SiteName;
        RouteAction = route.Action;
        AccessLogEnabled = route.AccessLogEnabled;
    }

    public void SetRouteAction(string action)
    {
        RouteAction = action;
    }

    public void SetUpstream(ProxyRequestUpstream upstream)
    {
        UpstreamName = upstream.Name;
        UpstreamEndpoint = upstream.Endpoint;
    }

    public void SetClientEndpoint(string? clientEndpoint)
    {
        ClientEndpoint = clientEndpoint;
    }

}

public sealed record ProxyRequestRoute(
    string Name,
    string SiteName,
    string Action,
    bool AccessLogEnabled);

public sealed record ProxyRequestUpstream(
    string Name,
    string Endpoint);

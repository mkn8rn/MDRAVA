using MDRAVA.API.Proxy.Configuration.Runtime;

namespace MDRAVA.API.Models.Observability;

public sealed class ProxyRequestContext
{
    private readonly long _startedTimestamp = TimeProvider.System.GetTimestamp();

    public ProxyRequestContext(
        string requestId,
        string listenerName,
        RuntimeListenerTransport transport,
        string? clientEndpoint,
        int configVersion)
    {
        RequestId = requestId;
        ListenerName = listenerName;
        Transport = transport.ToString();
        ClientEndpoint = clientEndpoint;
        ConfigVersion = configVersion;
        StartedAtUtc = DateTimeOffset.UtcNow;
    }

    public string RequestId { get; }

    public string? ExternalRequestId { get; set; }

    public string ListenerName { get; }

    public string Transport { get; }

    public string? ClientEndpoint { get; private set; }

    public int ConfigVersion { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public string? Method { get; set; }

    public string? Host { get; set; }

    public string? Target { get; set; }

    public string? RouteName { get; set; }

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

    public TimeSpan Elapsed => TimeProvider.System.GetElapsedTime(_startedTimestamp);

    public void SetRequest(string method, string host, string target, string? externalRequestId)
    {
        Method = method;
        Host = host;
        Target = target;
        ExternalRequestId = externalRequestId;
    }

    public void SetRoute(RuntimeRoute route)
    {
        RouteName = route.Name;
        AccessLogEnabled = route.ResolvedOptions.AccessLogEnabled;
    }

    public void SetUpstream(RuntimeUpstream upstream)
    {
        UpstreamName = upstream.Name;
        UpstreamEndpoint = upstream.Endpoint;
    }

    public void SetClientEndpoint(string? clientEndpoint)
    {
        ClientEndpoint = clientEndpoint;
    }
}

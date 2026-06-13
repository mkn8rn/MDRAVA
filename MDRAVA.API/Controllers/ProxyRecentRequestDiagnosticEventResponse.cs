using BusinessProxyRecentRequestDiagnosticEvent =
    MDRAVA.BLL.ControlPlane.RequestDiagnostics.ProxyRecentRequestDiagnosticEvent;

namespace MDRAVA.API.Controllers;

public sealed record ProxyRecentRequestDiagnosticEventResponse(
    DateTimeOffset TimestampUtc,
    string RequestId,
    string? ExternalRequestId,
    int ConfigVersion,
    string ListenerName,
    string Transport,
    string? ClientEndpoint,
    string? Method,
    string? Host,
    string? Target,
    string? RouteName,
    string? UpstreamName,
    string? UpstreamEndpoint,
    int? ResponseStatusCode,
    long DurationMilliseconds,
    string FailureKind,
    bool ResponseStarted,
    bool KeepClientConnectionOpen,
    bool IsUpgrade,
    bool TunnelEstablished,
    string? TunnelCloseReason,
    long TunnelBytesClientToUpstream,
    long TunnelBytesUpstreamToClient)
{
    public static IReadOnlyList<ProxyRecentRequestDiagnosticEventResponse> FromEvents(
        IReadOnlyList<BusinessProxyRecentRequestDiagnosticEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        return events.Select(FromEvent).ToArray();
    }

    private static ProxyRecentRequestDiagnosticEventResponse FromEvent(
        BusinessProxyRecentRequestDiagnosticEvent diagnosticEvent)
    {
        ArgumentNullException.ThrowIfNull(diagnosticEvent);

        return new ProxyRecentRequestDiagnosticEventResponse(
            diagnosticEvent.TimestampUtc,
            diagnosticEvent.RequestId,
            diagnosticEvent.ExternalRequestId,
            diagnosticEvent.ConfigVersion,
            diagnosticEvent.ListenerName,
            diagnosticEvent.Transport,
            diagnosticEvent.ClientEndpoint,
            diagnosticEvent.Method,
            diagnosticEvent.Host,
            diagnosticEvent.Target,
            diagnosticEvent.RouteName,
            diagnosticEvent.UpstreamName,
            diagnosticEvent.UpstreamEndpoint,
            diagnosticEvent.ResponseStatusCode,
            diagnosticEvent.DurationMilliseconds,
            diagnosticEvent.FailureKind,
            diagnosticEvent.ResponseStarted,
            diagnosticEvent.KeepClientConnectionOpen,
            diagnosticEvent.IsUpgrade,
            diagnosticEvent.TunnelEstablished,
            diagnosticEvent.TunnelCloseReason,
            diagnosticEvent.TunnelBytesClientToUpstream,
            diagnosticEvent.TunnelBytesUpstreamToClient);
    }
}

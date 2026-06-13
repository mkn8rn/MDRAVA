using BusinessProxySubsystemSummaries = MDRAVA.BLL.ControlPlane.Status.ProxySubsystemSummaries;

namespace MDRAVA.API.Controllers;

public sealed record ProxySubsystemSummariesResponse(
    ProxyConfigSubsystemSummaryResponse Config,
    ProxyListenerSubsystemSummaryResponse Listeners,
    ProxyRouteSubsystemSummaryResponse Routes,
    ProxyCertificateSubsystemSummaryResponse Certificates,
    ProxyAcmeSubsystemSummaryResponse Acme,
    ProxyUpstreamSubsystemSummaryResponse Upstreams,
    ProxyCacheSubsystemSummaryResponse Cache,
    ProxyCircuitSubsystemSummaryResponse Circuits,
    ProxyLimitSubsystemSummaryResponse Limits,
    ProxyLogSubsystemSummaryResponse Logs,
    ProxyShutdownSubsystemSummaryResponse Shutdown,
    ProxyProtocolSubsystemSummaryResponse Protocols)
{
    public static ProxySubsystemSummariesResponse FromSummaries(BusinessProxySubsystemSummaries summaries)
    {
        ArgumentNullException.ThrowIfNull(summaries);

        return new ProxySubsystemSummariesResponse(
            ProxyConfigSubsystemSummaryResponse.FromSummary(summaries.Config),
            ProxyListenerSubsystemSummaryResponse.FromSummary(summaries.Listeners),
            ProxyRouteSubsystemSummaryResponse.FromSummary(summaries.Routes),
            ProxyCertificateSubsystemSummaryResponse.FromSummary(summaries.Certificates),
            ProxyAcmeSubsystemSummaryResponse.FromSummary(summaries.Acme),
            ProxyUpstreamSubsystemSummaryResponse.FromSummary(summaries.Upstreams),
            ProxyCacheSubsystemSummaryResponse.FromSummary(summaries.Cache),
            ProxyCircuitSubsystemSummaryResponse.FromSummary(summaries.Circuits),
            ProxyLimitSubsystemSummaryResponse.FromSummary(summaries.Limits),
            ProxyLogSubsystemSummaryResponse.FromSummary(summaries.Logs),
            ProxyShutdownSubsystemSummaryResponse.FromSummary(summaries.Shutdown),
            ProxyProtocolSubsystemSummaryResponse.FromSummary(summaries.Protocols));
    }
}

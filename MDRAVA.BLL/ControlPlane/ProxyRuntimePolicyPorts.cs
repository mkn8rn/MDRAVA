using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane;

public interface IProxyUpstreamSelectionMetricsSink
{
    void UpstreamSelected(RuntimeUpstream upstream);

    void NoHealthyUpstream();

    void NoAvailableUpstream();
}

public interface IProxyCircuitBreakerMetricsSink
{
    void CircuitOpened(RuntimeUpstream upstream);

    void CircuitHalfOpened(RuntimeUpstream upstream);

    void CircuitClosed(RuntimeUpstream upstream);

    void CircuitRejected(RuntimeUpstream upstream);
}

public interface IProxyUpstreamHealthMetricsSink
{
    void UpstreamHealthTransition();

    void UpstreamRequestFailed(RuntimeUpstream upstream);
}

public interface IProxyHttp3AltSvcMetricsSink
{
    void Http3AltSvcEmitted();

    void Http3AltSvcSuppressed();
}

public interface IProxyRequestDiagnosticsMetricsSink
{
    void RecentDiagnosticOverwritten();
}

public interface IUpstreamConnectionPruner
{
    void PruneIdleConnections(RuntimeUpstream upstream);
}

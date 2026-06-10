using MDRAVA.BLL.ControlPlane;

namespace MDRAVA.BLL.ControlPlane.RequestDiagnostics;

public interface IProxyAccessLogMetricsSink
{
    void RequestFailed(ProxyFailureKind failureKind);

    void RequestCompleted(string? site, string? route, string? action, int? statusCode);

    void AccessLogEmitted();
}

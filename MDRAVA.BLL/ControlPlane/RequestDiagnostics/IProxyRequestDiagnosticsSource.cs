namespace MDRAVA.BLL.ControlPlane.RequestDiagnostics;

public interface IProxyRequestDiagnosticsSource
{
    IReadOnlyList<ProxyRequestDiagnosticSourceEvent> Recent(int limit);
}

namespace MDRAVA.BLL.ControlPlane.RequestDiagnostics;

public interface IProxyRequestDiagnosticsSource
{
    IReadOnlyList<ProxyRecentRequestDiagnosticEvent> Recent(int limit);
}

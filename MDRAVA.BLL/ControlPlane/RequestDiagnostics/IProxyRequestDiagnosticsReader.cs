namespace MDRAVA.BLL.ControlPlane.RequestDiagnostics;

public interface IProxyRequestDiagnosticsReader
{
    IReadOnlyList<ProxyRecentRequestDiagnosticEvent> Recent(int limit);
}

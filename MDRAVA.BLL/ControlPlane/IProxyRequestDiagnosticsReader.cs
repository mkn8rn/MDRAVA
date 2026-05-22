namespace MDRAVA.BLL.ControlPlane;

public interface IProxyRequestDiagnosticsReader
{
    IReadOnlyList<ProxyRecentRequestDiagnosticEvent> Recent(int limit);
}

namespace MDRAVA.BLL.ControlPlane;

public interface IProxyRequestDiagnosticsSource
{
    IReadOnlyList<ProxyRequestDiagnosticSourceEvent> Recent(int limit);
}

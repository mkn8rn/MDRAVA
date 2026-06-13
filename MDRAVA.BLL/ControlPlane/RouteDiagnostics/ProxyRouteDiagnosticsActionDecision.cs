namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed record ProxyRouteDiagnosticsActionDecision(
    bool ShouldProxy,
    int? GeneratedStatusCode)
{
    public static ProxyRouteDiagnosticsActionDecision Proxy { get; } = new(
        ShouldProxy: true,
        GeneratedStatusCode: null);

    public static ProxyRouteDiagnosticsActionDecision GeneratedResponse(int statusCode)
    {
        return new ProxyRouteDiagnosticsActionDecision(
            ShouldProxy: false,
            statusCode);
    }
}

namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed record RouteDiagnosticsStatus(bool Available)
{
    public static RouteDiagnosticsStatus Enabled { get; } = new(true);
}

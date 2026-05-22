namespace MDRAVA.BLL.ControlPlane;

public sealed record RouteDiagnosticsStatus(bool Available)
{
    public static RouteDiagnosticsStatus Enabled { get; } = new(true);
}

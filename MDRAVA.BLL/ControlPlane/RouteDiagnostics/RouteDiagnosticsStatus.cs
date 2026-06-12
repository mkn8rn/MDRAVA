namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed record RouteDiagnosticsStatus
{
    public static RouteDiagnosticsStatus Enabled { get; } = new(available: true);

    private RouteDiagnosticsStatus(bool available)
    {
        Available = available;
    }

    public bool Available { get; }
}

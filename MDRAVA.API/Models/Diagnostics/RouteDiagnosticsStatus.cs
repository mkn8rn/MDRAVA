namespace MDRAVA.API.Models.Diagnostics;

public sealed record RouteDiagnosticsStatus(bool Available)
{
    public static RouteDiagnosticsStatus Enabled { get; } = new(true);
}

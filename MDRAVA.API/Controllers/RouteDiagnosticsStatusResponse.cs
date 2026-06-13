using BusinessRouteDiagnosticsStatus = MDRAVA.BLL.ControlPlane.RouteDiagnostics.RouteDiagnosticsStatus;

namespace MDRAVA.API.Controllers;

public sealed record RouteDiagnosticsStatusResponse(bool Available)
{
    public static RouteDiagnosticsStatusResponse FromStatus(BusinessRouteDiagnosticsStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return new RouteDiagnosticsStatusResponse(status.Available);
    }
}

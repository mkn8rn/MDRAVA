using System.Globalization;
using MDRAVA.BLL.ControlPlane.RequestDiagnostics;

namespace MDRAVA.INF.Runtime;

public sealed class SystemRequestIdRuntimeIdentitySource : IProxyRequestIdRuntimeIdentitySource
{
    public string RuntimeIdentity => Environment.ProcessId.ToString("x", CultureInfo.InvariantCulture);
}

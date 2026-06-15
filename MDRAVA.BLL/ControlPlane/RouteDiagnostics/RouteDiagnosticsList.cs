using System.Collections.ObjectModel;

namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

internal static class RouteDiagnosticsList
{
    public static IReadOnlyList<T> Copy<T>(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return new ReadOnlyCollection<T>(values.ToArray());
    }
}

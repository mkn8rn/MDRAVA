using System.Collections.ObjectModel;

namespace MDRAVA.BLL.ControlPlane.Metrics;

internal static class MetricsList
{
    public static ReadOnlyCollection<T> Copy<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new ReadOnlyCollection<T>(values.ToArray());
    }
}

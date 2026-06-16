using System.Collections.ObjectModel;

namespace MDRAVA.BLL.ControlPlane.Listeners;

internal static class ProxyListenerList
{
    public static IReadOnlyList<T> Copy<T>(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return new ReadOnlyCollection<T>(values.ToArray());
    }
}

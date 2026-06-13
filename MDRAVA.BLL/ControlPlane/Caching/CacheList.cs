using System.Collections.ObjectModel;

namespace MDRAVA.BLL.ControlPlane.Caching;

internal static class CacheList
{
    public static ReadOnlyCollection<T> Copy<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return new ReadOnlyCollection<T>(values.ToArray());
    }
}

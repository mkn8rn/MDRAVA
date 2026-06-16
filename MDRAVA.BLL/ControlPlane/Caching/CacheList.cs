using System.Collections.ObjectModel;

namespace MDRAVA.BLL.ControlPlane.Caching;

internal static class CacheList
{
    public static ReadOnlyCollection<T> Copy<T>(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return new ReadOnlyCollection<T>(values.Select(RequireValue).ToArray());
    }

    private static T RequireValue<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value;
    }
}

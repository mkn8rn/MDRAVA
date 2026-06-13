using System.Collections.ObjectModel;

namespace MDRAVA.BLL.Configuration;

internal static class RuntimeList
{
    public static ReadOnlyCollection<T> Copy<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new ReadOnlyCollection<T>(values.ToArray());
    }
}

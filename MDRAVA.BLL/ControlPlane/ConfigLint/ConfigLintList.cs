using System.Collections.ObjectModel;

namespace MDRAVA.BLL.ControlPlane.ConfigLint;

internal static class ConfigLintList
{
    public static ReadOnlyCollection<T> Copy<T>(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return new ReadOnlyCollection<T>(values.ToArray());
    }
}

using System.Collections.ObjectModel;

namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

internal static class ConfigurationManagementList
{
    public static ReadOnlyCollection<T> Copy<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return new ReadOnlyCollection<T>(values.ToArray());
    }
}

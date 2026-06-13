using System.Collections.ObjectModel;

namespace MDRAVA.BLL.ControlPlane.Backup;

internal static class BackupList
{
    public static ReadOnlyCollection<T> Copy<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new ReadOnlyCollection<T>(values.ToArray());
    }
}

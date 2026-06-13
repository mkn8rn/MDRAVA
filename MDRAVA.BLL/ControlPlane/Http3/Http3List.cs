using System.Collections.ObjectModel;

namespace MDRAVA.BLL.ControlPlane.Http3;

internal static class Http3List
{
    public static ReadOnlyCollection<T> Copy<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return new ReadOnlyCollection<T>(values.ToArray());
    }
}

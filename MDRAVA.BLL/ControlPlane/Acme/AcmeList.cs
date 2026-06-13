using System.Collections.ObjectModel;

namespace MDRAVA.BLL.ControlPlane.Acme;

internal static class AcmeList
{
    public static ReadOnlyCollection<T> Copy<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return new ReadOnlyCollection<T>(values.ToArray());
    }
}

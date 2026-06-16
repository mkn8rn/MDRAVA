using System.Collections.ObjectModel;

namespace MDRAVA.BLL.Http;

internal static class ProxyHeaderFieldList
{
    public static IReadOnlyList<ProxyHeaderField> Copy(IEnumerable<ProxyHeaderField> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        return new ReadOnlyCollection<ProxyHeaderField>(headers.ToArray());
    }
}

using System.Collections.ObjectModel;

namespace MDRAVA.BLL.ControlPlane.Metrics;

internal static class MetricsList
{
    public static ReadOnlyCollection<T> Copy<T>(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new ReadOnlyCollection<T>(values.Select(RequireValue).ToArray());
    }

    public static ReadOnlyDictionary<TKey, TValue> CopyDictionary<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> values,
        IEqualityComparer<TKey>? comparer = null)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(values);
        return new ReadOnlyDictionary<TKey, TValue>(new Dictionary<TKey, TValue>(values, comparer));
    }

    private static T RequireValue<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value;
    }
}

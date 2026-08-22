namespace RandomForeseer.RandomForeseerCode.Common;

internal static class EnumerableExtensions
{
    /// <summary>
    /// Returns a read-only list containing the elements of the source sequence.
    /// If the source is already a read-only list, it is returned as-is; otherwise, a new list is created.
    /// </summary>
    public static IReadOnlyList<T> Materialize<T>(this IEnumerable<T> source)
    {
        return source as IReadOnlyList<T> ?? [.. source];
    }
}

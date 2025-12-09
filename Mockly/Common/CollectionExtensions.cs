namespace Mockly.Common;

internal static class CollectionExtensions
{
    /// <summary>
    /// Asynchronously returns the first element of a sequence that satisfies the specified asynchronous predicate,
    /// or a default value if no such element is found.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source collection.</typeparam>
    /// <param name="source">The collection to search.</param>
    /// <param name="predicate">An asynchronous function to test each element for a condition.</param>
    public static async Task<T?> FirstOrDefaultAsync<T>(
        this IEnumerable<T> source,
        Func<T, Task<bool>> predicate)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        foreach (var item in source)
        {
            if (await predicate(item))
            {
                return item;
            }
        }

        return default;
    }
}

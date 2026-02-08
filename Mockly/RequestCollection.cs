using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Mockly;

/// <summary>
/// Represents a collection of captured HTTP requests.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Collection suffix is appropriate for this class")]
public class RequestCollection : IEnumerable<CapturedRequest>
{
    private readonly List<CapturedRequest> requests = new();

    /// <summary>
    /// Adds a captured request to the collection.
    /// </summary>
    internal void Add(CapturedRequest request)
    {
        requests.Add(request);
        request.Sequence = requests.Count;
    }

    /// <summary>
    /// Gets the count of captured requests.
    /// </summary>
    public int Count => requests.Count;

    /// <summary>
    /// Checks if the collection is empty.
    /// </summary>
    public bool IsEmpty => requests.Count == 0;

    /// <summary>
    /// Checks if any unexpected requests were captured.
    /// </summary>
    public bool HasUnexpectedRequests => requests.Any(r => !r.WasExpected);

    /// <summary>
    /// Returns the first captured request, or null if none exist.
    /// </summary>
    public CapturedRequest? First() => requests.FirstOrDefault();

    public IEnumerator<CapturedRequest> GetEnumerator() => requests.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => $"{requests.Count} captured request(s)";
}

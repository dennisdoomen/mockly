using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockly.Http;

/// <summary>
/// Represents a collection of captured HTTP requests.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", 
    Justification = "Collection suffix is appropriate for this class")]
public class RequestCollection
{
    private readonly List<CapturedRequest> requests = new();

    /// <summary>
    /// Gets all captured requests.
    /// </summary>
    public IReadOnlyList<CapturedRequest> Requests => requests.AsReadOnly();

    /// <summary>
    /// Adds a captured request to the collection.
    /// </summary>
    internal void Add(CapturedRequest request)
    {
        requests.Add(request);
    }

    /// <summary>
    /// Gets the first captured request, or null if none exist.
    /// </summary>
    public CapturedRequest? First()
    {
        return requests.FirstOrDefault();
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
}

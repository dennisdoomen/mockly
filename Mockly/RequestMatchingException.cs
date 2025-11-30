using System.Text.Json;

namespace Mockly;

/// <summary>
/// Exception thrown when a request cannot be matched to any configured expectations.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032:Implement standard exception constructors",
    Justification = "Not all constructors are needed for this exception type")]
public class RequestMatchingException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RequestMatchingException"/> class.
    /// </summary>
    public RequestMatchingException(string message, JsonException jsonException)
        : base(message, jsonException)
    {
    }
}

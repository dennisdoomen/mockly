namespace Mockly;

/// <summary>
/// Exception thrown when the system under test sends a request that does not comply with the OpenAPI document
/// registered through <see cref="HttpMockOpenApiExtensions.UsingOpenApi"/>.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032:Implement standard exception constructors",
    Justification = "Not all constructors are needed for this exception type")]
public class RequestContractViolationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RequestContractViolationException"/> class.
    /// </summary>
    public RequestContractViolationException(string message)
        : base(message)
    {
    }
}

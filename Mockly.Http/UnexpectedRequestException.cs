using System;

namespace Mockly.Http;

/// <summary>
/// Exception thrown when an unexpected HTTP request is received.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032:Implement standard exception constructors", 
    Justification = "Not all constructors are needed for this exception type")]
public class UnexpectedRequestException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnexpectedRequestException"/> class.
    /// </summary>
    public UnexpectedRequestException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnexpectedRequestException"/> class.
    /// </summary>
    public UnexpectedRequestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

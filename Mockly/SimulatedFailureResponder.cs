namespace Mockly;

/// <summary>
/// Dedicated responder that represents an intentionally simulated transport failure.
/// </summary>
/// <remarks>
/// Unlike a regular responder, the exception produced by this responder is propagated to the
/// <see cref="System.Net.Http.HttpClient"/> caller by the HTTP pipeline instead of being turned into a
/// <c>500 Internal Server Error</c> response. This allows tests to verify retry, circuit-breaker and other
/// resilience behavior.
/// </remarks>
internal sealed class SimulatedFailureResponder
{
    private readonly Func<Exception> exceptionFactory;

    public SimulatedFailureResponder(Func<Exception> exceptionFactory)
    {
        this.exceptionFactory = exceptionFactory;
    }

    /// <summary>
    /// Creates the exception to throw for a matching request.
    /// </summary>
    public Exception CreateException() => exceptionFactory();
}

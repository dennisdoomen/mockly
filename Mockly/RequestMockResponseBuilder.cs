namespace Mockly;

/// <summary>
/// Fluent builder for configuring invocation limits on a configured mock response.
/// </summary>
public class RequestMockResponseBuilder
{
    private readonly RequestMock requestMock;

    internal RequestMockResponseBuilder(RequestMock requestMock)
    {
        this.requestMock = requestMock;
    }

    /// <summary>
    /// Limits this mock to be used exactly once.
    /// </summary>
    public RequestMockResponseBuilder Once()
    {
        requestMock.MaxInvocations = 1;
        return this;
    }

    /// <summary>
    /// Limits this mock to be used exactly twice.
    /// </summary>
    public RequestMockResponseBuilder Twice()
    {
        requestMock.MaxInvocations = 2;
        return this;
    }

    /// <summary>
    /// Limits this mock to be used exactly the specified number of times.
    /// </summary>
    public RequestMockResponseBuilder Times(uint count)
    {
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Cannot limit the number of mock invocations to less than 1");
        }

        requestMock.MaxInvocations = count;
        return this;
    }

    /// <summary>
    /// Delays the response by the specified <paramref name="delay"/> before it is produced, simulating a slow endpoint.
    /// </summary>
    /// <remarks>
    /// The delay is awaited on the asynchronous response path and honors the <see cref="CancellationToken"/> flowing
    /// from the HTTP pipeline. If the request is cancelled (for example through <see cref="System.Net.Http.HttpClient.Timeout"/>
    /// or an externally cancelled token) while the delay is in progress, a
    /// <see cref="System.Threading.Tasks.TaskCanceledException"/> is thrown, just as a real <see cref="System.Net.Http.HttpClient"/> would.
    /// </remarks>
    /// <param name="delay">The amount of time to wait before producing the response.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="delay"/> is negative.</exception>
    public RequestMockResponseBuilder After(TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Cannot delay a response by a negative amount of time");
        }

        requestMock.Delay = delay;
        return this;
    }
}

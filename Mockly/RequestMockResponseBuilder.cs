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
}

#if NET472_OR_GREATER
using System.Net.Http;
#endif

namespace Mockly;

/// <summary>
/// Fluent builder for configuring invocation limits and response headers on a configured mock response.
/// </summary>
public class RequestMockResponseBuilder
{
    internal static readonly HashSet<string> ContentHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Allow",
        "Content-Disposition",
        "Content-Encoding",
        "Content-Language",
        "Content-Length",
        "Content-Location",
        "Content-MD5",
        "Content-Range",
        "Content-Type",
        "Expires",
        "Last-Modified",
    };

    private readonly RequestMock requestMock;

    internal RequestMockResponseBuilder(RequestMock requestMock)
    {
        this.requestMock = requestMock;
    }

    /// <summary>
    /// Gets the underlying <see cref="RequestMock"/> so the assertion packages can inspect
    /// per-mock invocation state such as <see cref="RequestMock.InvocationCount"/> and
    /// <see cref="RequestMock.MaxInvocations"/>.
    /// </summary>
    internal RequestMock RequestMock => requestMock;

    /// <summary>
    /// Limits the most recently configured response in the sequence to be used exactly once.
    /// </summary>
    public RequestMockResponseBuilder Once()
    {
        requestMock.SetCurrentResponseCount(1);
        return this;
    }

    /// <summary>
    /// Limits the most recently configured response in the sequence to be used exactly twice.
    /// </summary>
    public RequestMockResponseBuilder Twice()
    {
        requestMock.SetCurrentResponseCount(2);
        return this;
    }

    /// <summary>
    /// Limits the most recently configured response in the sequence to be used exactly the specified number of times.
    /// </summary>
    public RequestMockResponseBuilder Times(uint count)
    {
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Cannot limit the number of mock invocations to less than 1");
        }

        requestMock.SetCurrentResponseCount(count);
        return this;
    }

    /// <summary>
    /// Adds a header with the specified name and value to every response produced by this mock.
    /// </summary>
    /// <remarks>
    /// Content headers such as <c>Content-Type</c> or <c>Content-Length</c> are routed to the response's
    /// content headers; all other headers are added to the response headers. If a content header is
    /// requested but the response has no content, an empty content is created to host the header.
    /// </remarks>
    /// <param name="name">The name of the header.</param>
    /// <param name="value">The value of the header.</param>
    public RequestMockResponseBuilder WithHeader(string name, string value)
    {
        return WithHeader(name, [value]);
    }

    /// <summary>
    /// Adds a header with the specified name and one or more values to every response produced by this mock.
    /// </summary>
    /// <remarks>
    /// Content headers such as <c>Content-Type</c> or <c>Content-Length</c> are routed to the response's
    /// content headers; all other headers are added to the response headers. If a content header is
    /// requested but the response has no content, an empty content is created to host the header.
    /// </remarks>
    /// <param name="name">The name of the header.</param>
    /// <param name="values">The values of the header.</param>
    public RequestMockResponseBuilder WithHeader(string name, params string[] values)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("The header name cannot be empty or consist solely of white-space.", nameof(name));
        }

        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        string[] capturedValues = [.. values];

        requestMock.AppendResponseMutator(response => ApplyHeader(response, name, capturedValues));

        return this;
    }

    internal static void ApplyHeader(HttpResponseMessage response, string name, string[] values)
    {
        if (ContentHeaderNames.Contains(name))
        {
#if NET472_OR_GREATER
            response.Content ??= new ByteArrayContent([]);
#endif
            response.Content.Headers.Remove(name);
            response.Content.Headers.TryAddWithoutValidation(name, values);
        }
        else
        {
            response.Headers.Remove(name);
            response.Headers.TryAddWithoutValidation(name, values);
        }
    }
}


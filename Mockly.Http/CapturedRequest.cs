using System;
using System.Net.Http;

namespace Mockly.Http;

/// <summary>
/// Represents a captured HTTP request with metadata.
/// </summary>
public class CapturedRequest
{
    /// <summary>
    /// The captured HTTP request message.
    /// </summary>
    public HttpRequestMessage Request { get; internal set; } = new();

    /// <summary>
    /// The HTTP response that was generated.
    /// </summary>
    public HttpResponseMessage Response { get; internal set; } = new();

    /// <summary>
    /// The mock that handled this request, if any.
    /// </summary>
    internal RequestMock? Mock { get; set; }

    /// <summary>
    /// Indicates whether this request was expected (matched a mock).
    /// </summary>
    public bool WasExpected { get; internal set; }

    /// <summary>
    /// Timestamp when the request was captured.
    /// </summary>
    public DateTime Timestamp { get; internal set; }

    /// <summary>
    /// Gets the path of the request.
    /// </summary>
    public string Path => Request.RequestUri?.AbsolutePath ?? string.Empty;

    /// <summary>
    /// Gets the query string of the request.
    /// </summary>
    public string Query => Request.RequestUri?.Query ?? string.Empty;

    /// <summary>
    /// Gets the HTTP method of the request.
    /// </summary>
    public HttpMethod Method => Request.Method;
}

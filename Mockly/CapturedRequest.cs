using System.Net.Http;
using System.Net.Http.Headers;

namespace Mockly;

/// <summary>
/// Represents a captured HTTP request with metadata.
/// </summary>
public class CapturedRequest(RequestInfo request)
{
    /// <summary>
    /// The HTTP response that was generated.
    /// </summary>
    public HttpResponseMessage Response { get; internal set; } = new();

    /// <summary>
    /// Gets the exception that was thrown to simulate a network-level failure for this request, or <c>null</c>
    /// when the request produced a regular response.
    /// </summary>
    /// <remarks>
    /// This is set when the matching mock was configured with one of the simulated-failure methods such as
    /// <c>ThrowsException</c> or <c>TimesOut</c>. When set, the HTTP pipeline propagates this exception to the
    /// <see cref="HttpClient"/> caller, and <see cref="Response"/> does not represent a response that was actually
    /// returned to the caller.
    /// </remarks>
    public Exception? SimulatedFailure { get; internal set; }

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
    /// Gets the host of the request.
    /// </summary>
    public string Host => request.Uri?.Host ?? string.Empty;

    /// <summary>
    /// Gets the path of the request.
    /// </summary>
    public string Path => request.Uri?.AbsolutePath ?? string.Empty;

    /// <summary>
    /// Gets the query string of the request.
    /// </summary>
    public string Query => request.Uri?.Query ?? string.Empty;

    /// <summary>
    /// Gets or sets the HTTP method of the captured request, such as GET, POST, PUT, or DELETE.
    /// </summary>
    public HttpMethod Method
    {
        get => request.Method;
    }

    /// <summary>
    /// Gets the scheme of the mocked HTTP request, such as "http" or "https".
    /// </summary>
    public string Scheme => request.Uri?.Scheme ?? string.Empty;

    /// <summary>
    /// Gets the URI associated with the captured HTTP request.
    /// </summary>
    public Uri? Uri
    {
        get => request.Uri;
    }

    /// <summary>
    /// Gets the body content of the captured HTTP request.
    /// </summary>
    public string? Body
    {
        get => request.Body;
    }

    /// <summary>
    /// The collection of HTTP request headers associated with the captured request.
    /// </summary>
    public HttpRequestHeaders Headers
    {
        get => request.Headers;
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Represents a collection of options associated with the captured HTTP request.
    /// This includes custom properties that can be set or retrieved during request processing.
    /// </summary>
    public HttpRequestOptions Options
    {
        get => request.Options;
    }

    /// <summary>
    /// Gets or sets the HTTP version policy to be used for the request.
    /// </summary>
    public HttpVersionPolicy VersionPolicy
    {
        get => request.VersionPolicy;
    }
#endif

    /// <summary>
    /// Gets or sets the HTTP protocol version for the request.
    /// </summary>
    public Version Version
    {
        get => request.Version;
    }

    /// <summary>
    /// The sequence number of the captured request, starting at 1.
    /// </summary>
    public int Sequence { get; set; }

    public override string ToString()
    {
        return $"{Method} {Scheme}://{Host}{Path}{Query}";
    }

}

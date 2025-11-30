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

    public override string ToString()
    {
        string route = $"{Method} {Scheme}://{Host}{Path}{Query}";

        return route;
    }

}

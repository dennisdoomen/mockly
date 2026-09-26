using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Mockly.Common;

namespace Mockly;

/// <summary>
/// Provides information about a request that was captured by a mock.
/// </summary>
public class RequestInfo
{
    private readonly HttpRequestMessage request;

    public RequestInfo(HttpRequestMessage request, byte[]? rawBody)
    {
        this.request = request;
        RawBody = rawBody;
    }

    /// <summary>
    /// When set, forces <see cref="Body"/> to be populated from <see cref="RawBody"/> regardless of whether
    /// <see cref="IsBodyLikelyTextual"/> recognizes the Content-Type as textual. Set internally by
    /// <see cref="HttpMock"/> when a registered mock opted in via
    /// <see cref="RequestMockBuilder.TreatBodyAsTextual"/>.
    /// </summary>
    internal bool ForceTextualBody { get; init; }

    /// <summary>
    /// The <see cref="System.Text.Json.JsonSerializerOptions"/> configured for the matched mock via
    /// <see cref="RequestMockBuilder.Using(JsonSerializerOptions)"/>. Set internally by <see cref="RequestMock"/>
    /// once a mock has matched. Used by <see cref="BodyAs{T}"/>.
    /// </summary>
    internal JsonSerializerOptions? JsonSerializerOptions { get; set; }

    /// <summary>
    /// The named route values captured from a <c>{name}</c> placeholder in the path template supplied to
    /// <see cref="RequestMockBuilder.WithPath"/>, if any. Set internally by <see cref="RequestMock"/> once a mock
    /// with a matching path template has matched.
    /// </summary>
    internal IReadOnlyDictionary<string, string>? RouteValues { get; set; }

    /// <summary>
    /// Gets the URI of the HTTP request, representing the full address, including the scheme, host, path, and query string, if present.
    /// </summary>
    public Uri? Uri => request.RequestUri;

    public string? Body => DeserializeBodyIfTextual();

    /// <summary>
    /// The request body as raw bytes, if prefetched.
    /// </summary>
    public byte[]? RawBody { get; }

    /// <summary>
    /// The content type of the request body, if any.
    /// </summary>
    public string? ContentType => request.Content?.Headers.ContentType?.MediaType;

    public HttpRequestHeaders Headers
    {
        get => request.Headers;
    }

    public HttpMethod Method
    {
        get => request.Method;
        set => request.Method = value;
    }

#if !NET8_0_OR_GREATER
    public IDictionary<string, object?> Properties
    {
        get => request.Properties;
    }
#endif

#if NET8_0_OR_GREATER
    public HttpRequestOptions Options
    {
        get => request.Options;
    }

    public HttpVersionPolicy VersionPolicy
    {
        get => request.VersionPolicy;
        set => request.VersionPolicy = value;
    }
#endif

    public Version Version
    {
        get => request.Version;
        set => request.Version = value;
    }

    /// <summary>
    /// Determines if the request body is likely to be textual based on the Content-Type header.
    /// </summary>
    /// <returns>
    /// Returns true if the Content-Type indicates a textual media type (e.g., text/*, application/json,
    /// application/xml, or other known textual types). Returns false otherwise.
    /// </returns>
    public bool IsBodyLikelyTextual()
    {
        string? mediaType = ContentType;

        if (mediaType == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return false;
        }

#pragma warning disable CA1308
        mediaType = mediaType.Trim().ToLowerInvariant();
#pragma warning restore CA1308

        // Any text/* is textual
        if (mediaType.StartsWith("text/", StringComparison.Ordinal))
        {
            return true;
        }

        // multipart/* (e.g. multipart/mixed, multipart/related, multipart/form-data) bodies used by
        // batch requests (such as OData $batch) are themselves text envelopes around their parts, even
        // though individual parts could contain binary content.
        if (mediaType.StartsWith("multipart/", StringComparison.Ordinal))
        {
            return true;
        }

        // RFC 6839 structured syntax suffixes
        if (mediaType.EndsWith("+json", StringComparison.Ordinal) ||
            mediaType.EndsWith("+xml", StringComparison.Ordinal) ||
            mediaType.EndsWith("+yaml", StringComparison.Ordinal) ||
            mediaType.EndsWith("+yml", StringComparison.Ordinal) ||
            mediaType.EndsWith("+csv", StringComparison.Ordinal))
        {
            return true;
        }

        // Common "application/*" textual types
        return mediaType is
            "application/json" or
            "application/xml" or
            "application/xhtml+xml" or
            "application/javascript" or
            "application/ecmascript" or
            "application/x-www-form-urlencoded" or
            "application/graphql" or
            "application/sql";
    }

    /// <summary>
    /// Deserializes the raw request body as JSON into <typeparamref name="T"/>, using the
    /// <see cref="System.Text.Json.JsonSerializerOptions"/> configured for the matched mock via
    /// <see cref="RequestMockBuilder.Using(JsonSerializerOptions)"/>.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the body into.</typeparam>
    /// <returns>
    /// The deserialized object, or the default value of <typeparamref name="T"/> when there is no prefetched body.
    /// </returns>
    public T? BodyAs<T>()
    {
        if (RawBody is null || RawBody.Length == 0)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(RawBody, JsonSerializerOptions);
    }

    /// <summary>
    /// Gets the non-empty path segment at the specified zero-based <paramref name="index"/>, or <c>null</c> when
    /// the path has fewer segments. For example, for <c>/api/users/123</c>, <c>PathSegment(2)</c> returns <c>"123"</c>.
    /// </summary>
    /// <param name="index">The zero-based index of the path segment to retrieve.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
    public string? PathSegment(int index)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        string path = WebUtility.UrlDecode(Uri?.AbsolutePath ?? string.Empty);
        string[] segments = path.Split(['/'], StringSplitOptions.RemoveEmptyEntries);

        return index < segments.Length ? segments[index] : null;
    }

    /// <summary>
    /// Gets the value of the specified query string parameter, or <c>null</c> when it isn't present. The name is
    /// matched case-insensitively, and when multiple values are present for the same name, the first is returned.
    /// </summary>
    /// <param name="name">The name of the query parameter to retrieve.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <c>null</c>.</exception>
    public string? QueryValue(string name)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        foreach (KeyValuePair<string, string> pair in Uri?.Query.ParseUrlEncoded() ?? [])
        {
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the value of the named route segment captured from a <c>{name}</c> placeholder in the path template
    /// supplied to <see cref="RequestMockBuilder.WithPath"/>, or <c>null</c> when no template was used or the
    /// request didn't match one.
    /// </summary>
    /// <param name="name">The name of the route placeholder to retrieve.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <c>null</c>.</exception>
    public string? RouteValue(string name)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        return RouteValues is not null && RouteValues.TryGetValue(name, out string? value) ? value : null;
    }

    /// <summary>
    /// Deserializes the raw body byte array into a textual representation if it is likely to be textual, or if
    /// <see cref="ForceTextualBody"/> is <c>true</c>.
    /// </summary>
    private string? DeserializeBodyIfTextual()
    {
        if (RawBody is null || RawBody.Length == 0 || (!IsBodyLikelyTextual() && !ForceTextualBody))
        {
            return null;
        }

        Encoding encoding = GetEncoding() ?? Encoding.UTF8;
        return encoding.GetString(RawBody);
    }

    private Encoding? GetEncoding()
    {
        string? charset = request.Content?.Headers.ContentType?.CharSet;
        if (string.IsNullOrWhiteSpace(charset))
        {
            return null;
        }

        try
        {
            return Encoding.GetEncoding(charset);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}

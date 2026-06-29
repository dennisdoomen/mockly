using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

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
        Body = DeserializeBodyIfTextual(rawBody);
    }

    /// <summary>
    /// Gets the URI of the HTTP request, representing the full address, including the scheme, host, path, and query string, if present.
    /// </summary>
    public Uri? Uri => request.RequestUri;

    public string? Body { get; }

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
    /// Deserializes the provided raw body byte array into a textual representation if it is likely to be textual.
    /// </summary>
    private string? DeserializeBodyIfTextual(byte[]? rawBody)
    {
        if (rawBody is null || rawBody.Length == 0 || !IsBodyLikelyTextual())
        {
            return null;
        }

        Encoding encoding = GetEncoding() ?? Encoding.UTF8;
        return encoding.GetString(rawBody);
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

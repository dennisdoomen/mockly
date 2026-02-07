using System.Net.Http;
using System.Net.Http.Headers;

namespace Mockly;

public class RequestInfo(HttpRequestMessage request, string? body)
{
    public Uri? Uri => request.RequestUri;

    public string? Body { get; } = body;

    /// <summary>
    /// The content type of the request body, if any.
    /// </summary>
    public string? ContentType { get; } = request.Content?.Headers.ContentType?.MediaType;

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
}

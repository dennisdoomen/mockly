using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Mockly;

internal enum RecordingValuePolicy
{
    RedactSensitive,
    KeepSensitive
}

internal sealed class HttpArchive
{
    public HttpArchiveLog Log { get; init; } = new();
}

#pragma warning disable SA1516
internal sealed class HttpArchiveLog
{
    public string Version { get; init; } = "1.2";
    public List<HttpArchiveEntry> Entries { get; init; } = [];
}

internal sealed class HttpArchiveEntry
{
    public HttpArchiveRequest Request { get; init; } = new();
    public HttpArchiveResponse Response { get; init; } = new();
    public DateTime StartedDateTime { get; init; }
}

internal sealed class HttpArchiveRequest
{
    public string Method { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public List<HttpArchiveHeader> Headers { get; init; } = [];
    public HttpArchivePostData? PostData { get; init; }
}

internal sealed class HttpArchiveResponse
{
    public int Status { get; init; }
    public string StatusText { get; init; } = string.Empty;
    public List<HttpArchiveHeader> Headers { get; init; } = [];
    public HttpArchiveContent Content { get; init; } = new();
}

internal sealed class HttpArchiveHeader
{
    public string Name { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

internal sealed class HttpArchivePostData
{
    public string? MimeType { get; init; }
    public string? Text { get; init; }
}

internal sealed class HttpArchiveContent
{
    public int Size { get; init; }
    public string? MimeType { get; init; }
    public string? Text { get; init; }
    public string? Encoding { get; init; }
}

internal static class HttpArchiveConverter
{
    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Cookie",
        "Proxy-Authorization",
        "Set-Cookie"
    };

    public static async Task<HttpArchiveEntry> CreateEntryAsync(HttpRequestMessage request, HttpResponseMessage response,
        RecordingValuePolicy valuePolicy)
    {
        byte[] responseBody = response.Content is null
            ? []
            : await response.Content.ReadAsByteArrayAsync();
        byte[]? requestBody = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync();

        return new HttpArchiveEntry
        {
            StartedDateTime = DateTime.UtcNow,
            Request = new HttpArchiveRequest
            {
                Method = request.Method.Method,
                Url = request.RequestUri?.ToString() ?? string.Empty,
                Headers = GetHeaders(request, valuePolicy),
                PostData = request.Content is null
                    ? null
                    : new HttpArchivePostData
                    {
                        MimeType = request.Content.Headers.ContentType?.ToString(),
                        Text = Convert.ToBase64String(requestBody ?? [])
                    }
            },
            Response = new HttpArchiveResponse
            {
                Status = (int)response.StatusCode,
                StatusText = response.ReasonPhrase ?? string.Empty,
                Headers = GetHeaders(response, valuePolicy),
                Content = new HttpArchiveContent
                {
                    Size = responseBody.Length,
                    MimeType = response.Content?.Headers.ContentType?.ToString(),
                    Text = Convert.ToBase64String(responseBody),
                    Encoding = "base64"
                }
            }
        };
    }

    public static HttpResponseMessage CreateResponse(HttpArchiveEntry entry)
    {
        byte[] body = entry.Response.Content.Encoding == "base64"
            ? Convert.FromBase64String(entry.Response.Content.Text ?? string.Empty)
            : System.Text.Encoding.UTF8.GetBytes(entry.Response.Content.Text ?? string.Empty);

        var response = new HttpResponseMessage((HttpStatusCode)entry.Response.Status)
        {
            ReasonPhrase = entry.Response.StatusText,
            Content = new ByteArrayContent(body)
        };

        if (entry.Response.Content.MimeType is not null)
        {
            response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(entry.Response.Content.MimeType);
        }

        AddHeaders(response, entry.Response.Headers);
        return response;
    }

    public static bool Matches(HttpArchiveEntry entry, RequestInfo request)
    {
        if (!string.Equals(entry.Request.Method, request.Method.Method, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(entry.Request.Url, request.Uri?.ToString(), StringComparison.Ordinal))
        {
            return false;
        }

        if (entry.Request.PostData is null)
        {
            return request.RawBody is null or { Length: 0 };
        }

        string requestBody = Convert.ToBase64String(request.RawBody ?? []);
        return string.Equals(entry.Request.PostData.Text ?? string.Empty, requestBody, StringComparison.Ordinal);
    }

    public static HttpArchive Parse(string json)
    {
        return JsonSerializer.Deserialize<HttpArchive>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
            throw new InvalidDataException("The recording file does not contain a valid HTTP archive.");
    }

    public static string Serialize(IEnumerable<HttpArchiveEntry> entries)
    {
        return JsonSerializer.Serialize(new HttpArchive
        {
            Log = new HttpArchiveLog { Entries = entries.ToList() }
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });
    }

    private static List<HttpArchiveHeader> GetHeaders(HttpRequestMessage request, RecordingValuePolicy valuePolicy)
    {
        var headers = request.Headers.ToList();
        if (request.Content is not null)
        {
            headers.AddRange(request.Content.Headers);
        }

        return GetHeaders(headers, valuePolicy);
    }

    private static List<HttpArchiveHeader> GetHeaders(HttpResponseMessage response, RecordingValuePolicy valuePolicy)
    {
        var headers = response.Headers.ToList();
        if (response.Content is not null)
        {
            headers.AddRange(response.Content.Headers);
        }

        return GetHeaders(headers, valuePolicy);
    }

    private static List<HttpArchiveHeader> GetHeaders(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers,
        RecordingValuePolicy valuePolicy)
    {
        return headers.SelectMany(header => header.Value.Select(value => new HttpArchiveHeader
        {
            Name = header.Key,
            Value = valuePolicy == RecordingValuePolicy.RedactSensitive && SensitiveHeaders.Contains(header.Key)
                ? "[REDACTED]"
                : value
        })).ToList();
    }

    private static void AddHeaders(HttpResponseMessage response, IEnumerable<HttpArchiveHeader> headers)
    {
        foreach (HttpArchiveHeader header in headers)
        {
            if (!response.Headers.TryAddWithoutValidation(header.Name, header.Value))
            {
                response.Content.Headers.TryAddWithoutValidation(header.Name, header.Value);
            }
        }
    }
}
#pragma warning restore SA1516

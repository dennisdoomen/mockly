using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using Mockly.Common;
#if NET472_OR_GREATER
using System.Net.Http;
#else
using System.Net.Http.Headers;
#endif

#pragma warning disable CA1054

namespace Mockly;

/// <summary>
/// HTTP mock that configures and intercepts HTTP requests based on configured mocks.
/// </summary>
public class HttpMock
{
    private static readonly HttpClient PassThroughClient = new(new HttpClientHandler
    {
        CheckCertificateRevocationList = true
    });

    private readonly List<RequestMock> mocks = new();
    private readonly List<HttpArchiveEntry> replayRecordings = new();
    private readonly List<HttpArchiveEntry> recordedEntries = new();
    private RequestMockBuilder? previousBuilder;
    private string? recordingPath;
    private bool passThroughUnmatched;
    private RecordingValuePolicy recordingValuePolicy = RecordingValuePolicy.RedactSensitive;

    /// <summary>
    /// Gets or sets whether to fail when unexpected HTTP requests are detected.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>true</c>
    /// </remarks>
    public bool FailOnUnexpectedCalls { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to prefetch the request body content. Defaults to <c>true</c>.
    /// </summary>
    /// <remarks>
    /// By default, the request body is read from the request stream and cached for subsequent matching. But if your
    /// request involves streaming content, this may cause performance or side-effects.
    /// </remarks>
    public bool PrefetchBody { get; set; } = true;

    /// <summary>
    /// Gets all captured requests.
    /// </summary>
    public RequestCollection Requests { get; } = new();

    /// <summary>
    /// Configures unmatched requests to be sent to the real server instead of failing.
    /// </summary>
    public HttpMock PassThroughUnmatched()
    {
        passThroughUnmatched = true;
        FailOnUnexpectedCalls = false;
        return this;
    }

    /// <summary>
    /// Records pass-through requests and responses in a HAR-compatible JSON file.
    /// Sensitive headers are redacted by default; call <see cref="KeepSensitiveRecordingValues"/> to retain them.
    /// </summary>
    public HttpMock RecordingTo(string path)
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        recordingPath = path;
        return PassThroughUnmatched();
    }

    /// <summary>
    /// Allows sensitive header values to be written to subsequent recordings.
    /// </summary>
    public HttpMock KeepSensitiveRecordingValues()
    {
        recordingValuePolicy = RecordingValuePolicy.KeepSensitive;
        return this;
    }

    /// <summary>
    /// Loads a HAR-compatible JSON file and serves matching requests without using the network.
    /// </summary>
    public HttpMock LoadRecordings(string path)
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        HttpArchive archive = HttpArchiveConverter.Parse(File.ReadAllText(path));
        replayRecordings.Clear();
        replayRecordings.AddRange(archive.Log.Entries);
        return this;
    }

    /// <summary>
    /// Starts building a mock for requests using the specified HTTP <paramref name="method"/>.
    /// Reuses settings from the previous request if available.
    /// </summary>
    public RequestMockBuilder For(HttpMethod method)
    {
        return Create(method);
    }

    /// <summary>
    /// Shortcut overload that accepts a full URL pattern (supports wildcards) and configures scheme, host, path and query.
    /// </summary>
    public RequestMockBuilder For(HttpMethod method, string urlPattern)
    {
        var builder = ApplyUrlPattern(Create(method), urlPattern);
        previousBuilder = builder;
        return builder;
    }

    /// <summary>
    /// Starts building a mock for GET requests.
    /// Reuses settings from the previous request if available.
    /// </summary>
    public RequestMockBuilder ForGet()
    {
        return Create(HttpMethod.Get);
    }

    /// <summary>
    /// Shortcut overload that accepts a full URL pattern (supports wildcards) and configures scheme, host, path and query.
    /// </summary>
    public RequestMockBuilder ForGet(string urlPattern)
    {
        return For(HttpMethod.Get, urlPattern);
    }

    /// <summary>
    /// Starts building a mock for POST requests.
    /// Reuses settings from the previous request if available.
    /// </summary>
    public RequestMockBuilder ForPost()
    {
        return Create(HttpMethod.Post);
    }

    /// <summary>
    /// Shortcut overload that accepts a full URL pattern (supports wildcards) and configures scheme, host, path and query.
    /// </summary>
    public RequestMockBuilder ForPost(string urlPattern)
    {
        return For(HttpMethod.Post, urlPattern);
    }

    /// <summary>
    /// Starts building a mock for PUT requests.
    /// Reuses settings from the previous request if available.
    /// </summary>
    public RequestMockBuilder ForPut()
    {
        return Create(HttpMethod.Put);
    }

    /// <summary>
    /// Shortcut overload that accepts a full URL pattern (supports wildcards) and configures scheme, host, path and query.
    /// </summary>
    public RequestMockBuilder ForPut(string urlPattern)
    {
        return For(HttpMethod.Put, urlPattern);
    }

    /// <summary>
    /// Starts building a mock for PATCH requests.
    /// Reuses settings from the previous request if available.
    /// </summary>
    public RequestMockBuilder ForPatch()
    {
        return Create(new HttpMethod("PATCH"));
    }

    /// <summary>
    /// Shortcut overload that accepts a full URL pattern (supports wildcards) and configures scheme, host, path and query.
    /// </summary>
    public RequestMockBuilder ForPatch(string urlPattern)
    {
        return For(new HttpMethod("PATCH"), urlPattern);
    }

    /// <summary>
    /// Starts building a mock for DELETE requests.
    /// Reuses settings from the previous request if available.
    /// </summary>
    public RequestMockBuilder ForDelete()
    {
        return Create(HttpMethod.Delete);
    }

    /// <summary>
    /// Shortcut overload that accepts a full URL pattern (supports wildcards) and configures scheme, host, path and query.
    /// </summary>
    public RequestMockBuilder ForDelete(string urlPattern)
    {
        return For(HttpMethod.Delete, urlPattern);
    }

    /// <summary>
    /// Starts building a mock for HEAD requests.
    /// Reuses settings from the previous request if available.
    /// </summary>
    public RequestMockBuilder ForHead()
    {
        return Create(HttpMethod.Head);
    }

    /// <summary>
    /// Shortcut overload that accepts a full URL pattern (supports wildcards) and configures scheme, host, path and query.
    /// </summary>
    public RequestMockBuilder ForHead(string urlPattern)
    {
        return For(HttpMethod.Head, urlPattern);
    }

    /// <summary>
    /// Starts building a mock for OPTIONS requests.
    /// Reuses settings from the previous request if available.
    /// </summary>
    public RequestMockBuilder ForOptions()
    {
        return Create(HttpMethod.Options);
    }

    /// <summary>
    /// Shortcut overload that accepts a full URL pattern (supports wildcards) and configures scheme, host, path and query.
    /// </summary>
    public RequestMockBuilder ForOptions(string urlPattern)
    {
        return For(HttpMethod.Options, urlPattern);
    }

    /// <summary>
    /// Creates a new <see cref="RequestMockBuilder"/> for the given <paramref name="method"/>,
    /// inheriting settings from the previous builder when one exists.
    /// </summary>
    private RequestMockBuilder Create(HttpMethod method)
    {
        RequestMockBuilder builder = previousBuilder != null
            ? new RequestMockBuilder(this, previousBuilder)
            {
                Method = method
            }
            : new RequestMockBuilder(this, method);

        previousBuilder = builder;
        return builder;
    }

    /// <summary>
    /// Builds a request mock from a <c>curl</c> command string, such as the output of a browser's
    /// "Copy as cURL" command.
    /// </summary>
    /// <remarks>
    /// The HTTP method (<c>-X</c>), URL, headers (<c>-H</c>) and body (<c>-d</c>, <c>--data</c>, <c>--data-raw</c>, ...)
    /// are translated into the equivalent request matching configuration. When no method is specified, <c>POST</c> is
    /// assumed if a body is present and <c>GET</c> otherwise. Attach a response (for example
    /// <see cref="RequestMockBuilder.RespondsWithStatus"/>) to the returned builder to complete the mock.
    /// </remarks>
    /// <param name="curlCommand">The <c>curl</c> command to import.</param>
    /// <returns>A <see cref="RequestMockBuilder"/> configured to match the imported request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="curlCommand"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="curlCommand"/> is empty or cannot be parsed.</exception>
    public RequestMockBuilder ImportFromCurl(string curlCommand)
    {
        if (curlCommand is null)
        {
            throw new ArgumentNullException(nameof(curlCommand));
        }

        CurlRequest parsed = CurlCommandParser.Parse(curlCommand);

        var builder = new RequestMockBuilder(this, ResolveMethod(parsed));
        builder = ApplyCurlUrl(builder, parsed.Url!);

        foreach (KeyValuePair<string, string?> header in parsed.Headers)
        {
            if (IsUnsupportedContentHeader(header.Key))
            {
                continue;
            }

            builder = header.Value is null
                ? ApplyHeaderExclusion(builder, header.Key)
                : ApplyHeader(builder, header.Key, header.Value);
        }

        if (parsed.Body is not null)
        {
            builder = ApplyBody(builder, parsed.Body, HasJsonContentType(parsed));
        }

        previousBuilder = builder;
        return builder;
    }

    /// <summary>
    /// Resets the previous request builder settings.
    /// Successive ForXxx calls will start fresh without inheriting settings.
    /// </summary>
    public HttpMock Reset()
    {
        previousBuilder = null;
        return this;
    }

    /// <summary>
    /// Clears all configured mocks.
    /// </summary>
    public void Clear()
    {
        mocks.Clear();
        previousBuilder = null;
    }

    /// <summary>
    /// Gets an HttpClient configured with this mock.
    /// </summary>
    [SuppressMessage("Design", "CA1024:Use properties where appropriate",
        Justification = "GetClient creates resources and should not be a property")]
    public HttpClient GetClient()
    {
#pragma warning disable CA2000
        var client = new HttpClient(new MockHttpMessageHandler(this))
        {
            BaseAddress = new Uri("https://localhost/")
        };

        return client;
#pragma warning restore CA2000
    }

    /// <summary>
    /// Gets an <see cref="IHttpClientFactory"/> that creates <see cref="HttpClient"/> instances
    /// wired to this mock.
    /// </summary>
    [SuppressMessage("Design", "CA1024:Use properties where appropriate",
        Justification = "Creates resources and should not be a property")]
    public IHttpClientFactory GetClientFactory()
    {
        return new MockHttpClientFactory(this);
    }

    /// <summary>
    /// Gets the underlying <see cref="HttpMessageHandler"/> that intercepts HTTP requests based on the configured mocks.
    /// Use this when you need to wire the mock handler directly into an <see cref="HttpClient"/> or other infrastructure
    /// that accepts an <see cref="HttpMessageHandler"/>.
    /// </summary>
    [SuppressMessage("Design", "CA1024:Use properties where appropriate",
        Justification = "GetMessageHandler creates resources and should not be a property")]
    public HttpMessageHandler GetMessageHandler()
    {
        return new MockHttpMessageHandler(this);
    }

    /// <summary>
    /// Registers a fully configured <see cref="RequestMock"/> with this instance so it is
    /// evaluated during request matching.
    /// </summary>
    internal void AddMock(RequestMock mock)
    {
        mocks.Add(mock);
    }

    /// <summary>
    /// Intercepts an outgoing HTTP request, finds the first non-exhausted matching mock,
    /// and returns its configured response. Records the request in <see cref="Requests"/> and
    /// throws <see cref="UnexpectedRequestException"/> when no mock matches and
    /// <see cref="FailOnUnexpectedCalls"/> is <c>true</c>.
    /// </summary>
    private async Task<HttpResponseMessage> HandleRequest(HttpRequestMessage httpRequest, CancellationToken cancellationToken)
    {
        RequestInfo request = await BuildRequestInfo(httpRequest);

        // Try to find a matching mock
        bool foundMatch = true;
        CapturedRequest? capturedRequest = await TryFindMatchingMock(request, cancellationToken);
        if (capturedRequest == null)
        {
            capturedRequest = new CapturedRequest(request)
            {
                Response = new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    ReasonPhrase = "No matching mock found for this request"
                },
                Mock = null,
                WasExpected = false,
                Timestamp = DateTime.UtcNow
            };

            foundMatch = false;
        }

        Requests.Add(capturedRequest);

        if (capturedRequest.SimulatedFailure is not null)
        {
            ExceptionDispatchInfo.Capture(capturedRequest.SimulatedFailure).Throw();
        }

        if (!foundMatch && replayRecordings.Count > 0)
        {
            HttpArchiveEntry? recording = replayRecordings.FirstOrDefault(entry => HttpArchiveConverter.Matches(entry, request));
            if (recording is not null)
            {
                capturedRequest.Response.Dispose();
                capturedRequest.Response = HttpArchiveConverter.CreateResponse(recording);
                foundMatch = true;
                capturedRequest.WasExpected = true;
            }
        }

        if (!foundMatch && passThroughUnmatched)
        {
            using HttpRequestMessage forwardedRequest = await CloneRequest(httpRequest, request.RawBody);
            HttpResponseMessage response = await PassThroughClient.SendAsync(forwardedRequest, cancellationToken);
            capturedRequest.Response.Dispose();
            capturedRequest.Response = response;
            capturedRequest.WasExpected = true;
            foundMatch = true;

            if (recordingPath is not null)
            {
                await RecordAsync(response, forwardedRequest);
            }
        }

        if (!foundMatch && FailOnUnexpectedCalls)
        {
            await ThrowDetailedException(request);
        }

        return capturedRequest.Response;
    }

    private async Task RecordAsync(HttpResponseMessage response, HttpRequestMessage request)
    {
        HttpArchiveEntry entry = await HttpArchiveConverter.CreateEntryAsync(request, response, recordingValuePolicy);
        recordedEntries.Add(entry);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(recordingPath!))!);
#if NET8_0_OR_GREATER
        await File.WriteAllTextAsync(recordingPath!, HttpArchiveConverter.Serialize(recordedEntries));
#else
        File.WriteAllText(recordingPath!, HttpArchiveConverter.Serialize(recordedEntries));
#endif
    }

    private static async Task<HttpRequestMessage> CloneRequest(HttpRequestMessage request, byte[]? rawBody)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version
        };

        foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content is not null)
        {
            byte[] body = rawBody ?? await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(body);
            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }

    /// <summary>
    /// Builds a detailed diagnostic message that includes the unmatched request, the closest
    /// matching mock (if any), and all registered mocks, then throws an
    /// <see cref="UnexpectedRequestException"/>.
    /// </summary>
    private async Task ThrowDetailedException(RequestInfo request)
    {
        // Determine the closest matching mock (if any) based on a similarity score.
        RequestMock? closestMock = null;
        var highestScore = 0;

        if (mocks.Count > 1)
        {
            foreach (RequestMock mock in mocks)
            {
                int score = await mock.GetMatchScoreAsync(request);
                if (score > highestScore)
                {
                    highestScore = score;
                    closestMock = mock;
                }
            }
        }

        var messageBuilder = new StringBuilder();
        messageBuilder.AppendLine("Unexpected request to:");
        int bodyLength = request.RawBody?.Length ?? request.Body?.Length ?? 0;
        messageBuilder.AppendLine($"  {request.Method} {request.Uri} with body of {bodyLength} bytes");

        messageBuilder.AppendLine();
        messageBuilder.AppendLine("Note that you can further inspect the executed requests through the HttpMock.Requests property.");

        bool requestHasQuery = !string.IsNullOrEmpty(request.Uri?.Query);

        if (closestMock != null && highestScore > 0)
        {
            messageBuilder.AppendLine();
            messageBuilder.AppendLine("Closest matching mock:");
            messageBuilder.Append($"  {closestMock}");
            if (requestHasQuery && closestMock.QueryPattern == null)
            {
                messageBuilder.Append(" (without query string)");
            }

            messageBuilder.AppendLine();
        }

        messageBuilder.AppendLine();
        messageBuilder.AppendLine("Registered mocks:");

        if (!mocks.Any())
        {
            messageBuilder.Append(" (none)");
        }
        else
        {
            foreach (RequestMock mock in mocks)
            {
                messageBuilder.Append(" - ");
                messageBuilder.Append(mock.ToString());
                if (requestHasQuery && mock.QueryPattern == null)
                {
                    messageBuilder.Append(" (without query string)");
                }

                messageBuilder.AppendLine();
            }
        }

        if ((request.RawBody?.Length ?? 0) > 0 || !string.IsNullOrEmpty(request.Body))
        {
            messageBuilder.AppendLine();
            messageBuilder.AppendLine($"Body ({request.ContentType}):");

            if (request.IsBodyLikelyTextual() && request.Body is not null)
            {
                messageBuilder.AppendLine($"  \"{request.Body}\"");
            }
            else
            {
                messageBuilder.AppendLine("  (binary content)");
            }
        }

        throw new UnexpectedRequestException(messageBuilder.ToString());
    }

    /// <summary>
    /// Converts an <see cref="HttpRequestMessage"/> into a <see cref="RequestInfo"/>,
    /// optionally pre-reading the body when <see cref="PrefetchBody"/> is <c>true</c>.
    /// </summary>
    private async Task<RequestInfo> BuildRequestInfo(HttpRequestMessage httpRequest)
    {
        byte[]? rawBody = null;
        if ((PrefetchBody || passThroughUnmatched || replayRecordings.Count > 0) && httpRequest.Content is not null)
        {
            rawBody = await httpRequest.Content.ReadAsByteArrayAsync();
        }

        // If any registered mock opted into TreatBodyAsTextual(), decode the body as text even when its
        // Content-Type isn't recognized as textual.
        bool forceTextualBody = mocks.Any(mock => mock.ForceTextualBody);

        return new RequestInfo(httpRequest, rawBody) { ForceTextualBody = forceTextualBody };
    }

    /// <summary>
    /// Finds the first non-exhausted mock that matches <paramref name="request"/>, invokes it,
    /// and returns the resulting <see cref="CapturedRequest"/>. Returns <c>null</c> when no mock matches.
    /// </summary>
    private async Task<CapturedRequest?> TryFindMatchingMock(RequestInfo request, CancellationToken cancellationToken)
    {
        RequestMock? matchingMock = await mocks.FirstOrDefaultAsync(m =>
            m.IsExhausted ? Task.FromResult(false) : m.Matches(request));

        if (matchingMock != null)
        {
            return await matchingMock.TrackRequestAsync(request, cancellationToken);
        }

        return null;
    }

    /// <summary>
    /// Gets a value indicating whether all configured request mocks in the HTTP mock have been invoked as many times as specified.
    /// </summary>
    /// <remarks>
    /// Returns <c>true</c> if each configured request mock has been invoked as many times as configured; otherwise, <c>false</c>.
    /// </remarks>
    public bool AllMocksInvoked
    {
        get => mocks.All(m => m.MaxInvocations is not null
            ? m.InvocationCount >= m.MaxInvocations
            : m.InvocationCount > 0);
    }

    /// <summary>
    /// Gets mocks that have not been invoked.
    /// </summary>
    public IEnumerable<RequestMock> GetUninvokedMocks()
    {
        return mocks
            .Where(m => m.MaxInvocations is not null ? m.InvocationCount < m.MaxInvocations : m.InvocationCount == 0)
            .ToList();
    }

    private class MockHttpMessageHandler(HttpMock mock) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
                return await mock.HandleRequest(request, cancellationToken);
        }
    }

    private sealed class MockHttpClientFactory(HttpMock mock) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
#pragma warning disable CA2000
            return new HttpClient(new MockHttpMessageHandler(mock))
            {
                BaseAddress = new Uri("https://localhost/")
            };
#pragma warning restore CA2000
        }
    }

    /// <summary>
    /// Configures scheme, host, path and query for a curl-imported URL using literal (non-wildcard) semantics,
    /// since the URL comes from an actual request rather than being an intentional Mockly matching pattern.
    /// </summary>
    private static RequestMockBuilder ApplyCurlUrl(RequestMockBuilder builder, string rawUrl)
    {
        Uri uri = ParseCurlUrl(rawUrl);

        builder = string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)
            ? builder.ForHttps()
            : builder.ForHttp();

        builder = builder.ForHost(uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}");

        string path = uri.AbsolutePath;
        builder = builder.With(
            request => string.Equals(request.Uri?.AbsolutePath, path, StringComparison.Ordinal),
            $"path equals '{path}'");

        string query = WebUtility.UrlDecode(uri.Query);
        builder = builder.With(
            request => string.Equals(WebUtility.UrlDecode(request.Uri?.Query ?? string.Empty), query, StringComparison.Ordinal),
            query.Length > 0 ? $"query equals '{query}'" : "no query string");

        return builder;
    }

    /// <summary>
    /// Parses a curl URL into an absolute <see cref="Uri"/>, defaulting to the <c>http</c> scheme when none is
    /// specified, matching curl's own default (unlike Mockly's builder, which defaults to <c>https</c>).
    /// </summary>
    private static Uri ParseCurlUrl(string rawUrl)
    {
        string trimmed = rawUrl.Trim();

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri) &&
            (string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)))
        {
            return uri;
        }

        return new Uri("http://" + trimmed, UriKind.Absolute);
    }

    /// <summary>
    /// Parses a full URL pattern (e.g. <c>"https://api.example.com/v1/users?active=*"</c>) and
    /// applies the scheme, host, path, and query segments to <paramref name="builder"/>.
    /// Wildcards (<c>*</c>) are supported in every segment.
    /// </summary>
    private static RequestMockBuilder ApplyUrlPattern(RequestMockBuilder builder, string urlPattern)
    {
        if (string.IsNullOrWhiteSpace(urlPattern))
        {
            return builder;
        }

        var pattern = urlPattern.Trim();

        // Scheme
        if (pattern.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            builder = builder.ForHttp();
            pattern = pattern.Substring("http://".Length);
        }
        else if (pattern.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            builder = builder.ForHttps();
            pattern = pattern.Substring("https://".Length);
        }
        else
        {
            // No scheme specified in pattern; leave as-is
        }

        // Host (until '/' or '?')
        string hostPattern;
        string remainder;
        int slashIdx = pattern.IndexOf("/", StringComparison.Ordinal);
        int qIdx = pattern.IndexOf("?", StringComparison.Ordinal);

        int stopIdx = -1;
        if (slashIdx >= 0 && qIdx >= 0)
        {
            stopIdx = Math.Min(slashIdx, qIdx);
        }
        else if (slashIdx >= 0)
        {
            stopIdx = slashIdx;
        }
        else if (qIdx >= 0)
        {
            stopIdx = qIdx;
        }
        else
        {
            // neither slash nor question mark present
        }

        if (stopIdx >= 0)
        {
            hostPattern = pattern.Substring(0, stopIdx);
            remainder = pattern.Substring(stopIdx);
        }
        else
        {
            hostPattern = pattern;
            remainder = string.Empty;
        }

        if (!string.IsNullOrEmpty(hostPattern))
        {
            builder = builder.ForHost(hostPattern);
        }

        // Path (starts with '/' until '?' or end)
        string pathPattern = string.Empty;
        string queryPattern = string.Empty;
        if (!string.IsNullOrEmpty(remainder))
        {
            int qMark = remainder.IndexOf("?", StringComparison.Ordinal);
            if (qMark >= 0)
            {
                pathPattern = remainder.Substring(0, qMark);
                queryPattern = remainder.Substring(qMark); // includes '?'
            }
            else
            {
                pathPattern = remainder;
            }
        }

        if (!string.IsNullOrEmpty(pathPattern))
        {
            builder = builder.WithPath(pathPattern);
        }

        if (!string.IsNullOrEmpty(queryPattern))
        {
            builder = builder.WithQuery(queryPattern);
        }

        return builder;
    }

    private static HttpMethod ResolveMethod(CurlRequest parsed)
    {
        if (!string.IsNullOrEmpty(parsed.Method))
        {
            return new HttpMethod(parsed.Method!.ToUpperInvariant());
        }

        return parsed.Body is not null ? HttpMethod.Post : HttpMethod.Get;
    }

    private static RequestMockBuilder ApplyHeader(RequestMockBuilder builder, string name, string value)
    {
        return builder.With(request => HeaderMatches(request, name, value), $"header '{name}: {value}'");
    }

    /// <summary>
    /// Configures the mock to require the given header to be absent, mirroring curl's <c>-H 'Name:'</c> syntax
    /// which suppresses a header rather than sending it with an empty value.
    /// </summary>
    private static RequestMockBuilder ApplyHeaderExclusion(RequestMockBuilder builder, string name)
    {
        if (string.Equals(name, "Content-Type", StringComparison.OrdinalIgnoreCase))
        {
            return builder.With(request => request.ContentType is null, $"header '{name}' is absent");
        }

        return builder.With(request => !request.Headers.Contains(name), $"header '{name}' is absent");
    }

    private static RequestMockBuilder ApplyBody(RequestMockBuilder builder, string body, bool jsonContentType)
    {
        if (IsJsonBody(body, jsonContentType))
        {
            return builder.WithBodyMatchingJson(body);
        }

        return builder.With(
            request => string.Equals(request.Body, body, StringComparison.Ordinal),
            $"body equals \"{body}\"");
    }

    private static bool IsUnsupportedContentHeader(string name)
    {
        return name.StartsWith("Content-", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(name, "Content-Type", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasJsonContentType(CurlRequest parsed)
    {
        foreach (KeyValuePair<string, string?> header in parsed.Headers)
        {
            if (string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase) &&
                header.Value is not null && header.Value.Contains("json", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HeaderMatches(RequestInfo request, string name, string value)
    {
        if (string.Equals(name, "Content-Type", StringComparison.OrdinalIgnoreCase))
        {
            string expected = value.Split(';')[0].Trim();
            return request.ContentType is { } actual &&
                string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }

#if NET8_0_OR_GREATER
        // Use the raw, non-validated header representation so headers whose values are split into multiple
        // tokens by .NET's typed parsing (such as User-Agent, which is space- rather than comma-separated)
        // are compared using their original textual form instead of being rejoined with a comma.
        if (!request.Headers.NonValidated.TryGetValues(name, out HeaderStringValues values))
        {
            return false;
        }

        return values.Any(v => string.Equals(v, value, StringComparison.Ordinal)) ||
            string.Equals(values.ToString(), value, StringComparison.Ordinal);
#else
        // net472 lacks HttpHeaders.NonValidated, so the original separator can't be recovered; try both the
        // comma (the list syntax most headers use) and the space (used by product-token headers like
        // User-Agent) to reconstruct the value .NET split into multiple tokens.
        if (!request.Headers.TryGetValues(name, out IEnumerable<string>? values))
        {
            return false;
        }

        List<string> materializedValues = values.ToList();
        return materializedValues.Any(v => string.Equals(v, value, StringComparison.Ordinal)) ||
            string.Equals(string.Join(", ", materializedValues), value, StringComparison.Ordinal) ||
            string.Equals(string.Join(" ", materializedValues), value, StringComparison.Ordinal);
#endif
    }

    private static bool IsJsonBody(string body, bool jsonContentType)
    {
        string trimmed = body.TrimStart();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (!jsonContentType && trimmed[0] != '{' && trimmed[0] != '[')
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

#pragma warning restore CA1054
}

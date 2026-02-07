using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using Mockly.Common;
#if NET472_OR_GREATER
using System.Net.Http;
#endif

#pragma warning disable CA1054

namespace Mockly;

/// <summary>
/// HTTP mock that configures and intercepts HTTP requests based on configured mocks.
/// </summary>
public class HttpMock
{
    private readonly List<RequestMock> mocks = new();
    private RequestMockBuilder? previousBuilder;

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
    /// Starts building a mock for GET requests.
    /// Reuses settings from the previous request if available.
    /// </summary>
    public RequestMockBuilder ForGet()
    {
        RequestMockBuilder mockBuilder = previousBuilder != null
            ? new RequestMockBuilder(this, previousBuilder)
            {
                Method = HttpMethod.Get
            }
            : new RequestMockBuilder(this, HttpMethod.Get);

        previousBuilder = mockBuilder;
        return mockBuilder;
    }

    /// <summary>
    /// Shortcut overload that accepts a full URL pattern (supports wildcards) and configures scheme, host, path and query.
    /// </summary>
    public RequestMockBuilder ForGet(string urlPattern)
    {
        var builder = ForGet();
        builder = ApplyUrlPattern(builder, urlPattern);
        previousBuilder = builder;
        return builder;
    }

    /// <summary>
    /// Starts building a mock for POST requests.
    /// Reuses settings from the previous request if available.
    /// </summary>
    public RequestMockBuilder ForPost()
    {
        var builder = previousBuilder != null
            ? new RequestMockBuilder(this, previousBuilder)
            {
                Method = HttpMethod.Post
            }
            : new RequestMockBuilder(this, HttpMethod.Post);

        previousBuilder = builder;
        return builder;
    }

    /// <summary>
    /// Shortcut overload that accepts a full URL pattern (supports wildcards) and configures scheme, host, path and query.
    /// </summary>
    public RequestMockBuilder ForPost(string urlPattern)
    {
        var builder = ForPost();
        builder = ApplyUrlPattern(builder, urlPattern);
        previousBuilder = builder;
        return builder;
    }

    /// <summary>
    /// Starts building a mock for PUT requests.
    /// Reuses settings from the previous request if available.
    /// </summary>
    public RequestMockBuilder ForPut()
    {
        var builder = previousBuilder != null
            ? new RequestMockBuilder(this, previousBuilder)
            {
                Method = HttpMethod.Put
            }
            : new RequestMockBuilder(this, HttpMethod.Put);

        previousBuilder = builder;
        return builder;
    }

    /// <summary>
    /// Shortcut overload that accepts a full URL pattern (supports wildcards) and configures scheme, host, path and query.
    /// </summary>
    public RequestMockBuilder ForPut(string urlPattern)
    {
        var builder = ForPut();
        builder = ApplyUrlPattern(builder, urlPattern);
        previousBuilder = builder;
        return builder;
    }

    /// <summary>
    /// Starts building a mock for PATCH requests.
    /// Reuses settings from the previous request if available.
    /// </summary>
    public RequestMockBuilder ForPatch()
    {
        var builder = previousBuilder != null
            ? new RequestMockBuilder(this, previousBuilder)
            {
                Method = new HttpMethod("PATCH")
            }
            : new RequestMockBuilder(this, new HttpMethod("PATCH"));

        previousBuilder = builder;
        return builder;
    }

    /// <summary>
    /// Shortcut overload that accepts a full URL pattern (supports wildcards) and configures scheme, host, path and query.
    /// </summary>
    public RequestMockBuilder ForPatch(string urlPattern)
    {
        var builder = ForPatch();
        builder = ApplyUrlPattern(builder, urlPattern);
        previousBuilder = builder;
        return builder;
    }

    /// <summary>
    /// Starts building a mock for DELETE requests.
    /// Reuses settings from the previous request if available.
    /// </summary>
    public RequestMockBuilder ForDelete()
    {
        var builder = previousBuilder != null
            ? new RequestMockBuilder(this, previousBuilder)
            {
                Method = HttpMethod.Delete
            }
            : new RequestMockBuilder(this, HttpMethod.Delete);

        previousBuilder = builder;
        return builder;
    }

    /// <summary>
    /// Shortcut overload that accepts a full URL pattern (supports wildcards) and configures scheme, host, path and query.
    /// </summary>
    public RequestMockBuilder ForDelete(string urlPattern)
    {
        var builder = ForDelete();
        builder = ApplyUrlPattern(builder, urlPattern);
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

    internal void AddMock(RequestMock mock)
    {
        mocks.Add(mock);
    }

    private async Task<HttpResponseMessage> HandleRequest(HttpRequestMessage httpRequest)
    {
        RequestInfo request = await BuildRequestInfo(httpRequest);

        // Try to find a matching mock
        bool foundMatch = true;
        CapturedRequest? capturedRequest = await TryFindMatchingMock(request);
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

        if (!foundMatch && FailOnUnexpectedCalls)
        {
            await ThrowDetailedException(request);
        }

        return capturedRequest.Response;
    }

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
        messageBuilder.AppendLine($"  {request.Method} {request.Uri} with body of {request.Body?.Length ?? 0} bytes");

        messageBuilder.AppendLine();
        messageBuilder.AppendLine("Note that you can further inspect the executed requests through the HttpMock.Requests property.");

        if (closestMock != null && highestScore > 0)
        {
            messageBuilder.AppendLine();
            messageBuilder.AppendLine("Closest matching mock:");
            messageBuilder.Append($"  {closestMock}");
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
                messageBuilder.AppendLine(mock.ToString());
            }
        }

        if (request.Body is not null && request.Body.Length > 0)
        {
            messageBuilder.AppendLine();
            messageBuilder.AppendLine($"Body ({request.ContentType}):");

            if (request.IsBodyLikelyTextual())
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

    private async Task<RequestInfo> BuildRequestInfo(HttpRequestMessage httpRequest)
    {
        string? body = null;
        if (PrefetchBody && httpRequest.Content is not null)
        {
            body = await httpRequest.Content.ReadAsStringAsync();
        }

        var request = new RequestInfo(httpRequest, body);
        return request;
    }

    private async Task<CapturedRequest?> TryFindMatchingMock(RequestInfo request)
    {
        RequestMock? matchingMock = await mocks.FirstOrDefaultAsync(m =>
            m.IsExhausted ? Task.FromResult(false) : m.Matches(request));

        if (matchingMock != null)
        {
            return matchingMock.TrackRequest(request);
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
            return await mock.HandleRequest(request);
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

#pragma warning restore CA1054
}

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Text;
using Mockly.Common;

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
        return new HttpClient(new MockHttpMessageHandler(this));
#pragma warning restore CA2000
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
        messageBuilder.AppendLine($"  {request.Method} {request.Uri}");

        if (closestMock != null && highestScore > 0)
        {
            messageBuilder.AppendLine();
            messageBuilder.AppendLine("Closest matching mock:");
            messageBuilder.Append($"  {closestMock.ToDetailedString()}");
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
                messageBuilder.AppendLine(mock.ToDetailedString());
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
}

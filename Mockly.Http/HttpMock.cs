using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Mockly.Http;

/// <summary>
/// HTTP mock that configures and intercepts HTTP requests based on configured mocks.
/// </summary>
public class HttpMock : IDisposable
{
    private readonly List<RequestMock> mocks = new();
    private readonly RequestCollection allRequests = new();
    private MockHttpMessageHandler? handler;
    private bool failOnUnexpectedCalls = true;
    private RequestBuilder? previousBuilder;

    /// <summary>
    /// Gets or sets whether to fail when unexpected HTTP requests are detected.
    /// Default is true.
    /// </summary>
    public bool FailOnUnexpectedCalls
    {
        get => failOnUnexpectedCalls;
        set => failOnUnexpectedCalls = value;
    }

    /// <summary>
    /// Gets all captured requests.
    /// </summary>
    public RequestCollection Requests => allRequests;

    /// <summary>
    /// Starts building a mock for GET requests.
    /// Reuses settings from the previous request if available.
    /// </summary>
    public RequestBuilder ForGet()
    {
        var builder = previousBuilder != null 
            ? new RequestBuilder(this, previousBuilder) { Method = HttpMethod.Get }
            : new RequestBuilder(this, HttpMethod.Get);
        previousBuilder = builder;
        return builder;
    }

    /// <summary>
    /// Starts building a mock for POST requests.
    /// Reuses settings from the previous request if available.
    /// </summary>
    public RequestBuilder ForPost()
    {
        var builder = previousBuilder != null 
            ? new RequestBuilder(this, previousBuilder) { Method = HttpMethod.Post }
            : new RequestBuilder(this, HttpMethod.Post);
        previousBuilder = builder;
        return builder;
    }

    /// <summary>
    /// Starts building a mock for PUT requests.
    /// Reuses settings from the previous request if available.
    /// </summary>
    public RequestBuilder ForPut()
    {
        var builder = previousBuilder != null 
            ? new RequestBuilder(this, previousBuilder) { Method = HttpMethod.Put }
            : new RequestBuilder(this, HttpMethod.Put);
        previousBuilder = builder;
        return builder;
    }

    /// <summary>
    /// Starts building a mock for PATCH requests.
    /// Reuses settings from the previous request if available.
    /// </summary>
    public RequestBuilder ForPatch()
    {
        var builder = previousBuilder != null 
            ? new RequestBuilder(this, previousBuilder) { Method = new HttpMethod("PATCH") }
            : new RequestBuilder(this, new HttpMethod("PATCH"));
        previousBuilder = builder;
        return builder;
    }

    /// <summary>
    /// Starts building a mock for DELETE requests.
    /// Reuses settings from the previous request if available.
    /// </summary>
    public RequestBuilder ForDelete()
    {
        var builder = previousBuilder != null 
            ? new RequestBuilder(this, previousBuilder) { Method = HttpMethod.Delete }
            : new RequestBuilder(this, HttpMethod.Delete);
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1024:Use properties where appropriate",
        Justification = "GetClient creates resources and should not be a property")]
    public HttpClient GetClient()
    {
        if (handler == null)
        {
            handler = new MockHttpMessageHandler(this);
        }

        return new HttpClient(handler);
    }

    internal void AddMock(RequestMock mock)
    {
        mocks.Add(mock);
    }

    internal HttpResponseMessage HandleRequest(HttpRequestMessage request)
    {
        // Try to find a matching mock
        var matchingMock = mocks.FirstOrDefault(m => m.Matches(request));

        if (matchingMock != null)
        {
            var response = matchingMock.HandleRequest(request);
            
            // Also add to global request collection
            allRequests.Add(new CapturedRequest
            {
                Request = request,
                Response = response,
                Mock = matchingMock,
                WasExpected = true,
                Timestamp = DateTime.UtcNow
            });

            return response;
        }

        // No matching mock found - this is an unexpected request
        var unexpectedResponse = new HttpResponseMessage(
            failOnUnexpectedCalls ? HttpStatusCode.InternalServerError : HttpStatusCode.NotFound)
        {
            ReasonPhrase = "No matching mock found for this request"
        };

        allRequests.Add(new CapturedRequest
        {
            Request = request,
            Response = unexpectedResponse,
            Mock = null,
            WasExpected = false,
            Timestamp = DateTime.UtcNow
        });

        if (failOnUnexpectedCalls)
        {
            throw new UnexpectedRequestException($"Unexpected {request.Method} request to {request.RequestUri}");
        }

        return unexpectedResponse;
    }

    /// <summary>
    /// Checks if all configured mocks have been invoked.
    /// </summary>
    public bool AllMocksInvoked()
    {
        return mocks.All(m => m.InvocationCount > 0);
    }

    /// <summary>
    /// Gets mocks that have not been invoked.
    /// </summary>
    public IEnumerable<RequestMock> GetUninvokedMocks()
    {
        return mocks.Where(m => m.InvocationCount == 0).ToList();
    }

    /// <summary>
    /// Disposes the HTTP mock and associated resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the HTTP mock and associated resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            handler?.Dispose();
        }
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpMock mock;

        public MockHttpMessageHandler(HttpMock mock)
        {
            this.mock = mock;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = mock.HandleRequest(request);
            return Task.FromResult(response);
        }
    }
}

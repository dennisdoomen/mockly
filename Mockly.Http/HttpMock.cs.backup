using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Mockly.Http;

/// <summary>
/// HTTP mock that intercepts and responds to HTTP requests based on configured mocks.
/// </summary>
public class HttpMock : IDisposable
{
    private readonly List<RequestMock> mocks;
    private readonly RequestCollection allRequests = new();
    private readonly MockHttpMessageHandler handler;
    private bool shouldFailOnUnexpectedCalls;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CSharp Guidelines", "AV1564:Avoid using bool parameters",
        Justification = "Internal constructor - bool parameter is acceptable here")]
    internal HttpMock(List<RequestMock> mocks, bool failOnUnexpectedCalls)
    {
        this.mocks = mocks;
        shouldFailOnUnexpectedCalls = failOnUnexpectedCalls;
        handler = new MockHttpMessageHandler(this);
    }

    /// <summary>
    /// Gets all captured requests.
    /// </summary>
    public RequestCollection Requests => allRequests;

    /// <summary>
    /// Builds and returns an HttpClient configured with this mock.
    /// </summary>
    public HttpClient Build()
    {
        return new HttpClient(handler);
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
            shouldFailOnUnexpectedCalls ? HttpStatusCode.InternalServerError : HttpStatusCode.NotFound)
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

        if (shouldFailOnUnexpectedCalls)
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
    internal IEnumerable<RequestMock> GetUninvokedMocks()
    {
        return mocks.Where(m => m.InvocationCount == 0);
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

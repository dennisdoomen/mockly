using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;

namespace Mockly.Http;

/// <summary>
/// Builder for configuring HTTP request mocks with fluent API.
/// </summary>
public class HttpMockBuilder
{
    private readonly List<RequestMock> mocks = new();
    private bool failOnUnexpectedCalls = true;

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
    /// Starts building a mock for GET requests.
    /// </summary>
    public RequestBuilder ForGet()
    {
        return new RequestBuilder(this, HttpMethod.Get);
    }

    /// <summary>
    /// Starts building a mock for POST requests.
    /// </summary>
    public RequestBuilder ForPost()
    {
        return new RequestBuilder(this, HttpMethod.Post);
    }

    /// <summary>
    /// Starts building a mock for PUT requests.
    /// </summary>
    public RequestBuilder ForPut()
    {
        return new RequestBuilder(this, HttpMethod.Put);
    }

    /// <summary>
    /// Starts building a mock for PATCH requests.
    /// </summary>
    public RequestBuilder ForPatch()
    {
        return new RequestBuilder(this, new HttpMethod("PATCH"));
    }

    /// <summary>
    /// Starts building a mock for DELETE requests.
    /// </summary>
    public RequestBuilder ForDelete()
    {
        return new RequestBuilder(this, HttpMethod.Delete);
    }

    /// <summary>
    /// Clears all configured mocks.
    /// </summary>
    public void Clear()
    {
        mocks.Clear();
    }

    /// <summary>
    /// Builds an HttpMock instance with the configured mocks.
    /// </summary>
    public HttpMock Build()
    {
        return new HttpMock(mocks, failOnUnexpectedCalls);
    }

    internal void AddMock(RequestMock mock)
    {
        mocks.Add(mock);
    }
}

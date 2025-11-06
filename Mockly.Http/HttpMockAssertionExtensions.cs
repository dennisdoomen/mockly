using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Primitives;

namespace Mockly.Http;

/// <summary>
/// FluentAssertions extensions for HttpMock.
/// </summary>
public static class HttpMockAssertionExtensions
{
    /// <summary>
    /// Returns an assertion object for the HttpMock.
    /// </summary>
    public static HttpMockAssertions Should(this HttpMock mock)
    {
        return new HttpMockAssertions(mock);
    }

    /// <summary>
    /// Returns an assertion object for the RequestCollection.
    /// </summary>
    public static RequestCollectionAssertions Should(this RequestCollection collection)
    {
        return new RequestCollectionAssertions(collection);
    }

    /// <summary>
    /// Returns an assertion object for the CapturedRequest.
    /// </summary>
    public static CapturedRequestAssertions Should(this CapturedRequest request)
    {
        return new CapturedRequestAssertions(request);
    }
}

/// <summary>
/// Assertions for HttpMock.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "MA0048:File name must match type name", 
    Justification = "Multiple assertion classes in one file for convenience")]
public class HttpMockAssertions
{
    private readonly HttpMock subject;

    public HttpMockAssertions(HttpMock subject)
    {
        this.subject = subject;
    }

    /// <summary>
    /// Asserts that all configured request mocks have been invoked.
    /// </summary>
    public void HaveAllRequestsCalled(string because = "", params object[] becauseArgs)
    {
        subject.AllMocksInvoked().Should()
            .BeTrue(because + " all request mocks should have been called, but {0} mock(s) were not invoked", 
                becauseArgs.Concat(new object[] { subject.GetUninvokedMocks().Count() }).ToArray());
    }
}

/// <summary>
/// Assertions for RequestCollection.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "MA0048:File name must match type name", 
    Justification = "Multiple assertion classes in one file for convenience")]
public class RequestCollectionAssertions
{
    private readonly RequestCollection subject;

    public RequestCollectionAssertions(RequestCollection subject)
    {
        this.subject = subject;
    }

    /// <summary>
    /// Asserts that the request collection is not empty.
    /// </summary>
    public void NotBeEmpty(string because = "", params object[] becauseArgs)
    {
        (subject.Count > 0).Should().BeTrue(because + " request collection should not be empty", becauseArgs);
    }

    /// <summary>
    /// Asserts that the request collection is empty.
    /// </summary>
    public void BeEmpty(string because = "", params object[] becauseArgs)
    {
        (subject.Count == 0).Should().BeTrue(because + " request collection should be empty, but found {0} request(s)", 
            becauseArgs.Concat(new object[] { subject.Count }).ToArray());
    }

    /// <summary>
    /// Asserts that the request collection does not contain any unexpected requests.
    /// </summary>
    public void NotContainUnexpectedCalls(string because = "", params object[] becauseArgs)
    {
        var unexpectedRequests = subject.Requests.Where(r => !r.WasExpected).ToList();

        (!unexpectedRequests.Any()).Should().BeTrue(
            because + " no unexpected requests should exist, but found {0} unexpected request(s):{1}{2}",
            becauseArgs.Concat(new object[] 
            { 
                unexpectedRequests.Count,
                Environment.NewLine,
                string.Join(Environment.NewLine, unexpectedRequests.Select(r => $"  {r.Method} {r.Request.RequestUri}"))
            }).ToArray());
    }

    /// <summary>
    /// Asserts that the request collection has the specified count.
    /// </summary>
    public void HaveCount(int expected, string because = "", params object[] becauseArgs)
    {
        subject.Count.Should().Be(expected, because, becauseArgs);
    }
}

/// <summary>
/// Assertions for CapturedRequest.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "MA0048:File name must match type name", 
    Justification = "Multiple assertion classes in one file for convenience")]
public class CapturedRequestAssertions
{
    private readonly CapturedRequest subject;

    public CapturedRequestAssertions(CapturedRequest subject)
    {
        this.subject = subject;
    }

    /// <summary>
    /// Asserts that the request was expected (matched a mock).
    /// </summary>
    public void BeExpected(string because = "", params object[] becauseArgs)
    {
        subject.WasExpected.Should().BeTrue(because + " request should be expected, but it was unexpected", becauseArgs);
    }

    /// <summary>
    /// Asserts that the request was unexpected (did not match any mock).
    /// </summary>
    public void BeUnexpected(string because = "", params object[] becauseArgs)
    {
        (!subject.WasExpected).Should().BeTrue(because + " request should be unexpected, but it was expected", becauseArgs);
    }
}

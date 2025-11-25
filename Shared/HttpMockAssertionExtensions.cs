using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Collections;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Mockly.Common;

#pragma warning disable CA1054

#pragma warning disable AV1505
namespace Mockly;
#pragma warning restore AV1505

// This file intentionally contains multiple assertion types for discoverability and
// to keep the FluentAssertions extensions grouped together. The MA0048 rule enforces
// a file name to match a single type name, which doesn't apply to this design.
#pragma warning disable MA0048 // File name must match type name

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
[SuppressMessage("Design", "MA0048:File name must match type name",
    Justification = "Multiple assertion classes in one file for convenience")]
public class HttpMockAssertions(HttpMock subject) :
#if FA8
    ReferenceTypeAssertions<HttpMock, HttpMockAssertions>(subject, AssertionChain.GetOrCreate())
#else
    ReferenceTypeAssertions<HttpMock, HttpMockAssertions>(subject)
#endif
{
    private readonly HttpMock subject = subject;

    /// <summary>
    /// Asserts that all configured request mocks have been invoked.
    /// </summary>
    public void HaveAllRequestsCalled(string because = "", params object[] becauseArgs)
    {
        var uninvokedCount = subject.GetUninvokedMocks().Count();

#if FA8
        AssertionChain.GetOrCreate()
#else
        Execute.Assertion
#endif
            .BecauseOf(because, becauseArgs)
            .ForCondition(subject.AllMocksInvoked)
            .FailWith("all request mocks should have been called, but {0} mock(s) were not invoked", uninvokedCount);
    }

    protected override string Identifier => "HTTP mock";
}

/// <summary>
/// Assertions for RequestCollection.
/// </summary>
[SuppressMessage("Design", "MA0048:File name must match type name",
    Justification = "Multiple assertion classes in one file for convenience")]
public class RequestCollectionAssertions : GenericCollectionAssertions<CapturedRequest>
{
    private readonly RequestCollection subject;

    public RequestCollectionAssertions(RequestCollection subject)
#if FA8
        : base(subject, AssertionChain.GetOrCreate())
#else
        : base(subject)
#endif
    {
        this.subject = subject;
    }

    /// <summary>
    /// Asserts that the request collection does not contain any unexpected requests.
    /// </summary>
    public void NotContainUnexpectedCalls(string because = "", params object[] becauseArgs)
    {
        var unexpectedRequests = subject.Where(r => !r.WasExpected).ToList();

#if FA8
        AssertionChain.GetOrCreate()
#else
        Execute.Assertion
#endif
            .BecauseOf(because, becauseArgs)
            .ForCondition(!unexpectedRequests.Any())
            .FailWith(
                "no unexpected requests should exist, but found {0} unexpected request(s):{1}{2}",
                unexpectedRequests.Count,
                Environment.NewLine,
                string.Join(Environment.NewLine, unexpectedRequests.Select(r => $"  {r.Method} {r.Uri}")));
    }

    /// <summary>
    /// Asserts that the collection contains at least one request and returns assertions for that request.
    /// </summary>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
    /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <paramref name="because" />.
    /// </param>
    public ContainedRequestAssertions ContainRequest(string because = "", params object[] becauseArgs)
    {
        CapturedRequest? found = subject.FirstOrDefault();

#if FA8
        AssertionChain.GetOrCreate()
#else
        Execute.Assertion
#endif
            .BecauseOf(because, becauseArgs)
            .ForCondition(found is not null)
            .FailWith("Expected at least one request to have been captured{because}, but none were found");

        return new ContainedRequestAssertions(found!);
    }

    /// <summary>
    /// Asserts that the collection contains a request for the given URI and returns assertions for that request.
    /// </summary>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
    /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <paramref name="because" />.
    /// </param>
    public ContainedRequestAssertions ContainRequestFor(Uri uri, string because = "", params object[] becauseArgs)
    {
        return ContainRequestFor(uri.ToString(), because, becauseArgs);
    }

    /// <summary>
    /// Asserts that the collection contains a request for the given URL pattern and returns assertions for that request.
    /// </summary>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
    /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <paramref name="because" />.
    /// </param>
    public ContainedRequestAssertions ContainRequestFor(string urlPattern, string because = "", params object[] becauseArgs)
    {
        // If the provided URI is relative, compare by AbsolutePath + Query; otherwise by absolute URI
        CapturedRequest? found = subject.FirstOrDefault(r => r.Uri is not null && r.Uri.ToString().MatchesWildcard(urlPattern));

        var failureMessage = new StringBuilder();
        if (!subject.IsEmpty)
        {
            failureMessage.AppendFormat("Expected a request for URL pattern \"{0}\"{{because}}, but none were found among:",
                urlPattern);

            failureMessage.AppendLine();
            foreach (CapturedRequest request in subject)
            {
                failureMessage.AppendLine($" - {request}");
            }
        }
        else
        {
            failureMessage.AppendFormat(
                "Expected a request for URL pattern \"{0}\"{{because}}, but no requests where captured at all", urlPattern);
        }

#if FA8
        AssertionChain.GetOrCreate()
#else
        Execute.Assertion
#endif
            .BecauseOf(because, becauseArgs)
            .ForCondition(found is not null)
            .FailWith(failureMessage.ToString());

        return new ContainedRequestAssertions(found!);
    }
}

/// <summary>
/// Assertions for CapturedRequest.
/// </summary>
[SuppressMessage("Design", "MA0048:File name must match type name",
    Justification = "Multiple assertion classes in one file for convenience")]
public class CapturedRequestAssertions : ObjectAssertions<CapturedRequest, CapturedRequestAssertions>
{
    private readonly CapturedRequest subject;

    public CapturedRequestAssertions(CapturedRequest subject)
#if FA8
        : base(subject, AssertionChain.GetOrCreate())
#else
        : base(subject)
#endif
    {
        this.subject = subject;
    }

    /// <summary>
    /// Asserts that the request was expected (matched a mock).
    /// </summary>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
    /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <paramref name="because" />.
    /// </param>
    public void BeExpected(string because = "", params object[] becauseArgs)
    {
#if FA8
        AssertionChain.GetOrCreate()
#else
        Execute.Assertion
#endif
            .BecauseOf(because, becauseArgs)
            .ForCondition(subject.WasExpected)
            .FailWith("request should be expected, but it was unexpected");
    }

    /// <summary>
    /// Asserts that the request was unexpected (did not match any mock).
    /// </summary>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
    /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <paramref name="because" />.
    /// </param>
    public void BeUnexpected(string because = "", params object[] becauseArgs)
    {
#if FA8
        AssertionChain.GetOrCreate()
#else
        Execute.Assertion
#endif
            .BecauseOf(because, becauseArgs)
            .ForCondition(!subject.WasExpected)
            .FailWith("request should be unexpected, but it was expected");
    }

    protected override string Identifier
    {
        get => "request";
    }
}

/// <summary>
/// Assertion chain for a specific captured request located from a RequestCollection.
/// </summary>
public class ContainedRequestAssertions : ReferenceTypeAssertions<CapturedRequest, ContainedRequestAssertions>
{
    private readonly CapturedRequest request;

    public ContainedRequestAssertions(CapturedRequest request)
#if FA8
        : base(request, AssertionChain.GetOrCreate())
#else
        : base(request)
#endif
    {
        this.request = request;
    }

    /// <summary>
    /// Asserts that the request body matches a wildcard pattern.
    /// </summary>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
    /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <paramref name="because" />.
    /// </param>
    public ContainedRequestAssertions WithBody(string wildcard, string because = "", params object[] becauseArgs)
    {
        var body = request.Body;

#if FA8
        AssertionChain.GetOrCreate()
#else
        Execute.Assertion
#endif
            .BecauseOf(because, becauseArgs)
            .ForCondition(body is not null && body.MatchesWildcard(wildcard))
            .FailWith("Expected request body to match wildcard pattern {0}, but it was {1}", wildcard, body ?? "<null>");

        return this;
    }

    /// <summary>
    /// Asserts that the request body matches the provided JSON, ignoring whitespace/layout differences.
    /// </summary>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
    /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <paramref name="because" />.
    /// </param>
    public ContainedRequestAssertions WithBodyMatchingJson(string json, string because = "", params object[] becauseArgs)
    {
        if (string.IsNullOrEmpty(json))
        {
#if FA8
            AssertionChain.GetOrCreate()
#else
            Execute.Assertion
#endif
                .FailWith("Cannot compare the JSON body with <null>");
        }

        try
        {
            using var expected = JsonDocument.Parse(json);
            using var actual = JsonDocument.Parse(request.Body ?? string.Empty);

#if FA8
            AssertionChain.GetOrCreate()
#else
            Execute.Assertion
#endif
                .BecauseOf(because, becauseArgs)
                .ForCondition(expected.RootElement.JsonEquals(actual.RootElement))
                .FailWith("Expected request body to be JSON-equivalent to:{1}{0}{1}but was:{1}{2}", json, Environment.NewLine,
                    request.Body ?? "<null>");
        }
        catch (JsonException)
        {
#if FA8
            AssertionChain.GetOrCreate()
#else
            Execute.Assertion
#endif
                .FailWith("Request body is not valid JSON: {0}", request.Body ?? "<null>");
        }

        return this;
    }

    /// <summary>
    /// Deserializes the JSON request body to a particular type and asserts it is equivalent to the expected value.
    /// </summary>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
    /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <paramref name="because" />.
    /// </param>
    public ContainedRequestAssertions WithBodyEquivalentTo<T>(T expected, string because = "", params object[] becauseArgs)
    {
        T? actualObj = default;
        try
        {
            actualObj = request.Body is null ? default : JsonSerializer.Deserialize<T>(request.Body);
        }
        catch (JsonException exception)
        {
#if FA8
            AssertionChain.GetOrCreate()
#else
            Execute.Assertion
#endif
                .BecauseOf(because, becauseArgs)
                .ForCondition(actualObj is not null)
                .FailWith("Expected the request body to be deserializable to {0}{because}, but {1} failed to deserialize: {2}",
                    typeof(T).Name, request.Body ?? "<null>", exception.Message);
        }

        actualObj.Should().BeEquivalentTo(expected, because, becauseArgs);
        return this;
    }

    /// <summary>
    /// Deserializes the request body as a dictionary and asserts it is equivalent to the expected dictionary.
    /// </summary>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
    /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <paramref name="because" />.
    /// </param>
    public ContainedRequestAssertions WithBodyHavingPropertiesOf(IDictionary<string, string> expectation, string because = "",
        params object[] becauseArgs)
    {
#if FA8
        AssertionChain.GetOrCreate()
#else
        Execute.Assertion
#endif
            .BecauseOf(because, becauseArgs)
            .ForCondition(request.Body is not null)
            .FailWith("Expected the request body to be deserializable to a dictionary{because}, but the body is <null>");

        var actual = JsonSerializer.Deserialize<IDictionary<string, string>>(request.Body!);
#if FA8
        AssertionChain.GetOrCreate()
#else
        Execute.Assertion
#endif
            .BecauseOf(because, becauseArgs)
            .ForCondition(actual is not null)
            .FailWith("Expected the request body to be deserializable to a dictionary{because}, but deserialization failed");

        actual.Should().BeEquivalentTo(expectation, because, becauseArgs);
        return this;
    }

    protected override string Identifier
    {
        get => "captured request";
    }
}

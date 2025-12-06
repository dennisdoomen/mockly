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
    public AndConstraint<HttpMockAssertions> HaveAllRequestsCalled(string because = "", params object[] becauseArgs)
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

        return new AndConstraint<HttpMockAssertions>(this);
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
    public AndConstraint<RequestCollectionAssertions> NotContainUnexpectedCalls(string because = "", params object[] becauseArgs)
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

        return new AndConstraint<RequestCollectionAssertions>(this);
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
#if FA8
        AssertionChain.GetOrCreate()
#else
        Execute.Assertion
#endif
            .BecauseOf(because, becauseArgs)
            .ForCondition(subject.Any())
            .FailWith("Expected at least one request to have been captured{because}, but none were found");

        return new ContainedRequestAssertions(subject.ToArray());
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
        CapturedRequest[] matchingRequests = subject
            .Where(r => r.Uri is not null && r.Uri.ToString().MatchesWildcard(urlPattern))
            .ToArray();

        var failureMessage = new StringBuilder();

        if (subject.Count == 0)
        {
            failureMessage.AppendFormat(
                "Expected a request for URL pattern \"{0}\"{{because}}, but no requests where captured at all", urlPattern);
        }
        else if (matchingRequests.Length == 0)
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
            // The assertion succeeded
        }

#if FA8
        AssertionChain.GetOrCreate()
#else
        Execute.Assertion
#endif
            .BecauseOf(because, becauseArgs)
            .ForCondition(matchingRequests.Length > 0)
            .FailWith(failureMessage.ToString());

        return new ContainedRequestAssertions(matchingRequests);
    }

    /// <summary>
    /// Asserts that the collection does not contain a request matching the given URI.
    /// </summary>
    public AndConstraint<RequestCollectionAssertions> NotContainRequestFor(Uri uri, string because = "",
        params object[] becauseArgs)
    {
        return NotContainRequestFor(uri.ToString(), because, becauseArgs);
    }

    /// <summary>
    /// Asserts that the collection does not contain a request matching the given URL pattern.
    /// </summary>
    public AndConstraint<RequestCollectionAssertions> NotContainRequestFor(string urlPattern, string because = "",
        params object[] becauseArgs)
    {
        var matches = subject.Where(r => r.Uri is not null && r.Uri.ToString().MatchesWildcard(urlPattern)).ToList();

#if FA8
        AssertionChain.GetOrCreate()
#else
        Execute.Assertion
#endif
            .BecauseOf(because, becauseArgs)
            .ForCondition(!matches.Any())
            .FailWith(
                matches.Any()
                    ? $"Did not expect a request for URL pattern \"{urlPattern}\"{{because}}, but found:{Environment.NewLine}{string.Join(Environment.NewLine, matches.Select(r => $" - {r}"))}"
                    : $"Did not expect a request for URL pattern \"{urlPattern}\"{{because}}, but none were found")
            ;

        return new AndConstraint<RequestCollectionAssertions>(this);
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
    public AndConstraint<CapturedRequestAssertions> BeExpected(string because = "", params object[] becauseArgs)
    {
#if FA8
        AssertionChain.GetOrCreate()
#else
        Execute.Assertion
#endif
            .BecauseOf(because, becauseArgs)
            .ForCondition(subject.WasExpected)
            .FailWith("request should be expected, but it was unexpected");

        return new AndConstraint<CapturedRequestAssertions>(this);
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
    public AndConstraint<CapturedRequestAssertions> BeUnexpected(string because = "", params object[] becauseArgs)
    {
#if FA8
        AssertionChain.GetOrCreate()
#else
        Execute.Assertion
#endif
            .BecauseOf(because, becauseArgs)
            .ForCondition(!subject.WasExpected)
            .FailWith("request should be unexpected, but it was expected");

        return new AndConstraint<CapturedRequestAssertions>(this);
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
    private readonly CapturedRequest[] requests;

    // Internal factory ctor: multiple requests (not part of public API)
    internal ContainedRequestAssertions(CapturedRequest[] requests)
#if FA8
        : base(requests.Length > 0 ? requests[0] : throw new ArgumentException("requests cannot be empty", nameof(requests)),
            AssertionChain.GetOrCreate())
#else
        : base(requests.Length > 0 ? requests[0] : throw new ArgumentException("requests cannot be empty", nameof(requests)))
#endif
    {
        this.requests = requests;
    }

    /// <summary>
    /// Asserts that the body of at least one of the matching requests matches a wildcard pattern.
    /// </summary>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
    /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <paramref name="because" />.
    /// </param>
    /// <returns>
    /// A construct that allows chaining more assertions on the matching <see cref="CapturedRequest"/>
    /// </returns>
    public AndWhichConstraint<ContainedRequestAssertions, CapturedRequest> WithBody(string wildcard, string because = "",
        params object[] becauseArgs)
    {
        foreach (CapturedRequest request in requests)
        {
            if (request.Body is not null && request.Body.MatchesWildcard(wildcard))
            {
                return new AndWhichConstraint<ContainedRequestAssertions, CapturedRequest>(this, request);
            }
        }

        if (requests.Length == 1)
        {
#if FA8
            AssertionChain.GetOrCreate()
#else
            Execute.Assertion
#endif
                .BecauseOf(because, becauseArgs)
                .FailWith("Expected request body to match wildcard pattern {0}, but it was {1}", wildcard,
                    requests[0].Body ?? "<null>");
        }
        else
        {
#if FA8
            AssertionChain.GetOrCreate()
#else
            Execute.Assertion
#endif
                .BecauseOf(because, becauseArgs)
                .FailWith("Expected at least one request having a body that matches wildcard pattern {0}, but none did",
                    wildcard);
        }

        return new AndWhichConstraint<ContainedRequestAssertions, CapturedRequest>(this, []);
    }

    /// <summary>
    /// Asserts that at least one of the matching requests has a body matching the provided JSON, ignoring whitespace/layout differences.
    /// </summary>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
    /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <paramref name="because" />.
    /// </param>
    /// <returns>
    /// A construct that allows chaining more assertions on the matching <see cref="CapturedRequest"/>
    /// </returns>
    public AndWhichConstraint<ContainedRequestAssertions, CapturedRequest> WithBodyMatchingJson(string json, string because = "",
        params object[] becauseArgs)
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

        // Single-request behavior: keep original semantics/messages
        if (requests.Length == 1)
        {
            try
            {
                using var expected = JsonDocument.Parse(json);
                using var actual = JsonDocument.Parse(requests[0].Body ?? string.Empty);

#if FA8
                AssertionChain.GetOrCreate()
#else
                Execute.Assertion
#endif
                    .BecauseOf(because, becauseArgs)
                    .ForCondition(expected.RootElement.JsonEquals(actual.RootElement))
                    .FailWith("Expected request body to be JSON-equivalent to:{1}{0}{1}but was:{1}{2}", json, Environment.NewLine,
                        requests[0].Body ?? "<null>");
            }
            catch (JsonException)
            {
#if FA8
                AssertionChain.GetOrCreate()
#else
                Execute.Assertion
#endif
                    .FailWith("Request body is not valid JSON: {0}", requests[0].Body ?? "<null>");
            }

            return new AndWhichConstraint<ContainedRequestAssertions, CapturedRequest>(this, []);
        }

        // Multiple requests: succeed if any body JSON-matches
        using var expectedDoc = JsonDocument.Parse(json);
        foreach (var request in requests)
        {
            try
            {
                using var actualDoc = JsonDocument.Parse(request.Body ?? string.Empty);
                if (expectedDoc.RootElement.JsonEquals(actualDoc.RootElement))
                {
                    return new AndWhichConstraint<ContainedRequestAssertions, CapturedRequest>(this, request);
                }
            }
            catch (JsonException)
            {
                // Ignore invalid JSON bodies when multiple requests are present; we'll fail after checking all
            }
        }

#if FA8
        AssertionChain.GetOrCreate()
#else
        Execute.Assertion
#endif
            .BecauseOf(because, becauseArgs)
            .FailWith("Expected at least one request body to be JSON-equivalent to:{1}{0}", json, Environment.NewLine);

        return new AndWhichConstraint<ContainedRequestAssertions, CapturedRequest>(this, []);
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
    public AndWhichConstraint<ContainedRequestAssertions, CapturedRequest> WithBodyEquivalentTo<T>(T expected,
        string because = "",
        params object[] becauseArgs)
    {
        string[] failures = [];
        foreach (CapturedRequest request in requests)
        {
            T? actual = request.Body is null ? default : JsonSerializer.Deserialize<T>(request.Body!);
            if (actual is not null)
            {
                using var scope = new AssertionScope();
                actual.Should().BeEquivalentTo(expected, because, becauseArgs);

                failures = scope.Discard();
                if (failures.Length == 0)
                {
                    return new AndWhichConstraint<ContainedRequestAssertions, CapturedRequest>(this, request);
                }
            }
        }

        if (requests.Length == 1)
        {
#if FA8
            AssertionChain.GetOrCreate()
#else
            Execute.Assertion
#endif
                .BecauseOf(because, becauseArgs)
                .FailWith(
                    "Expected the request body to be equivalent to the expectation{because}, but it failed with: {0}",
                    string.Join(Environment.NewLine, failures));
        }
        else
        {
#if FA8
            AssertionChain.GetOrCreate()
#else
            Execute.Assertion
#endif
                .BecauseOf(because, becauseArgs)
                .FailWith("Expected at least one request body to be equivalent to the expected object{because}, but none were");
        }

        return new AndWhichConstraint<ContainedRequestAssertions, CapturedRequest>(this, []);
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
    public AndWhichConstraint<ContainedRequestAssertions, CapturedRequest> WithBodyHavingPropertiesOf(
        IDictionary<string, string> expectation,
        string because = "",
        params object[] becauseArgs)
    {
        string[] failures = [];
        foreach (CapturedRequest request in requests)
        {
            if (request.Body is null)
            {
                continue;
            }

            var dictionary = JsonSerializer.Deserialize<IDictionary<string, object>>(request.Body);
            if (dictionary is not null)
            {
                var actual = dictionary.ToDictionary(x => x.Key, x => x.Value.ToString());

                using var scope = new AssertionScope();
                actual.Should().BeEquivalentTo(expectation, because, becauseArgs);

                failures = scope.Discard();
                if (failures.Length == 0)
                {
                    return new AndWhichConstraint<ContainedRequestAssertions, CapturedRequest>(this, request);
                }
            }
        }

        if (requests.Length == 1)
        {
#if FA8
            AssertionChain.GetOrCreate()
#else
            Execute.Assertion
#endif
                .BecauseOf(because, becauseArgs)
                .FailWith("Expected the top-level properties of the request body to be equivalent to the provided dictionary{because}, but it failed with: ",
                    string.Join(Environment.NewLine, failures));
        }
        else
        {
#if FA8
            AssertionChain.GetOrCreate()
#else
        Execute.Assertion
#endif
                .BecauseOf(because, becauseArgs)
                .FailWith("Expected at least one request body to have the expected properties{because}, but none did");
        }

        return new AndWhichConstraint<ContainedRequestAssertions, CapturedRequest>(this, []);
    }

    /// <summary>
    /// Asserts the body contains a top-level property with the given key and value.
    /// </summary>
    /// <param name="because">
    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
    /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
    /// </param>
    /// <param name="becauseArgs">
    /// Zero or more objects to format using the placeholders in <paramref name="because" />.
    /// </param>
    public AndWhichConstraint<ContainedRequestAssertions, CapturedRequest> WithBodyHavingProperty(string key, string value,
        string because = "",
        params object[] becauseArgs)
    {
        if (requests.Length == 1)
        {
#if FA8
            AssertionChain.GetOrCreate()
#else
            Execute.Assertion
#endif
                .BecauseOf(because, becauseArgs)
                .ForCondition(requests[0].Body is not null)
                .FailWith("Expected the request body to contain a property with key {0}, but the body is <null>", key);

            var singleDict = JsonSerializer.Deserialize<IDictionary<string, object>>(requests[0].Body!);
#if FA8
            AssertionChain.GetOrCreate()
#else
            Execute.Assertion
#endif
                .BecauseOf(because, becauseArgs)
                .ForCondition(singleDict is not null)
                .FailWith("Expected the request body to be deserializable to a dictionary{because}, but deserialization failed");

            var actual = singleDict!.ToDictionary(x => x.Key, x => x.Value.ToString());
            actual.Should().Contain(key, value);

            return new AndWhichConstraint<ContainedRequestAssertions, CapturedRequest>(this, requests[0]);
        }

        foreach (var req in requests)
        {
            if (req.Body is null)
            {
                continue;
            }

            IDictionary<string, object>? dict = null;
            try
            {
                dict = JsonSerializer.Deserialize<IDictionary<string, object>>(req.Body!);
            }
            catch (JsonException)
            {
                continue;
            }

            if (dict is null)
            {
                continue;
            }

            var actual = dict.ToDictionary(x => x.Key, x => x.Value.ToString());
            if (actual.TryGetValue(key, out var val) && string.Equals(val, value, StringComparison.Ordinal))
            {
                return new AndWhichConstraint<ContainedRequestAssertions, CapturedRequest>(this, req);
            }
        }

#if FA8
        AssertionChain.GetOrCreate()
#else
        Execute.Assertion
#endif
            .BecauseOf(because, becauseArgs)
            .FailWith("Expected at least one request body to contain property {0} with value {1}{because}, but none did", key,
                value);

        return new AndWhichConstraint<ContainedRequestAssertions, CapturedRequest>(this, []);
    }

    protected override string Identifier
    {
        get => "captured request";
    }
}

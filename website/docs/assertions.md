---
sidebar_position: 5
---

# Assertions

Mockly integrates naturally with [FluentAssertions](https://fluentassertions.com/) through the `FluentAssertions.Mockly` extension packages. These packages provide a set of intention-revealing extension methods to verify that your system under test interacted with the HTTP mocks as expected.

## Installation

Depending on which version of FluentAssertions you are using, install the corresponding Mockly assertion package:

### For FluentAssertions v8.x

```bash
dotnet add package FluentAssertions.Mockly.v8
```

### For FluentAssertions v7.x

```bash
dotnet add package FluentAssertions.Mockly.v7
```

All the assertions below become available as soon as you install one of these packages, through a `Should()` extension method on `HttpMock`, `RequestCollection`, `CapturedRequest` and the builder returned by `mock.ForGet()`/`mock.ForPost()`/etc.

## HttpMock Assertions

You can perform high-level assertions on the `HttpMock` instance itself to ensure all configured mocks were utilized.

```csharp
var mock = new HttpMock();
mock.ForGet("/api/users").RespondsWithStatus(HttpStatusCode.OK);

// ... perform actions ...

// Verify that all configured mocks were called at least once
mock.Should().HaveAllRequestsCalled();
```

You can also verify that a set of mocks were first invoked in a particular order:

```csharp
var mock1 = mock.ForGet("/api/first").RespondsWithStatus(HttpStatusCode.OK);
var mock2 = mock.ForGet("/api/second").RespondsWithStatus(HttpStatusCode.OK);

// ... perform actions ...

mock.Should().HaveCalledInOrder(mock1, mock2);
```

`HaveCalledInOrder` compares the first captured request for each mock, so it still passes if `mock1` (or `mock2`) was invoked multiple times, as long as the first observed invocation of `mock1` happened before the first observed invocation of `mock2`.

### Unexpected Requests

You can verify that no unexpected calls were made to the mock:

```csharp
mock.Requests.Should().NotContainUnexpectedCalls();
```

## Setup Assertions

If you keep a reference to a mock setup (the object returned by `ForGet()`, `ForPost()`, etc.), you can assert on its invocation count directly.

```csharp
var userMock = mock.ForGet("/api/users/*").RespondsWithStatus(HttpStatusCode.OK);

// ... perform actions ...

// At least one invocation
userMock.Should().HaveBeenCalled();

// Exactly one invocation
userMock.Should().HaveBeenCalled(1);
userMock.Should().HaveBeenCalledTimes(1);

// Never invoked
userMock.Should().NotHaveBeenCalled();
```

Like any other FluentAssertions method, these accept an optional `because`/`becauseArgs` pair to explain the reason for the assertion:

```csharp
userMock.Should().HaveBeenCalled("the user list should have been refreshed");
```

## Request Collection Assertions

When using a `RequestCollection` to capture requests — either the mock's own `mock.Requests`, or one you pass to `CollectingRequestsIn` — you can use specialized assertions to inspect the captured data. `RequestCollection` also supports the standard FluentAssertions collection assertions, such as `NotBeEmpty()` and `HaveCount()`.

```csharp
var captured = new RequestCollection();
mock.ForPost().WithPath("/api/data").CollectingRequestsIn(captured).RespondsWithStatus(HttpStatusCode.OK);

// ... perform actions ...

// Check if any requests were captured at all
captured.Should().NotBeEmpty();
captured.Should().HaveCount(2);

// Assert on the presence of any request, or a request matching a URL pattern
captured.Should().ContainRequest();
captured.Should().ContainRequestFor("/api/data");
captured.Should().NotContainRequestFor("/api/other");
```

`ContainRequestFor` and `NotContainRequestFor` also accept an `HttpMethod`, or a method prefix directly in the URL pattern string, to scope the match to a specific verb:

```csharp
captured.Should().ContainRequestFor(HttpMethod.Post, "/api/data");
captured.Should().ContainRequestFor("POST /api/data");

captured.Should().NotContainRequestFor(HttpMethod.Delete, "/api/data");
captured.Should().NotContainRequestFor("DELETE /api/data");
```

You can also verify that no requests were captured at all, regardless of URL:

```csharp
captured.Should().NotContainRequest();
```

### Count, Order, and Timing

`ContainRequestsFor` verifies how many matching requests were captured, using the standard
FluentAssertions occurrence constraints. `ContainRequestsInOrder` verifies that requests were
captured in the given sequence, and `AllHaveBeenSentWithin` verifies that the first and last
captured request were sent no further apart than the given `TimeSpan`.

```csharp
captured.Should().ContainRequestsFor("/api/data", Exactly.Times(2));
captured.Should().ContainRequestsFor(HttpMethod.Post, "/api/data", AtLeast.Once());

captured.Should().ContainRequestsInOrder("/api/first", "/api/second");
captured.Should().ContainRequestsInOrder(
    (HttpMethod.Post, "/api/data"),
    (HttpMethod.Get, "/api/data"));

captured.Should().AllHaveBeenSentWithin(TimeSpan.FromSeconds(1));
```

## Chaining Assertions On a Matched Request

`ContainRequest()` and `ContainRequestFor(...)` return a `ContainedRequestAssertions` object for the matching request(s), which you can use to assert on headers, body, query string and more. Each of the assertions below succeeds as soon as *any* of the matched requests satisfies it, and returns an `AndWhichConstraint`, so you can keep chaining with `.And`:

```csharp
captured.Should().ContainRequestFor("/api/data")
    .WithHeader("X-Custom-Header", "Expected*")
    .And.WithBody("*part-of-body*")
    .And.WithBearerToken("expected-token");
```

### Available Chained Assertions

On the result of `ContainRequest()` / `ContainRequestFor(...)`, you can use:

- `WithHeader(name)` / `WithHeader(name, valuePattern)` — the request has the given header, optionally with a value matching a wildcard pattern.
- `WithBearerToken()` / `WithBearerToken(tokenPattern)` — the request has an `Authorization: Bearer` header, optionally with a token matching a wildcard pattern.
- `WithBody(pattern)` — the request body matches a wildcard pattern.
- `WithQueryParam(name)` / `WithQueryParam(name, valuePattern)` — the request's query string has the given parameter, optionally with a value matching a wildcard pattern.
- `WithFormField(name, valuePattern)` — the URL-encoded form body has the given field with a value matching a wildcard pattern.
- `WithResponseHeader(name)` / `WithResponseHeader(name, value)` — the response received for the request has the given header, optionally with a value matching a wildcard pattern.
- The [body assertions](#body-assertions-on-captured-requests) below (`WithBodyMatchingJson`, `WithBodyEquivalentTo`, ...).

To drop back down to the matched `CapturedRequest` itself — for example, to assert it `BeExpected()` — use `.Which`:

```csharp
captured.Should().ContainRequestFor("/api/data")
    .WithBearerToken()
    .Which.Should().BeExpected();
```

### Excluding Sensitive Request Data

Use `Without...` assertions after `ContainRequestFor` to verify that every matching request
omits sensitive data. A matching request with the rejected data causes the assertion to fail.

```csharp
captured.Should().ContainRequestFor("/api/users")
    .WithoutHeader("X-Internal-Token")
    .And.WithoutQueryParam("debug")
    .And.WithoutBodyProperty("password");
```

Header names are matched without case sensitivity. Query parameter names and JSON property names
are case-sensitive. `WithoutBodyProperty` only inspects the top-level properties of a JSON object.
It fails for missing, empty, malformed, or non-object JSON bodies.

## Body Assertions on Captured Requests

Use these specialized assertions to verify the JSON body of captured requests:

```csharp
// Assert JSON-equivalence using a JSON string (ignores formatting/ordering)
mock.Requests.Should().ContainRequest()
    .WithBodyMatchingJson("{ \"id\": 1, \"name\": \"John\" }");

// Assert the body deserializes and is equivalent to an object graph
var expected = new { id = 1, name = "John" };
mock.Requests.Should().ContainRequest()
    .WithBodyEquivalentTo(expected);

// Assert the body has at least the given top-level properties (deserialized as a dictionary);
// extra properties in the body are ignored
var expectedProps = new Dictionary<string, string>
{
    ["id"] = "1",
    ["name"] = "John"
};
mock.Requests.Should().ContainRequest()
    .WithBodyHavingPropertiesOf(expectedProps);

// Assert the body has exactly the given top-level properties, no more, no less
mock.Requests.Should().ContainRequest()
    .WithBodyHavingPropertiesEqualTo(expectedProps);

// Assert a single top-level property and its value
mock.Requests.Should().ContainRequest()
    .WithBodyHavingProperty("id", "1");
```

:::info
These assertions require the request body to be available in memory. If you disabled `HttpMock.PrefetchBody`, these assertions will fail as `CapturedRequest.Body` will be `null`.
:::

## Individual Request Assertions

You can also assert on individual `CapturedRequest` objects, for example after locating one via `captured.First()` or through `.Which` on a chained assertion.

```csharp
var request = captured.First();

request.Should().BeExpected();
```

Or the opposite, to confirm a request was *not* matched by any configured mock:

```csharp
request.Should().BeUnexpected();
```

### Simulated Failures

When a mock simulates a failure, verify the captured request directly.

```csharp
var request = captured.First();

request.Should().BeASimulatedFailure();
```

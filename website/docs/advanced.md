---
sidebar_position: 4
---

# Advanced Features

Explore advanced features and patterns for power users.

## Custom Matchers

Use predicates for advanced matching logic:

```csharp
mock.ForGet()
    .WithPath("/api/data")
    .With(request => request.Headers.Contains("X-API-Key"))
    .RespondsWithStatus(HttpStatusCode.OK);
```

### Inspect Request Body

```csharp
mock.ForPost()
    .WithPath("/api/test")
    .With(req => req.Body!.Contains("something"))
    .RespondsWithStatus(HttpStatusCode.NoContent);
```

### Async Predicate Matching

```csharp
mock.ForGet()
    .WithPath("/api/async")
    .With(async req =>
    {
        await Task.Delay(1);
        return req.Uri!.Query == "?q=test";
    })
    .RespondsWithStatus(HttpStatusCode.OK);
```

:::info
If no mock matches, an `UnexpectedRequestException` is thrown when `FailOnUnexpectedCalls` is `true` (default).
:::

## Body Matching

Match request bodies using different strategies:

### Wildcard Pattern

```csharp
mock.ForPost()
    .WithPath("/api/test")
    .WithBody("*something*")
    .RespondsWithStatus(HttpStatusCode.NoContent);
```

### JSON Equivalence

Layout and whitespace independent, using a raw JSON string:

```csharp
mock.ForPost()
    .WithPath("/api/json")
    .WithBodyMatchingJson("{\"name\": \"John\", \"age\": 30}")
    .RespondsWithStatus(HttpStatusCode.NoContent);
```

:::warning
If the body cannot be parsed as JSON for `WithBodyMatchingJson`, a `RequestMatchingException` is thrown.
:::

### Object Serialized to JSON

Pass an object directly and let Mockly serialize it to JSON for matching. This is useful when you have a strongly-typed request body:

```csharp
mock.ForPatch()
    .WithPath("/api/relationships/42")
    .WithBody(new
    {
        EntityKey = "TheRuleKey",
        RepresentativeId = "abc123"
    })
    .RespondsWithStatus(HttpStatusCode.NoContent);
```

The object is serialized using `JsonSerializer` with default options and compared to the request body using JSON equivalence, ignoring differences in whitespace and layout.

#### Custom JSON Options

You can supply custom `JsonSerializerOptions` for the body matching:

```csharp
var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

mock.ForPost()
    .WithPath("/api/data")
    .Using(options)
    .WithBody(new { UserId = 42, UserName = "Alice" })
    .RespondsWithStatus(HttpStatusCode.NoContent);
```

### Regular Expression

```csharp
mock.ForPost()
    .WithPath("/api/test")
    .WithBodyMatchingRegex(".*something.*")
    .RespondsWithStatus(HttpStatusCode.NoContent);
```

## Request Body Prefetching

By default, Mockly prefetches the request body for matchers. You can disable this to defer reading content inside your predicate:

```csharp
var mock = new HttpMock { PrefetchBody = false };

RequestInfo? captured = null;

mock.ForPost()
    .WithPath("/api/test")
    .With(req =>
    {
        captured = req; // req.Body can be read lazily here by your predicate
        return true;
    })
    .RespondsWithStatus(HttpStatusCode.OK);
```

### What PrefetchBody Does

- **Purpose**: When `PrefetchBody` is `true` (default), Mockly eagerly reads and caches the HTTP request body into `RequestInfo.Body` so that matchers and later assertions can inspect it without re-reading the stream.
- **When to disable**: Turn it off for scenarios with large or streaming content where reading the body up front is expensive or undesirable. In that case, `RequestInfo.Body` will be `null` unless your own predicate reads it.
- **Impact on assertions**: Body-based assertions require the body to be available. Keep `PrefetchBody` enabled if you plan to assert on the request body after the call.

## Limiting Mock Invocations

Sometimes you want a mock to respond only a limited number of times. You can restrict a mock using the fluent methods `Once()`, `Twice()`, or `Times(int count)` on the request builder.

```csharp
var mock = new HttpMock();

// Single-use response
mock.ForGet()
    .WithPath("/api/item")
    .RespondsWithStatus(HttpStatusCode.OK)
    .Once();

// Exactly two times
mock.ForPost()
    .WithPath("/api/items")
    .RespondsWithJsonContent(new { ok = true })
    .Twice();

// Exactly N times
mock.ForDelete()
    .WithPath("/api/items/*")
    .RespondsWithEmptyContent()
    .Times(3);
```

### Behavior Notes

- Exhausted mocks are skipped when matching. If no other non-exhausted mock matches and `FailOnUnexpectedCalls` is `true` (default), an `UnexpectedRequestException` is thrown.
- The mocks are evaluated in the order they were created.
- The default for mocks without limits is unlimited invocations
- The verification helpers consider limits:
  - `HttpMock.AllMocksInvoked` returns `true` only when each mock has been called at least once or has reached its configured `Times(..)` limit.
  - `HttpMock.GetUninvokedMocks()` lists mocks that haven't reached their required count (or have 0 calls for unlimited mocks).

## Request Collection

Capture requests for specific mocks:

```csharp
var capturedRequests = new RequestCollection();

mock.ForPatch()
    .WithPath("/api/update")
    .CollectingRequestsIn(capturedRequests)
    .RespondsWithStatus(HttpStatusCode.NoContent);

// After making requests
capturedRequests.Count.Should().Be(2);
capturedRequests.First().WasExpected.Should().BeTrue();
```

## Assertions

Mockly provides extensive support for test assertions through **FluentAssertions**. For a full guide on available assertions for mocks, collections, and requests, see the [Assertions](./assertions.md) page.

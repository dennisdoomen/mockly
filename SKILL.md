---
name: mockly
description: >
  Helps write tests using Mockly — a fluent HTTP mocking library for .NET that intercepts HttpClient
  calls. Use this skill when writing unit or integration tests that need to mock HTTP requests,
  configure responses (status codes, JSON, OData, raw content), capture and inspect requests,
  limit mock invocations, or assert HTTP interactions with FluentAssertions.
---

# Mockly

Fluent HTTP mocking library for .NET. Intercepts `HttpClient` calls via a custom `HttpMessageHandler`.
Unmatched requests throw `UnexpectedRequestException` by default (`FailOnUnexpectedCalls = true`).
Default base address: `https://localhost/`.

## Setup

```csharp
var mock = new HttpMock();
mock.ForGet().WithPath("/api/users").RespondsWithStatus(HttpStatusCode.OK);
HttpClient client = mock.GetClient(); // or mock.GetClientFactory()
```

## URL Matching

```csharp
// Exact path (leading slash optional); * is wildcard
mock.ForGet().WithPath("/api/users/*").RespondsWithStatus(HttpStatusCode.OK);

// Query: omitting WithQuery() rejects requests that include a query string
mock.ForGet().WithPath("/api/search").WithQuery("?q=*").RespondsWithStatus(HttpStatusCode.OK);
mock.ForGet().WithPath("/api/search").WithAnyQuery().RespondsWithStatus(HttpStatusCode.OK);
mock.ForGet().WithPath("/api/search").WithoutQuery().RespondsWithStatus(HttpStatusCode.OK);

// Scheme / host
mock.ForGet().ForHttps().ForHost("api.example.com").WithPath("/data").RespondsWithStatus(HttpStatusCode.OK);
mock.ForGet().ForHttp().ForAnyHost().WithPath("/data").RespondsWithStatus(HttpStatusCode.OK);

// Full-URL shorthand (scheme + host + path + query, all support *)
mock.ForGet("https://api.example.com/users/*?q=*").RespondsWithStatus(HttpStatusCode.OK);
mock.ForPatch("http://*.example.com/*").RespondsWithStatus(HttpStatusCode.OK);
```

## Request Body Matching

```csharp
mock.ForPost().WithPath("/api/data").WithBody("*keyword*").RespondsWithStatus(HttpStatusCode.NoContent);
mock.ForPost().WithPath("/api/data").WithBody(new { name = "John" }).RespondsWithStatus(HttpStatusCode.NoContent); // JSON object
mock.ForPost().WithPath("/api/data").WithBodyMatchingJson("{\"name\":\"John\"}").RespondsWithStatus(HttpStatusCode.NoContent);
mock.ForPost().WithPath("/api/data").WithBodyMatchingRegex(".*keyword.*").RespondsWithStatus(HttpStatusCode.NoContent);

// Custom predicate (sync or async) — also use .With() for header/URI checks
mock.ForPost().WithPath("/api/data").With(req => req.Body!.Contains("keyword")).RespondsWithStatus(HttpStatusCode.NoContent);
mock.ForGet().WithPath("/api/secure").With(req => req.Headers.Contains("X-API-Key")).RespondsWithStatus(HttpStatusCode.OK);
```

## Responses

```csharp
mock.ForDelete().WithPath("/api/items/1").RespondsWithStatus(HttpStatusCode.NoContent);
mock.ForGet().WithPath("/api/user").RespondsWithJsonContent(new { Id = 1, Name = "Alice" });
mock.ForGet().WithPath("/api/user").RespondsWithJsonContent(HttpStatusCode.Created, new { Id = 1 });
mock.ForGet().WithPath("/api/text").RespondsWithContent("Hello"); // 200 OK, application/json
mock.ForGet().WithPath("/api/text").RespondsWithContent(HttpStatusCode.OK, "<xml/>", "text/xml");
mock.ForGet().WithPath("/api/empty").RespondsWithEmptyContent(); // 204 No Content
mock.ForGet().WithPath("/api/binary").RespondsWith(new ByteArrayContent(bytes));
mock.ForGet().WithPath("/api/custom").RespondsWith(_ => new HttpResponseMessage(HttpStatusCode.OK));

// OData v4: wraps in { "value": [...] }
mock.ForGet().WithPath("/odata/items").RespondsWithODataResult(new { Id = 1 });
mock.ForGet().WithPath("/odata/items").RespondsWithODataResult(HttpStatusCode.OK, items, "https://localhost/$metadata#Items");
```

> `HttpContent` instances are reused across calls — use the lambda overload for mocks called multiple times.

## Request Capture

```csharp
// Global log
mock.Requests.HasUnexpectedRequests // bool
mock.Requests.First().WasExpected   // bool; .Uri, .Method, .Body, .Timestamp, .Response also available

// Scoped collection
var captured = new RequestCollection();
mock.ForPatch().WithPath("/api/items/1").CollectingRequestsIn(captured).RespondsWithStatus(HttpStatusCode.NoContent);
captured.Count.Should().Be(1);
```

## Invocation Limits

```csharp
mock.ForGet().WithPath("/api/once").RespondsWithStatus(HttpStatusCode.OK).Once();
mock.ForGet().WithPath("/api/twice").RespondsWithStatus(HttpStatusCode.OK).Twice();
mock.ForGet().WithPath("/api/data").RespondsWithStatus(HttpStatusCode.OK).Times(3);

mock.AllMocksInvoked.Should().BeTrue();   // true when all mocks hit required count
mock.GetUninvokedMocks();                  // IEnumerable<RequestMock>
```

## Response Latency

```csharp
// Delay the response to test timeout / cancellation / resilience behavior.
mock.ForGet().WithPath("/slow").RespondsWithStatus(HttpStatusCode.OK).After(TimeSpan.FromSeconds(2));
// A shorter HttpClient.Timeout or a cancelled token throws TaskCanceledException/OperationCanceledException.
```

## Reset / Clear

```csharp
mock.Reset(); // clears scheme/host inheritance from previous builder; start fresh
mock.Clear(); // removes all configured mocks
```

## FluentAssertions.Mockly (`FluentAssertions.Mockly.v8` / `.v7`)

```csharp
mock.Should().HaveAllRequestsCalled();
mock.Requests.Should().NotContainUnexpectedCalls();
mock.Requests.Should().NotBeEmpty();
mock.Requests.First().Should().BeExpected();
mock.Requests.First().Should().BeUnexpected();
```

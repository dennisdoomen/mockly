---
name: mockly
description: >
  Helps write tests using Mockly — a fluent HTTP mocking library for .NET that intercepts HttpClient
  calls. Use this skill when writing unit or integration tests that need to mock HTTP requests,
  configure responses (status codes, JSON, OData, raw content), capture and inspect requests,
  limit mock invocations, or assert HTTP interactions with FluentAssertions.
---

# Mockly

Mockly is a fluent HTTP mocking library for .NET. It intercepts `HttpClient` calls via a custom
`HttpMessageHandler` and matches them against configured mocks, returning the configured response.
Unmatched requests throw `UnexpectedRequestException` by default.

## Basic Setup

```csharp
using System.Net;
using Mockly;

var mock = new HttpMock();

mock.ForGet()
    .WithPath("/api/users")
    .RespondsWithStatus(HttpStatusCode.OK);

HttpClient client = mock.GetClient();
var response = await client.GetAsync("https://localhost/api/users");
// response.StatusCode == HttpStatusCode.OK
```

**Key defaults:**
- Base address is `https://localhost/`
- `FailOnUnexpectedCalls = true` (throws `UnexpectedRequestException` for unmatched requests)
- `PrefetchBody = true` (request body is buffered for matching)

---

## HTTP Method Mocking

Start a mock with `ForGet()`, `ForPost()`, `ForPut()`, `ForPatch()`, or `ForDelete()`. Each returns a
`RequestMockBuilder` for fluent configuration, ending with a `RespondsWithXxx` call.

```csharp
// GET
mock.ForGet().WithPath("/api/items").RespondsWithStatus(HttpStatusCode.OK);

// POST
mock.ForPost().WithPath("/api/items").RespondsWithStatus(HttpStatusCode.Created);

// PUT
mock.ForPut().WithPath("/api/items/1").RespondsWithStatus(HttpStatusCode.Accepted);

// PATCH
mock.ForPatch().WithPath("/api/items/1").RespondsWithStatus(HttpStatusCode.NoContent);

// DELETE
mock.ForDelete().WithPath("/api/items/1").RespondsWithStatus(HttpStatusCode.NoContent);
```

---

## URL Matching

### Path Matching

`WithPath()` matches the URI path. Leading slashes are optional. The match is exact unless wildcards
are used.

```csharp
// Exact match (leading slash optional)
mock.ForGet().WithPath("api/users").RespondsWithStatus(HttpStatusCode.OK);
mock.ForGet().WithPath("/api/users").RespondsWithStatus(HttpStatusCode.OK); // same

// Wildcard in path — * matches any sequence of characters
mock.ForGet().WithPath("/api/users/*").RespondsWithStatus(HttpStatusCode.OK);
// matches /api/users/123, /api/users/abc, etc.

// Wildcard with special characters (pipe, parentheses, etc.)
mock.ForDelete().WithPath($"IncomeRelations/{key}").RespondsWithStatus(HttpStatusCode.OK);
mock.ForGet().WithPath("/fnv_collectiveschemes(123*)").RespondsWithStatus(HttpStatusCode.OK);
```

### Query String Matching

A mock configured without `WithQuery()` does **not** match requests that include a query string:

```csharp
// Only matches /api/search with NO query string
mock.ForGet().WithPath("/api/search").RespondsWithStatus(HttpStatusCode.OK);

// Exact query match (? prefix is optional)
mock.ForGet().WithPath("/api/search").WithQuery("?q=hello").RespondsWithStatus(HttpStatusCode.OK);
mock.ForGet().WithPath("/api/search").WithQuery("q=hello").RespondsWithStatus(HttpStatusCode.OK); // same

// Wildcard in query
mock.ForGet().WithPath("/api/search").WithQuery("?q=*").RespondsWithStatus(HttpStatusCode.OK);

// Match any query string
mock.ForGet().WithPath("/api/search").WithAnyQuery().RespondsWithStatus(HttpStatusCode.OK);

// Explicitly require no query string (useful after chain reuse)
mock.ForGet().WithPath("/api/data").WithoutQuery().RespondsWithStatus(HttpStatusCode.OK);
```

### Scheme and Host Matching

```csharp
// Require HTTPS (default)
mock.ForGet().ForHttps().WithPath("/api/data").RespondsWithStatus(HttpStatusCode.OK);

// Require HTTP
mock.ForGet().ForHttp().WithPath("/api/data").RespondsWithStatus(HttpStatusCode.OK);

// Specific host (default: "localhost")
mock.ForGet().ForHost("api.example.com").WithPath("/users").RespondsWithStatus(HttpStatusCode.OK);

// Wildcard host
mock.ForGet().ForHost("*.example.com").WithPath("/data").RespondsWithStatus(HttpStatusCode.OK);

// Any host
mock.ForGet().ForAnyHost().WithPath("/data").RespondsWithStatus(HttpStatusCode.OK);
```

### URL Shortcut Overloads

All `ForXxx()` methods accept a full URL pattern as shorthand. The pattern is parsed into scheme,
host, path, and query components — wildcards are supported throughout.

```csharp
// Full URL pattern
mock.ForGet("https://api.example.com/users/*?q=*").RespondsWithStatus(HttpStatusCode.OK);

// With port
mock.ForGet("https://api.example.com:7777/users/*").RespondsWithStatus(HttpStatusCode.OK);

// Wildcard host and path
mock.ForGet("http://*.example.com:80/*").RespondsWithStatus(HttpStatusCode.OK);

// PATCH with wildcard host
mock.ForPatch("http://*.example.com/*").RespondsWithStatus(HttpStatusCode.OK);

// DELETE — no query in pattern means requests with a query won't match
mock.ForDelete("https://localhost/api/items/*").RespondsWithStatus(HttpStatusCode.NoContent);
// GET https://localhost/api/items/123?force=true → UnexpectedRequestException
```

---

## Request Body Matching

### Wildcard Pattern

```csharp
mock.ForPost()
    .WithPath("/api/data")
    .WithBody("*keyword*")
    .RespondsWithStatus(HttpStatusCode.NoContent);
// matches any body containing "keyword"

// Multiline body — * spans across newlines
mock.ForPost()
    .WithPath("/api/data")
    .WithBody("*condition attribute=\"statecode\" operator=\"eq\" value=\"0\"*")
    .RespondsWithStatus(HttpStatusCode.NoContent);
```

### JSON Equivalence

```csharp
// Match by JSON string (ignores whitespace/formatting)
mock.ForPost()
    .WithPath("/api/data")
    .WithBodyMatchingJson("{\"name\": \"John\", \"age\": 30}")
    .RespondsWithStatus(HttpStatusCode.NoContent);

// Match by serializing an object (uses System.Text.Json)
mock.ForPost()
    .WithPath("/api/data")
    .WithBody(new { name = "John", age = 30 })
    .RespondsWithStatus(HttpStatusCode.NoContent);
```

### Regex

```csharp
mock.ForPost()
    .WithPath("/api/data")
    .WithBodyMatchingRegex(".*keyword.*")
    .RespondsWithStatus(HttpStatusCode.NoContent);
```

### Custom Predicate

```csharp
// Synchronous predicate on RequestInfo
mock.ForPost()
    .WithPath("/api/data")
    .With(req => req.Body!.Contains("keyword"))
    .RespondsWithStatus(HttpStatusCode.NoContent);

// Access headers
mock.ForGet()
    .WithPath("/api/secure")
    .With(req => req.Headers.Contains("X-API-Key"))
    .RespondsWithStatus(HttpStatusCode.OK);

// Async predicate
mock.ForGet()
    .WithPath("/api/async")
    .With(async req =>
    {
        await Task.Delay(1);
        return req.Uri!.Query == "?filter=active";
    })
    .RespondsWithStatus(HttpStatusCode.OK);
```

### Combining Matchers

Multiple `With*` / `WithBody*` calls are AND-combined:

```csharp
mock.ForPost()
    .WithPath("/api/data")
    .WithBody("*something*")         // AND
    .WithBodyMatchingRegex(".*else.*") // AND
    .RespondsWithStatus(HttpStatusCode.NoContent);
```

### Custom JSON Serializer Options

Use `Using()` to control JSON serialization for both body matching and response content:

```csharp
var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

mock.ForPost()
    .WithPath("/api/data")
    .Using(options)
    .WithBody(new { UserId = 42, UserName = "Alice" }) // serialized as { "userId":42,"userName":"Alice" }
    .RespondsWithStatus(HttpStatusCode.NoContent);
```

---

## Response Configuration

All `RespondsWithXxx` methods return a `RequestMockResponseBuilder` for optional invocation limits.

### Status Code Only

```csharp
mock.ForDelete().WithPath("/api/items/1").RespondsWithStatus(HttpStatusCode.NoContent);
```

### JSON Response

```csharp
// Status 200 OK with serialized object
mock.ForGet().WithPath("/api/user").RespondsWithJsonContent(new { Id = 1, Name = "Alice" });

// Custom status code
mock.ForGet().WithPath("/api/user").RespondsWithJsonContent(HttpStatusCode.Created, new { Id = 1 });
```

### Raw String Content

```csharp
// Default: 200 OK, content-type application/json
mock.ForGet().WithPath("/api/text").RespondsWithContent("Hello, World!");

// Custom status code and content type
mock.ForGet().WithPath("/api/text").RespondsWithContent(HttpStatusCode.OK, "<xml/>", "text/xml");
```

### Empty Content

```csharp
// Default: 204 No Content
mock.ForGet().WithPath("/api/empty").RespondsWithEmptyContent();

// Custom status code
mock.ForGet().WithPath("/api/empty").RespondsWithEmptyContent(HttpStatusCode.OK);
```

### HttpContent

```csharp
// Pass any HttpContent instance directly
mock.ForGet().WithPath("/api/binary").RespondsWith(new ByteArrayContent(bytes));
mock.ForGet().WithPath("/api/stream").RespondsWith(HttpStatusCode.Created, new StreamContent(stream));

// Multipart (e.g. OData batch)
var multipart = new MultipartContent("mixed", $"batchresponse_{Guid.NewGuid()}");
multipart.Add(new HttpMessageContent(innerResponse));
mock.ForGet().WithPath("/api/batch").RespondsWith(HttpStatusCode.OK, multipart);
```

> **Note:** The same `HttpContent` instance is reused for all matching requests. For mocks called
> multiple times, use the lambda overload instead.

### Custom Lambda Responder

```csharp
// Full control — receives the RequestInfo and returns HttpResponseMessage
mock.ForGet()
    .WithPath("/api/custom")
    .RespondsWith(_ => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent("custom body")
    });

// Throwing inside the lambda returns 500 InternalServerError
mock.ForGet()
    .WithPath("/api/failing")
    .RespondsWith(_ => throw new InvalidOperationException("simulated failure"));
```

### OData v4 Envelope

Wraps items in `{ "value": [...] }` automatically:

```csharp
// Single item
mock.ForGet().WithPath("/odata/items").RespondsWithODataResult(new { Id = 1, Name = "A" });

// Collection
mock.ForGet().WithPath("/odata/items").RespondsWithODataResult(new object[]
{
    new { Id = 1, Name = "A" },
    new { Id = 2, Name = "B" }
});

// Custom status code
mock.ForGet().WithPath("/odata/items")
    .RespondsWithODataResult(HttpStatusCode.Found, item);

// With @odata.context
mock.ForGet().WithPath("/odata/items")
    .RespondsWithODataResult(HttpStatusCode.OK, items, "https://localhost/$metadata#Items");
```

---

## IResponseBuilder Integration

Implement `IResponseBuilder<T>` to integrate test-data builders directly with response configuration:

```csharp
// IResponseBuilder<T> requires a single Build() method
public class UserBuilder : IResponseBuilder<User>
{
    private int _id = 1;
    private string _name = "Default";

    public UserBuilder WithId(int id) { _id = id; return this; }
    public UserBuilder WithName(string name) { _name = name; return this; }

    public User Build() => new User { Id = _id, Name = _name };
}

// Use with JSON responses
mock.ForGet().WithPath("/api/user")
    .RespondsWithJsonContent(new UserBuilder().WithId(42).WithName("Alice"));

// Use with OData responses
mock.ForGet().WithPath("/odata/users")
    .RespondsWithODataResult(new[] { new UserBuilder().WithId(1), new UserBuilder().WithId(2) });
```

---

## Request Collection and Capture

### Global Request Log

All requests (expected and unexpected) are captured in `mock.Requests`:

```csharp
// Access all captured requests
RequestCollection allRequests = mock.Requests;

allRequests.IsEmpty.Should().BeFalse();
allRequests.HasUnexpectedRequests.Should().BeFalse();

// Inspect individual requests
CapturedRequest first = allRequests.First();
first.WasExpected.Should().BeTrue();
first.ToString(); // "GET https://localhost/api/users"

// Properties on CapturedRequest
// .Uri, .Method, .Scheme, .Host, .Path, .Query, .Body, .Timestamp, .WasExpected, .Response
```

### Scoped Request Collection

Collect requests for a specific mock in a dedicated `RequestCollection`:

```csharp
var patchRequests = new RequestCollection();

mock.ForPatch()
    .WithPath("/api/items/1")
    .CollectingRequestsIn(patchRequests)
    .RespondsWithStatus(HttpStatusCode.NoContent);

// After making requests:
patchRequests.Count.Should().Be(1);
patchRequests.Should().ContainSingle()
    .Which.ToString().Should().Be("PATCH https://localhost/api/items/1");
```

---

## Invocation Limits

Chain invocation limits on the result of `RespondsWithXxx`:

```csharp
// Only match once — second call throws UnexpectedRequestException
mock.ForGet().WithPath("/api/once").RespondsWithStatus(HttpStatusCode.OK).Once();

// Only match twice
mock.ForGet().WithPath("/api/twice").RespondsWithStatus(HttpStatusCode.OK).Twice();

// Match exactly N times
mock.ForGet().WithPath("/api/data").RespondsWithStatus(HttpStatusCode.OK).Times(3);
```

`AllMocksInvoked` returns `true` when every configured mock has been invoked as many times as required:

```csharp
mock.AllMocksInvoked.Should().BeTrue();

// Get mocks that were not invoked (or not invoked enough times)
IEnumerable<RequestMock> uninvoked = mock.GetUninvokedMocks();
```

---

## Fluent Continuation Building

Each successive `ForXxx()` call inherits the **scheme** and **host** from the previous builder, but
not path, query, custom matchers, or invocation limits. Use `Reset()` to start fresh.

```csharp
// Second call inherits host "api.example.com" and https from the first
mock.ForGet()
    .ForHttps().ForHost("api.example.com")
    .WithPath("/api/v1/users")
    .RespondsWithStatus(HttpStatusCode.OK);

mock.ForPost()                          // inherits ForHttps + ForHost("api.example.com")
    .WithPath("/api/v1/users")
    .RespondsWithStatus(HttpStatusCode.Created);

// Reset prevents inheritance
mock.Reset();
mock.ForGet().WithPath("/api/v2/users").RespondsWithStatus(HttpStatusCode.OK); // uses default localhost/https

// Clear all configured mocks
mock.Clear();
```

---

## Fail-on-Unexpected Behavior

```csharp
// Default: throw UnexpectedRequestException on unmatched requests
var mock = new HttpMock { FailOnUnexpectedCalls = true };

// Disable: return 404 instead of throwing
var mock = new HttpMock { FailOnUnexpectedCalls = false };
```

The exception message lists:
- The unmatched request (method, URL, body size)
- The closest matching mock (if multiple mocks are registered)
- All registered mocks

---

## Disabling Body Prefetch

By default, the request body is buffered before matching. Disable this for streaming scenarios:

```csharp
var mock = new HttpMock { PrefetchBody = false };
// req.Body will be null in custom matchers
```

---

## IHttpClientFactory Support

```csharp
IHttpClientFactory factory = mock.GetClientFactory();
HttpClient client = factory.CreateClient("any-name");

var response = await client.GetAsync("https://localhost/api/data");
```

---

## FluentAssertions.Mockly

Install the `FluentAssertions.Mockly.v8` (or `.v7`) package for expressive assertions.

### Assert All Mocks Were Called

```csharp
mock.Should().HaveAllRequestsCalled();
// Failure message lists which mocks were not invoked:
// "but the following mock was not invoked: POST https://localhost:443/api/orders"
```

### Assert No Unexpected Calls

```csharp
mock.Requests.Should().NotContainUnexpectedCalls();
// Failure: "no unexpected requests should exist, but found 1 unexpected request(s): ..."
```

### Assert Request Collection Is Not Empty

```csharp
mock.Requests.Should().NotBeEmpty();
```

### Assert Captured Request Expectedness

```csharp
CapturedRequest request = mock.Requests.First();

request.Should().BeExpected();    // asserts WasExpected == true
request.Should().BeUnexpected();  // asserts WasExpected == false
```

### HTTP Response Assertions (FluentAssertions.Web)

Mockly's specs package extends `FluentAssertions.Web`:

```csharp
response.Should().Be200Ok();

// Assert JSON body equivalence
await response.Should().BeEquivalentTo(new { Id = 1, Name = "Alice" });
```

---

## Opt-Out

To prevent Mockly from copying the skill file to `.agents/skills/mockly/SKILL.md` when the package
is restored, set the following in your project or `Directory.Build.props`:

```xml
<PropertyGroup>
  <MocklySkill>false</MocklySkill>
</PropertyGroup>
```

---

## Full Example

```csharp
using System.Net;
using System.Net.Http;
using Mockly;

var patchedItems = new RequestCollection();

var mock = new HttpMock
{
    FailOnUnexpectedCalls = true
};

// GET with wildcard path
mock.ForGet()
    .WithPath("/api/items/*")
    .RespondsWithJsonContent(new { Id = 1, Name = "Widget" });

// GET with wildcard query
mock.ForGet()
    .WithPath("/api/items")
    .WithQuery("?$filter=*")
    .RespondsWithJsonContent(new object[] { new { Id = 1 }, new { Id = 2 } });

// POST — expected once
mock.ForPost()
    .WithPath("/api/items")
    .RespondsWithStatus(HttpStatusCode.Created)
    .Once();

// PATCH — collect requests
mock.ForPatch()
    .WithPath("/api/items/1")
    .CollectingRequestsIn(patchedItems)
    .RespondsWithStatus(HttpStatusCode.NoContent);

HttpClient client = mock.GetClient();

await client.GetAsync("https://localhost/api/items/42");
await client.GetAsync("https://localhost/api/items?$filter=active eq true");
await client.PostAsync("https://localhost/api/items", new StringContent("{}"));
await client.SendAsync(new HttpRequestMessage(new HttpMethod("PATCH"), "https://localhost/api/items/1")
{
    Content = new StringContent("{\"Name\":\"Updated\"}")
});

// Verify everything was called
mock.AllMocksInvoked.Should().BeTrue();
mock.Requests.HasUnexpectedRequests.Should().BeFalse();

// Inspect captured PATCH requests
patchedItems.Count.Should().Be(1);
patchedItems.First().WasExpected.Should().BeTrue();
```

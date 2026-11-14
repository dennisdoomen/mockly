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

### Forcing a Body to be Treated as Textual

Body matchers (`WithBody`, `WithBodyMatchingJson`, `WithBodyMatchingRegex`, and form-field matching) only work when the request's Content-Type is recognized as textual (`text/*`, `multipart/*`, `application/json`, `application/xml`, and similar). If your request uses a Content-Type Mockly doesn't recognize as textual — but the body is actually text — opt in with `TreatBodyAsTextual()`:

```csharp
mock.ForPost()
    .WithPath("/api/test")
    .WithBody("*something*")
    .TreatBodyAsTextual()
    .RespondsWithStatus(HttpStatusCode.NoContent);
```

## Request-Driven Responses

`RequestInfo` offers a few accessors that make it easy to build a response directly from the matched
request, without deserializing the body or picking a route segment apart by hand:

```csharp
req.BodyAs<User>();          // deserializes the body, honoring Using(options)
req.PathSegment(2);          // "/api/users/123" -> "123" (zero-based)
req.QueryValue("page");      // value of the "page" query parameter, or null
req.RouteValue("id");        // value captured from a {id} placeholder in WithPath, or null
```

`WithPath` supports named route placeholders (`{name}`) alongside the existing `*` wildcard, and their
captured values are exposed through `RouteValue`:

```csharp
mock.ForGet("/api/users/{id}")
    .RespondsWithJsonContent(req => new { Id = req.RouteValue("id"), Status = "active" });
```

`RespondsWithJsonContent` (and the sequenced `ThenRespondsWithJsonContent`) accept a factory function so
the response can be built from the request, instead of a fixed object:

```csharp
mock.ForPost("/api/users")
    .RespondsWithJsonContent(HttpStatusCode.Created,
        req => new { Id = Guid.NewGuid(), Name = req.BodyAs<User>().Name });
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

## Record and Replay

Record unmatched requests against a real service and replay them later without a network connection. Recording files use
the [HTTP Archive (HAR)](https://w3c.github.io/web-performance/specs/HAR/Overview.html) JSON format.

### Recording Requests

Use `RecordingTo` to send unmatched requests to the real service and write their requests and responses to a file:

```csharp
using var mock = new HttpMock()
    .RecordingTo("Recordings/github.json");

HttpClient client = mock.GetClient();
HttpResponseMessage response = await client.GetAsync(
    "https://api.github.com/repos/dennisdoomen/mockly/issues");
```

`RecordingTo` enables pass-through requests. Configured mocks still take precedence, so Mockly records only unmatched
requests.

Mockly replaces the values of `Authorization`, `Cookie`, `Proxy-Authorization`, and `Set-Cookie` headers with
`[REDACTED]`. Call `KeepSensitiveRecordingValues` before `RecordingTo` only if it is safe to store the values:

```csharp
using var mock = new HttpMock()
    .KeepSensitiveRecordingValues()
    .RecordingTo("Recordings/github.json");
```

### Replaying Requests

Use `LoadRecordings` to serve responses from a recording without contacting the real service:

```csharp
using var mock = new HttpMock()
    .LoadRecordings("Recordings/github.json");

HttpClient client = mock.GetClient();
HttpResponseMessage response = await client.GetAsync(
    "https://api.github.com/repos/dennisdoomen/mockly/issues");
```

Mockly selects a recording by HTTP method and exact URL. If the recording contains a request body, Mockly also requires
the request body to match. If no recording matches, Mockly handles the request as an unexpected request. You can add
`PassThroughUnmatched` to contact the real service for missing recordings.

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
- Invocation limits and sequenced responses are independent. A `Times(2)` mock with three configured responses still stops matching after the second call.
- The verification helpers consider limits:
  - `HttpMock.AllMocksInvoked` returns `true` only when each mock has been called at least once or has reached its configured `Times(..)` limit.
  - `HttpMock.GetUninvokedMocks()` lists mocks that haven't reached their required count (or have 0 calls for unlimited mocks).

## Simulating Response Latency

Use `After(TimeSpan delay)` to delay a response, simulating a slow endpoint. This is useful for exercising timeout, cancellation, and resilience (e.g. Polly) behavior.

```csharp
var mock = new HttpMock();

mock.ForGet()
    .WithPath("/slow")
    .RespondsWithStatus(HttpStatusCode.OK)
    .After(TimeSpan.FromSeconds(2));
```

### Behavior Notes

- The delay is awaited before the response is produced and honors the `CancellationToken` flowing from the HTTP pipeline.
- If `HttpClient.Timeout` is shorter than the delay, the request throws a `TaskCanceledException`, just like a real `HttpClient`.
- If the `CancellationToken` passed to the request is cancelled while the delay is in progress, an `OperationCanceledException` is thrown.

## Simulating Network Failures

Use `ThrowsException` or `TimesOut` to simulate a network-level failure instead of returning a response. This lets
you verify retry, circuit-breaker and other resilience behavior without relying on a real, flaky network.

```csharp
var mock = new HttpMock();

mock.ForGet()
    .WithPath("/flaky")
    .ThrowsException<HttpRequestException>();
```

Throw a specific exception instance instead:

```csharp
mock.ForGet()
    .WithPath("/flaky")
    .ThrowsException(new HttpRequestException("connection reset"));
```

Simulate an `HttpClient` timeout, which throws a `TaskCanceledException`:

```csharp
mock.ForGet()
    .WithPath("/slow")
    .TimesOut();
```

### Behavior Notes

- The exception is propagated to the `HttpClient` caller rather than being converted into a `500 Internal Server Error` response.
- The matching request is still recorded (e.g. in `HttpMock.Requests`) before the exception is thrown.
- `ThrowsException(Exception)` throws the same instance on every matching invocation; `ThrowsException<TException>()` creates a fresh instance each time.
- Combine with invocation limits (`Once()`, `Times(n)`) to fail only the first few calls before succeeding.

## Real Socket Mode

Some code does not let you inject an `HttpClient` or `HttpMessageHandler`. For example, an SDK that only accepts a base URL, or another process. For those cases, install the `Mockly.Server` package (.NET 8.0 or later):

```bash
dotnet add package Mockly.Server
```

`StartServer()` exposes the same mock through a real HTTP listener on the loopback interface:

```csharp
var mock = new HttpMock();
mock.ForGet("/api/users").RespondsWithJsonContent(users);

await using var server = mock.StartServer();
sdk.BaseUrl = server.Url; // e.g. http://127.0.0.1:50123/

// ... exercise the SDK ...

mock.Should().HaveAllRequestsCalled();
```

By default, the server picks a free port. Pass a port to use a specific one:

```csharp
await using var server = mock.StartServer(5080);
```

### Behavior Notes

- The server only listens on `127.0.0.1`, so it is not reachable from other machines.
- Requests are matched as if they were sent to `https://localhost/`, the base address of `GetClient()`. Configure mocks by path, e.g. `ForGet("/api/users")`.
- Request and response headers and bodies pass through unchanged. Captured requests, assertions and invocation limits work the same as with `GetClient()`.
- An unmatched request gets a `404 Not Found` with the reason in the body.
- A failure simulated with `ThrowsException` or `TimesOut` aborts the connection, so the caller sees a network error instead of a response.
- Dispose the server (`await using`) to stop listening and free the port.

## OpenAPI-Driven Mocks

Hand-written mocks encode what you *believe* an API looks like. When the real API changes, those mocks keep passing and the bug only shows up in production. The `Mockly.OpenApi` package uses the OpenAPI (Swagger) document of the API instead:

```bash
dotnet add package Mockly.OpenApi
```

### Generated Responses

`UsingOpenApi` answers every request that maps onto an operation of the document, without any further setup:

```csharp
var mock = new HttpMock().UsingOpenApi("petstore.json");
HttpClient client = mock.GetClient();

// GET /pets/42 returns the success response of the 'getPet' operation
```

The response uses the lowest `2xx` status code of the operation (or its `default` response). The body is the example from the document. If the document has no example, Mockly generates a value from the schema: it uses `example`, `default` and `enum` values where possible, and fills in plausible values that respect the type, format, length and range constraints.

Explicitly configured mocks always take precedence over the generated responses, regardless of the order in which you set them up:

```csharp
mock.UsingOpenApi("petstore.json");
mock.ForGet("/pets/42").RespondsWithJsonContent(new { id = 42, name = "Rex" });
```

### Loading the Document from a URL

To keep the mocks in sync with the API, download the document from a real environment instead of keeping a copy in your repository:

```csharp
var mock = new HttpMock().UsingOpenApi("https://staging.example.com/swagger/v1/swagger.json");
```

The document is downloaded once, when `UsingOpenApi` is called. To add authentication headers or a proxy, pass your own `HttpClient`:

```csharp
var http = new HttpClient();
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

var mock = new HttpMock().UsingOpenApi(new Uri("https://staging.example.com/swagger/v1/swagger.json"), http);
```

If the server returns an error status code, `UsingOpenApi` throws an `InvalidOperationException`.

### Request Contract Validation

`UsingOpenApi` also checks every request your code sends to the API against the document(s). Mockly treats a request that violates the document like any other unexpected call. A request fails when:

- No path of the document matches it, or the path does not support the HTTP method.
- A required path, query, header or cookie parameter is missing, or a parameter value does not match its schema.
- A required request body is missing, the content type is not accepted, or a JSON body does not match its schema (types, required and unknown properties, enumerations, lengths, ranges, patterns, array sizes and `allOf`/`anyOf`/`oneOf`).

The validation also applies to requests handled by explicitly configured mocks. A violating request is captured as unexpected in `HttpMock.Requests`. If `FailOnUnexpectedCalls` is `true` (the default), it throws a `RequestContractViolationException` that lists every violation. Otherwise, it returns `400 Bad Request` with the violations in the body:

```text
Request violates the API contract:
  POST https://api.example.com/v1/pets

Validated against operation 'createPet' (POST /pets):
    ✗ $ is missing required property 'name'
    ✗ $.status must be a string, but found an integer 3
```

### Behavior Notes

- JSON documents in OpenAPI 3.0, OpenAPI 3.1 and Swagger 2.0 format are supported. YAML documents are not.
- Request paths are matched both as-is and relative to the path of each server URL (or the Swagger 2.0 `basePath`) of the document. The host is ignored, so the base address of `GetClient()` works too.
- Concrete paths such as `/pets/mine` take precedence over templated ones such as `/pets/{petId}`.
- Call `UsingOpenApi` multiple times to combine several documents. A request is valid if it complies with any of them.
- Only requests for the API are validated: requests that match a path of the document, or that are under the path of one of its server URLs. Other requests, such as calls to an identity provider, are handled as usual: explicit mocks, recordings, pass-through, or an `UnexpectedRequestException`.

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

### Inspecting the Captured Body

Each `CapturedRequest` exposes the request body both as text (`Body`) and as raw bytes (`RawBody`):

```csharp
var capturedRequest = capturedRequests.First();

// The body decoded as text, or null if it's not textual (see IsBodyLikelyTextual)
capturedRequest.Body.Should().Be("hello");

// The raw, undecoded bytes of the body
capturedRequest.RawBody.Should().Equal(Encoding.UTF8.GetBytes("hello"));
```

:::info
`Body` and `RawBody` are only populated when `HttpMock.PrefetchBody` is `true` (the default). See [Request Body Prefetching](#request-body-prefetching) above.
:::

## Traffic Report

When a test fails for a reason that isn't captured by a single unexpected request — for example, a mock responded but with the wrong content, or the order of several requests matters — dump the whole conversation `HttpMock` has handled so far:

```csharp
output.WriteLine(mock.GetTrafficReport());
```

```
Traffic report (3 request(s)):
  #1 GET https://api.example.com/api/users -> 200 OK
  #2 POST https://api.example.com/api/users -> 404 NotFound (unexpected)
      body: {"role":"Admin"}
  #3 GET https://api.example.com/api/users/123 -> 200 OK
```

## Assertions

Mockly provides extensive support for test assertions through **FluentAssertions**. For a full guide on available assertions for mocks, collections, and requests, see the [Assertions](./assertions.md) page.

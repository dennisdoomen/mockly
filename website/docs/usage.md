---
sidebar_position: 3
---

# Usage

Learn about the core features and how to use Mockly effectively in your tests.

## Basic Mocking

Create an `HttpMock` instance and configure it using the fluent API:

```csharp
var mock = new HttpMock();

// Mock a GET request
mock.ForGet()
    .WithPath("/api/products")
    .RespondsWithJsonContent(new[]
    {
        new { Id = 1, Name = "Product 1" },
        new { Id = 2, Name = "Product 2" }
    });

// Get the HttpClient
HttpClient client = mock.GetClient(); // BaseAddress defaults to https://localhost/
```

## Getting an HttpClient, IHttpClientFactory or HttpMessageHandler

Mockly provides three ways to wire the mock into your code under test:

- **`GetClient()`** — returns a new `HttpClient` with `BaseAddress` set to `https://localhost/`, ready to use directly in tests.
- **`GetClientFactory()`** — returns an `IHttpClientFactory` whose `CreateClient()` method produces `HttpClient` instances backed by the mock. Use this when your code depends on `IHttpClientFactory`.
- **`GetMessageHandler()`** — returns the underlying `HttpMessageHandler`. Use this when you need to build a custom `HttpClient` or pass the handler to other infrastructure.

```csharp
// Option 1: HttpClient (BaseAddress defaults to https://localhost/)
HttpClient client = mock.GetClient();

// Option 2: IHttpClientFactory
IHttpClientFactory factory = mock.GetClientFactory();
HttpClient clientFromFactory = factory.CreateClient("myClient");

// Option 3: HttpMessageHandler
HttpMessageHandler handler = mock.GetMessageHandler();
var customClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
```

## HTTP Method Support

Mockly supports all common HTTP methods:

```csharp
mock.ForGet()     // GET requests
mock.ForPost()    // POST requests
mock.ForPut()     // PUT requests
mock.ForPatch()   // PATCH requests
mock.ForDelete()  // DELETE requests
mock.ForHead()    // HEAD requests
mock.ForOptions() // OPTIONS requests

// Generic method support for any verb
mock.For(HttpMethod.Get)
mock.For(new HttpMethod("PROPFIND"))
```

### Full-URL Shortcuts

Instead of configuring scheme/host/path/query separately, you can provide a single full URL pattern to each `ForXxx` method. Wildcards (`*`) are supported in the host, path, and query parts.

```csharp
// GET: full https URL, wildcard path and query
mock.ForGet("https://api.example.com/users/*?q=*")
    .RespondsWithStatus(HttpStatusCode.OK);

// POST: wildcard host and path
mock.ForPost("http://*.example.com/*")
    .RespondsWithStatus(HttpStatusCode.Created);

// PUT: full https URL with wildcard path and query
mock.ForPut("https://api.example.com/items/*?filter=*")
    .RespondsWithStatus(HttpStatusCode.OK);

// PATCH: wildcard host and path
mock.ForPatch("http://*.contoso.local/*")
    .RespondsWithStatus(HttpStatusCode.OK);

// DELETE: specific host and path
mock.ForDelete("http://localhost/api/items/*")
    .RespondsWithEmptyContent(HttpStatusCode.NoContent);
```

## Path and Query Matching

### Exact Matching

```csharp
mock.ForGet().WithPath("/api/users/123");
```

### Wildcard Matching

```csharp
// Match any user ID
mock.ForGet().WithPath("/api/users/*");

// Match query parameters with wildcards
mock.ForGet()
    .WithPath("/api/search")
    .WithQuery("?q=*&limit=10");

// Match any query string
mock.ForGet().WithPath("/api/data").WithAnyQuery();

// Match requests without any query string
mock.ForGet().WithPath("/api/data").WithoutQuery();
```

### Query Parameter Matching

Match individual query parameters regardless of their order in the URI:

```csharp
mock.ForGet()
    .WithPath("/api/search")
    .WithQueryParam("q", "mockly*")
    .WithQueryParam("page", "1")
    .RespondsWithStatus(HttpStatusCode.OK);
```

You can also match by presence only:

```csharp
mock.ForGet()
    .WithPath("/api/search")
    .WithQueryParam("api-key")
    .RespondsWithStatus(HttpStatusCode.OK);
```

## Form Field Matching

Match `application/x-www-form-urlencoded` request bodies by field name and value:

```csharp
mock.ForPost()
    .WithPath("/oauth/token")
    .WithFormField("grant_type", "client_credentials")
    .WithFormField("scope", "read*")
    .RespondsWithStatus(HttpStatusCode.OK);
```

## Header Matching

Use the first-class header matchers when you need to assert on common request metadata:

```csharp
mock.ForGet()
    .WithPath("/api/secure")
    .WithHeader("X-Api-Key")
    .RespondsWithStatus(HttpStatusCode.OK);

mock.ForGet()
    .WithPath("/api/secure")
    .WithHeader("X-Trace-Id", "abc-*")
    .RespondsWithStatus(HttpStatusCode.OK);

mock.ForGet()
    .WithPath("/api/auth")
    .WithBearerToken("eyJ*")
    .RespondsWithStatus(HttpStatusCode.OK);

mock.ForPost()
    .WithPath("/api/json")
    .WithContentType("application/json")
    .RespondsWithStatus(HttpStatusCode.OK);
```

`WithHeader(name, valuePattern)` matches when any value of a multi-valued header satisfies the wildcard pattern. `WithContentType` matches the media type and ignores parameters such as `charset`.

## Response Configuration

### JSON Responses

```csharp
mock.ForGet()
    .WithPath("/api/user")
    .RespondsWithJsonContent(new { Id = 1, Name = "John" });
```

#### Custom JSON Options

You can supply custom `JsonSerializerOptions` for serialization:

```csharp
var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

mock.ForGet()
    .WithPath("/api/user")
    .Using(options)
    .RespondsWithJsonContent(new { UserId = 1, UserName = "John" });
```

### Problem Details Responses

Mockly has built-in support for [RFC 7807 Problem Details](https://datatracker.ietf.org/doc/html/rfc7807):

```csharp
mock.ForGet()
    .WithPath("/api/users/999")
    .RespondsWithProblemDetails(
        HttpStatusCode.NotFound,
        title: "User not found",
        detail: "No user exists with id 999",
        type: "https://example.com/problems/not-found",
        instance: "/api/users/999",
        extensions: new Dictionary<string, object?>
        {
            ["traceId"] = "00-abc-def-01"
        });
```

## Assertions

Mockly integrates with **FluentAssertions** for expressive and intention-revealing test assertions. See the [Assertions](./assertions.md) page for details on how to verify your HTTP interactions.

### String Content

```csharp
mock.ForGet()
    .WithPath("/api/text")
    .RespondsWithContent("Hello, World!", "text/plain");
```

### Status Codes

```csharp
mock.ForPost()
    .WithPath("/api/create")
    .RespondsWithStatus(HttpStatusCode.Created);
```

### Response Headers

Use `WithHeader` to add response headers such as `Location`, `ETag` or `Cache-Control`. Content headers
(`Content-Type`, `Content-Length`, etc.) are routed to the response content automatically; all other headers are
added to the response headers. Pass multiple values to add a multi-valued header.

```csharp
mock.ForPost()
    .WithPath("/api/users")
    .RespondsWithStatus(HttpStatusCode.Created)
    .WithHeader("Location", "/api/users/123")
    .WithHeader("ETag", "\"v1\"");

// Multi-valued header
mock.ForGet()
    .WithPath("/api/data")
    .RespondsWithStatus(HttpStatusCode.OK)
    .WithHeader("X-Custom", "first", "second");
```

### Empty Responses

```csharp
mock.ForDelete()
    .WithPath("/api/resource/123")
    .RespondsWithEmptyContent(HttpStatusCode.NoContent);
```

### HTTP Content Responses

```csharp
// Simple content (defaults to 200 OK)
var content = new ByteArrayContent(imageBytes);
mock.ForGet()
    .WithPath("/api/image")
    .RespondsWith(content);

// Complex content with status code
var inner = new HttpResponseMessage(HttpStatusCode.OK)
{
    Content = new StringContent(json, Encoding.UTF8, "application/json")
};

var multipart = new MultipartContent("mixed", $"batch_{Guid.NewGuid()}");
multipart.Add(new HttpMessageContent(inner));

mock.ForPost()
    .WithPath("/api/batch")
    .RespondsWith(HttpStatusCode.OK, multipart);
```

### File, Stream and Byte Responses

For large or binary payloads (file downloads, images, PDFs) you can stream a file, a raw byte array, or an arbitrary `Stream` directly:

```csharp
// Stream a file; the file is opened freshly per request so the mock can be called multiple times.
// The content type is inferred from the extension (defaults to application/octet-stream) unless supplied.
mock.ForGet()
    .WithPath("/api/report")
    .RespondsWithFile("report.pdf");

mock.ForGet()
    .WithPath("/api/logo")
    .RespondsWithFile("logo.dat", "image/png");

// Raw bytes are buffered, so the mock can safely be invoked multiple times.
mock.ForGet()
    .WithPath("/api/bytes")
    .RespondsWithBytes(imageBytes, "image/png");

// A Stream can only be consumed once. Prefer RespondsWithBytes or RespondsWithFile when the
// mock may be called more than once.
mock.ForGet()
    .WithPath("/api/stream")
    .RespondsWithStream(stream, "application/octet-stream");
```

### Custom Responses

```csharp
mock.ForGet()
    .WithPath("/api/custom")
    .RespondsWith(request =>
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("X-Custom-Header", "value");
        response.Content = new StringContent("Custom content");
        return response;
    });
```

### Sequenced Responses

Configure a sequence of responses for the same matched request so consecutive calls get different results:

```csharp
mock.ForGet()
    .WithPath("/api/resource")
    .RespondsWithStatus(HttpStatusCode.ServiceUnavailable)
    .Then(HttpStatusCode.ServiceUnavailable)
    .Then(HttpStatusCode.OK);
```

`Then*` mirrors the `RespondsWith*` response methods, including JSON/content/OData variants and custom delegates (`Then(Func<RequestInfo, HttpResponseMessage>)`).

Behavior notes:

- After the configured sequence is exhausted, the last response is reused.
- Sequence length is independent from invocation limits (`Once()`, `Twice()`, `Times(n)`).

## Using Test Data Builders

Mockly supports the `IResponseBuilder<T>` interface, allowing you to integrate test data builders seamlessly:

### Implementing a Test Data Builder

```csharp
public class UserBuilder : IResponseBuilder<User>
{
    private int id = 1;
    private string name = "Default User";
    private string email = "user@example.com";

    public UserBuilder WithId(int id)
    {
        this.id = id;
        return this;
    }

    public UserBuilder WithName(string name)
    {
        this.name = name;
        return this;
    }

    public UserBuilder WithEmail(string email)
    {
        this.email = email;
        return this;
    }

    public User Build()
    {
        return new User { Id = id, Name = name, Email = email };
    }
}
```

### Using Builders with JSON Responses

```csharp
var userBuilder = new UserBuilder()
    .WithId(123)
    .WithName("John Doe")
    .WithEmail("john@example.com");

mock.ForGet()
    .WithPath("/api/user")
    .RespondsWithJsonContent(userBuilder);

// With custom status code
mock.ForPost()
    .WithPath("/api/user")
    .RespondsWithJsonContent(HttpStatusCode.Created, userBuilder);
```

### Using Builders with OData Responses

```csharp
// Single item
var userBuilder = new UserBuilder().WithId(1).WithName("Alice");

mock.ForGet()
    .WithPath("/odata/user")
    .RespondsWithODataResult(userBuilder);

// Collection of items
var builders = new[]
{
    new UserBuilder().WithId(1).WithName("Alice"),
    new UserBuilder().WithId(2).WithName("Bob"),
    new UserBuilder().WithId(3).WithName("Charlie")
};

mock.ForGet()
    .WithPath("/odata/users")
    .RespondsWithODataResult(builders);

// With custom status code and OData context
mock.ForGet()
    .WithPath("/odata/users")
    .RespondsWithODataResult(
        HttpStatusCode.OK, 
        builders, 
        "https://localhost/$metadata#Users");
```

## Request Inspection

Access all captured requests globally:

```csharp
var allRequests = mock.Requests;

allRequests.Count.Should().Be(5);
allRequests.Should().NotContainUnexpectedCalls();

var firstRequest = allRequests.First();
firstRequest.Method.Should().Be(HttpMethod.Get);
firstRequest.Path.Should().StartWith("/api/");
firstRequest.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
```

## Handling Unexpected Requests

By default, unexpected requests throw an exception:

```csharp
var mock = new HttpMock();
mock.FailOnUnexpectedCalls = true; // Default

// This will throw UnexpectedRequestException
await client.GetAsync("http://localhost/unmocked-path");
```

To allow unexpected requests:

```csharp
mock.FailOnUnexpectedCalls = false;

// Returns 404 NotFound instead of throwing
var response = await client.GetAsync("http://localhost/unmocked-path");
response.StatusCode.Should().Be(HttpStatusCode.NotFound);
```

## Continuing Configuration

You can continue adding mocks to an existing `HttpMock`:

```csharp
var mock = new HttpMock();

// Initial configuration
mock.ForGet().WithPath("/api/users").RespondsWithStatus(HttpStatusCode.OK);

var client = mock.GetClient();
// ... make some requests ...

// Add more mocks later
mock.ForPost().WithPath("/api/users").RespondsWithStatus(HttpStatusCode.Created);

// Same client works with new mocks
await client.PostAsync("http://localhost/api/users", content);
```

## Builder Lifecycle

Continue building can reuse parts of the previous builder for convenience. You can opt out with `Reset()`:

```csharp
mock.ForGet()
    .ForHttps().ForHost("somehost")
    .WithPath("/api/test")
    .WithQuery("?q=test")
    .RespondsWithStatus(HttpStatusCode.OK);

// Reset prevents reusing the previous builder's scheme/host
mock.Reset();

mock.ForGet()
    .WithPath("/api/test")
    .WithQuery("?q=test")
    .RespondsWithStatus(HttpStatusCode.NotModified);
```

## Clearing Mocks

Reset all configured mocks:

```csharp
mock.Clear();
```

## OData Result Helpers

Produce OData-style envelopes directly from the builder:

```csharp
var items = new[] { new { Id = 1, Name = "A" }, new { Id = 2, Name = "B" } };

mock.ForGet()
    .WithPath("/odata/items")
    .RespondsWithODataResult(items);

// Empty result
mock.ForGet()
    .WithPath("/odata/empty")
    .RespondsWithODataResult(Array.Empty<object>());

// Include @odata.context and custom status code
mock.ForGet()
    .WithPath("/odata/ctx")
    .RespondsWithODataResult(items, context: "http://localhost/$metadata#items", statusCode: HttpStatusCode.OK);
```

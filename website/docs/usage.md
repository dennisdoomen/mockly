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

## HTTP Method Support

Mockly supports all common HTTP methods:

```csharp
mock.ForGet()     // GET requests
mock.ForPost()    // POST requests
mock.ForPut()     // PUT requests
mock.ForPatch()   // PATCH requests
mock.ForDelete()  // DELETE requests
```

## Full-URL Shortcuts

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
```

## Response Configuration

### JSON Responses

```csharp
mock.ForGet()
    .WithPath("/api/user")
    .RespondsWithJsonContent(new { Id = 1, Name = "John" });
```

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

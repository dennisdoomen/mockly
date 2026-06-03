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

## HttpMock Assertions

You can perform high-level assertions on the `HttpMock` instance itself to ensure all configured mocks were utilized.

```csharp
using var mock = new HttpMock();
mock.ForGet("/api/users").RespondsWithStatus(HttpStatusCode.OK);

// ... perform actions ...

// Verify that all configured mocks were called at least once
mock.Should().HaveAllRequestsCalled();

// Verify that mocks were called in a specific order
var mock1 = mock.ForGet("/api/first").RespondsWithStatus(HttpStatusCode.OK);
var mock2 = mock.ForGet("/api/second").RespondsWithStatus(HttpStatusCode.OK);

mock.Should().HaveCalledInOrder(mock1, mock2);
```

### Unexpected Requests

You can verify that no unexpected calls were made to the mock:

```csharp
mock.Requests.Should().NotContainUnexpectedCalls();
```

## Setup Assertions

If you keep a reference to a mock setup, you can assert on it directly.

```csharp
var userMock = mock.ForGet("/api/users/*").RespondsWithStatus(HttpStatusCode.OK);

// ... perform actions ...

userMock.Should().HaveBeenCalled();
userMock.Should().HaveBeenCalled(Because.Of("the user list should be refreshed"));
```

## Request Collection Assertions

When using a `RequestCollection` to capture requests, you can use specialized assertions to inspect the captured data.

```csharp
var captured = new RequestCollection();
mock.ForPost("/api/data").CollectingRequestsIn(captured).RespondsWithStatus(HttpStatusCode.OK);

// ... perform actions ...

// Check if any requests were captured at all
captured.Should().NotBeEmpty();
captured.Should().HaveCount(2);

// Assert on the presence of specific requests using URL patterns
captured.Should().ContainRequestFor("/api/data");
captured.Should().NotContainRequestFor("/api/other");
```

## Chaining Assertions

The `ContainRequestFor` assertion allows you to chain further checks on the matched request using the `Which` property.

```csharp
captured.Should().ContainRequestFor("/api/data")
    .Which.HasHeader("X-Custom-Header", "ExpectedValue")
    .And.WithBody("*part-of-body*")
    .And.WithBearerToken();
```

### Available Chained Assertions

On the result of `ContainRequestFor(...).Which`, you can use:

- `HasHeader(name, [value])`: Verifies the presence and optionally the value of a header.
- `WithBody(pattern)`: Verifies the request body matches a string or wildcard pattern.
- `WithBearerToken()`: Verifies that a Bearer token is present in the `Authorization` header.
- `WithQuery(query)`: Verifies the request query string.

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

// Assert the body has specific properties (deserialized as a dictionary)
var expectedProps = new Dictionary<string, string>
{
    ["id"] = "1",
    ["name"] = "John"
};
mock.Requests.Should().ContainRequest()
    .WithBodyHavingPropertiesOf(expectedProps);
```

:::info
These assertions require the request body to be available in memory. If you disabled `HttpMock.PrefetchBody`, these assertions will fail as `RequestInfo.Body` will be `null`.
:::

## Individual Request Assertions

You can also assert on individual `CapturedRequest` objects.

```csharp
var request = captured.First();

request.Should().BeExpected();
request.Should().NotBeUnexpected();
```

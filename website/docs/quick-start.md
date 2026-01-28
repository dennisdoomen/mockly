---
sidebar_position: 2
---

# Quick Start

Get up and running with Mockly in minutes.

## Installation

Install the Mockly package:

```bash
dotnet add package mockly
```

To get the assertions, also install one of the two assertion packages, depending on which version of FluentAssertions you're using:

```bash
dotnet add package FluentAssertions.Mockly.v7
# or
dotnet add package FluentAssertions.Mockly.v8
```

## Basic Usage

Here's a simple example to get you started:

```csharp
using Mockly;
using FluentAssertions;

// Arrange
var mock = new HttpMock();
mock.ForGet()
    .WithPath("/api/users/123")
    .RespondsWithJsonContent(new { Id = 123, Name = "John Doe" });

HttpClient client = mock.GetClient();

// Act
// Note: BaseAddress defaults to https://localhost/
var response = await client.GetAsync("/api/users/123");
var content = await response.Content.ReadAsStringAsync();

// Assert
response.StatusCode.Should().Be(HttpStatusCode.OK);
content.Should().Contain("John Doe");
mock.Should().HaveAllRequestsCalled();
```

## Using IHttpClientFactory

If your code expects an `IHttpClientFactory`, Mockly can provide one that produces clients wired to the mock:

```csharp
var mock = new HttpMock();
mock.ForGet().WithPath("/ping").RespondsWithStatus(HttpStatusCode.OK);

var factory = mock.GetClientFactory();
HttpClient client = factory.CreateClient("any");

// Note: BaseAddress defaults to https://localhost/
var response = await client.GetAsync("/ping");
response.StatusCode.Should().Be(HttpStatusCode.OK);
```

## Complete Example

Here's a more comprehensive example showing multiple features:

```csharp
using Mockly;
using FluentAssertions;

public class UserServiceTests
{
    [Fact]
    public async Task Should_Handle_User_Operations()
    {
        // Arrange
        var mock = new HttpMock();
        var capturedPatches = new RequestCollection();

        // Configure mocks
        mock.ForGet()
            .WithPath("/api/users/*")
            .RespondsWithJsonContent(new { Id = 123, Name = "John Doe" });

        mock.ForPost()
            .WithPath("/api/users")
            .RespondsWithStatus(HttpStatusCode.Created);

        mock.ForPatch()
            .WithPath("/api/users/*")
            .CollectingRequestIn(capturedPatches)
            .RespondsWithStatus(HttpStatusCode.NoContent);

        HttpClient client = mock.GetClient();
        var service = new UserService(client);

        // Act
        var user = await service.GetUserAsync(123);
        await service.CreateUserAsync(new { Name = "Jane" });
        await service.UpdateUserAsync(123, new { Name = "John Updated" });

        // Assert
        user.Name.Should().Be("John Doe");
        mock.Should().HaveAllRequestsCalled();
        mock.Requests.Should().HaveCount(3);
        mock.Requests.Should().NotContainUnexpectedCalls();

        capturedPatches.Count.Should().Be(1);
        capturedPatches.First().Should().BeExpected();
    }
}
```

## Next Steps

Now that you have Mockly set up, explore the following topics:

- [Usage](usage.md) - Learn about basic mocking patterns
- [Advanced Features](advanced.md) - Dive into custom matchers, request capture, and more
- [Building](building.md) - Information about building the project from source

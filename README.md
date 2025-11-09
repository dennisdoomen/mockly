
<h1 align="center">
  <br>
  Mockly.Http
  <br>
</h1>


<h4 align="center">A fluent HTTP mocking library for .NET with powerful request matching and assertions</h4>


<div align="center">

[![](https://img.shields.io/github/actions/workflow/status/dennisdoomen/mockly.http/build.yml?branch=main)](https://github.com/dennisdoomen/mockly.http/actions?query=branch%3amain)
[![Coveralls branch](https://img.shields.io/coverallsCoverage/github/dennisdoomen/mockly.http?branch=main)](https://coveralls.io/github/dennisdoomen/mockly.http?branch=main)
[![](https://img.shields.io/github/release/dennisdoomen/mockly.http.svg?label=latest%20release&color=007edf)](https://github.com/dennisdoomen/mockly.http/releases/latest)
[![](https://img.shields.io/nuget/dt/mockly.http.svg?label=downloads&color=007edf&logo=nuget)](https://www.nuget.org/packages/mockly.http)
[![](https://img.shields.io/librariesio/dependents/nuget/mockly.http.svg?label=dependent%20libraries)](https://libraries.io/nuget/mockly.http)
![GitHub Repo stars](https://img.shields.io/github/stars/dennisdoomen/mockly.http?style=flat)
[![GitHub contributors](https://img.shields.io/github/contributors/dennisdoomen/mockly.http)](https://github.com/dennisdoomen/mockly.http/graphs/contributors)
[![GitHub last commit](https://img.shields.io/github/last-commit/dennisdoomen/mockly.http)](https://github.com/dennisdoomen/mockly.http)
[![GitHub commit activity](https://img.shields.io/github/commit-activity/m/dennisdoomen/mockly.http)](https://github.com/dennisdoomen/mockly.http/graphs/commit-activity)
[![open issues](https://img.shields.io/github/issues/dennisdoomen/mockly.http)](https://github.com/dennisdoomen/mockly.http/issues)
![Static Badge](https://img.shields.io/badge/4.7%2C_8.0%2C_netstandard2.0%2C_netstandard2.1-dummy?label=dotnet&color=%235027d5)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg?style=flat-square)](https://makeapullrequest.com)
![](https://img.shields.io/badge/release%20strategy-githubflow-orange.svg)

<a href="#about">About</a> •
<a href="#key-features">Key Features</a> •
<a href="#quick-start">Quick Start</a> •
<a href="#usage">Usage</a> •
<a href="#advanced-features">Advanced Features</a> •
<a href="#download">Download</a> •
<a href="#building">Building</a> •
<a href="#contributing">Contributing</a> •
<a href="#license">License</a>

</div>

## About

### What's this?

**Mockly.Http** is a powerful and flexible HTTP mocking library for .NET that makes it easy to test code that depends on `HttpClient`. It provides a fluent API for configuring HTTP request mocks, capturing request details, and asserting on HTTP interactions in your tests.

The library supports:
* **.NET Framework 4.7** and higher
* **.NET Standard 2.0 and 2.1** (compatible with .NET Core 2.0+, .NET 5+)
* **.NET 8.0** and higher
* **FluentAssertions 8.0** integration for expressive test assertions

### What's so special about that?

Unlike other HTTP mocking libraries, Mockly.Http offers:

* **Fluent, intuitive API** - Chain method calls to build complex mocking scenarios with ease
* **Wildcard pattern matching** - Match URLs using wildcards (`*`) in paths and query strings
* **Custom matchers** - Use predicates for advanced request matching logic
* **Request capture & inspection** - Automatically capture all requests with full metadata (headers, body, timestamp)
* **Powerful assertions** - Built-in FluentAssertions extensions for verifying HTTP behavior
* **Diagnostic support** - Detailed error messages when unexpected requests occur
* **Extensibility** - Design allows for custom response generators and matchers
* **Zero configuration** - Works out of the box with sensible defaults
* **Performance optimized** - Regex patterns are cached for efficient matching

### Who created this?

Mockly.Http is created and maintained by [Dennis Doomen](https://github.com/dennisdoomen), also the creator of [FluentAssertions](https://fluentassertions.com/). It's designed to work seamlessly with modern .NET testing practices and integrates naturally with FluentAssertions for expressive test assertions.

## Key Features

### 🎯 Fluent Request Matching

```csharp
mock.ForGet().ForPath("/api/users/*").RespondsWithJsonContent(user);
mock.ForPost().ForPath("/api/data").ForQuery("?filter=*").RespondsWithStatus(HttpStatusCode.Created);
```

### 🔍 Request Capture & Inspection

```csharp
var patches = new RequestCollection();
mock.ForPatch().ForPath("/api/update").CollectingRequestIn(patches);

// After test execution
patches.Count.Should().Be(3);
patches.First().Path.Should().Contain("/api/update");
```

### ✅ Powerful Assertions

```csharp
mock.Should().HaveAllRequestsCalled();
mock.Requests.Should().NotBeEmpty();
mock.Requests.Should().NotContainUnexpectedCalls();
```

### 🎨 Multiple Response Types

* JSON content with automatic serialization
* Raw string content
* Empty responses
* Custom HTTP status codes
* Custom response generators

### 🛡️ Fail-Fast Testing

```csharp
mock.FailOnUnexpectedCalls = true; // Default behavior
// Throws UnexpectedRequestException if an unmocked request is made
```

## Quick Start

Install the package:

```bash
dotnet add package mockly.http
```

Basic usage:

```csharp
using Mockly.Http;
using FluentAssertions;

// Arrange
var mock = new HttpMock();
mock.ForGet()
    .ForPath("/api/users/123")
    .RespondsWithJsonContent(new { Id = 123, Name = "John Doe" });

HttpClient client = mock.GetClient();

// Act
var response = await client.GetAsync("http://localhost/api/users/123");
var content = await response.Content.ReadAsStringAsync();

// Assert
response.StatusCode.Should().Be(HttpStatusCode.OK);
content.Should().Contain("John Doe");
mock.Should().HaveAllRequestsCalled();
```

## Usage

### Basic Mocking

Create an `HttpMock` instance and configure it using the fluent API:

```csharp
var mock = new HttpMock();

// Mock a GET request
mock.ForGet()
    .ForPath("/api/products")
    .RespondsWithJsonContent(new[] 
    { 
        new { Id = 1, Name = "Product 1" },
        new { Id = 2, Name = "Product 2" }
    });

// Get the HttpClient
HttpClient client = mock.GetClient();
```

### HTTP Method Support

```csharp
mock.ForGet()     // GET requests
mock.ForPost()    // POST requests
mock.ForPut()     // PUT requests
mock.ForPatch()   // PATCH requests
mock.ForDelete()  // DELETE requests
```

### Path and Query Matching

**Exact matching:**
```csharp
mock.ForGet().ForPath("/api/users/123");
```

**Wildcard matching:**
```csharp
// Match any user ID
mock.ForGet().ForPath("/api/users/*");

// Match query parameters with wildcards
mock.ForGet()
    .ForPath("/api/search")
    .ForQuery("?q=*&limit=10");
```

### Response Configuration

**JSON responses:**
```csharp
mock.ForGet()
    .ForPath("/api/user")
    .RespondsWithJsonContent(new { Id = 1, Name = "John" });
```

**String content:**
```csharp
mock.ForGet()
    .ForPath("/api/text")
    .RespondsWithContent("Hello, World!", "text/plain");
```

**Status codes:**
```csharp
mock.ForPost()
    .ForPath("/api/create")
    .RespondsWithStatus(HttpStatusCode.Created);
```

**Empty responses:**
```csharp
mock.ForDelete()
    .ForPath("/api/resource/123")
    .RespondsWithEmptyContent(HttpStatusCode.NoContent);
```

**Custom responses:**
```csharp
mock.ForGet()
    .ForPath("/api/custom")
    .RespondsWith(request => 
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("X-Custom-Header", "value");
        response.Content = new StringContent("Custom content");
        return response;
    });
```

## Advanced Features

### Custom Matchers

Use predicates for advanced matching logic:

```csharp
mock.ForGet()
    .ForPath("/api/data")
    .For(request => request.Headers.Contains("X-API-Key"))
    .RespondsWithStatus(HttpStatusCode.OK);
```

### Request Collection

Capture requests for specific mocks:

```csharp
var capturedRequests = new RequestCollection();

mock.ForPatch()
    .ForPath("/api/update")
    .CollectingRequestIn(capturedRequests)
    .RespondsWithStatus(HttpStatusCode.NoContent);

// After making requests
capturedRequests.Count.Should().Be(2);
capturedRequests.First().WasExpected.Should().BeTrue();
```

### Request Inspection

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

### Assertions

**Verify all mocks were called:**
```csharp
mock.Should().HaveAllRequestsCalled();
```

**Verify no unexpected requests:**
```csharp
mock.Requests.Should().NotContainUnexpectedCalls();
```

**Verify request expectations:**
```csharp
var request = mock.Requests.First();
request.Should().BeExpected();
request.WasExpected.Should().BeTrue();
```

**Collection assertions:**
```csharp
mock.Requests.Should().NotBeEmpty();
mock.Requests.Should().HaveCount(3);
capturedRequests.Should().BeEmpty();
```

### Handling Unexpected Requests

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

### Continuing Configuration

You can continue adding mocks to an existing `HttpMock`:

```csharp
var mock = new HttpMock();

// Initial configuration
mock.ForGet().ForPath("/api/users").RespondsWithStatus(HttpStatusCode.OK);

var client = mock.GetClient();
// ... make some requests ...

// Add more mocks later
mock.ForPost().ForPath("/api/users").RespondsWithStatus(HttpStatusCode.Created);

// Same client works with new mocks
await client.PostAsync("http://localhost/api/users", content);
```

### Clearing Mocks

Reset all configured mocks:

```csharp
mock.Clear();
```

## Complete Example

Here's a comprehensive example showing multiple features:

```csharp
using Mockly.Http;
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
            .ForPath("/api/users/*")
            .RespondsWithJsonContent(new { Id = 123, Name = "John Doe" });
        
        mock.ForPost()
            .ForPath("/api/users")
            .RespondsWithStatus(HttpStatusCode.Created);
        
        mock.ForPatch()
            .ForPath("/api/users/*")
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

## Download

This library is available as [a NuGet package](https://www.nuget.org/packages/mockly.http) on https://nuget.org. To install it, use the following command-line:

```bash
dotnet add package mockly.http
```

Or via the Package Manager Console in Visual Studio:

```powershell
Install-Package mockly.http
```


## Building

To build this repository locally, you need the following:
* The [.NET SDKs](https://dotnet.microsoft.com/en-us/download/visual-studio-sdks) for .NET 4.7 and 8.0.
* Visual Studio, JetBrains Rider or Visual Studio Code with the C# DevKit

You can also build, run the unit tests and package the code using the following command-line:

```bash
build.ps1
```

Or, if you have the [Nuke tool installed](https://nuke.build/docs/getting-started/installation/):

```bash
nuke
```

Also try using `--help` to see all the available options or `--plan` to see what the scripts does.

## Contributing

Your contributions are always welcome! Please have a look at the [contribution guidelines](CONTRIBUTING.md) first.

Previous contributors include:

<a href="https://github.com/dennisdoomen/mockly.http/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=dennisdoomen/mockly.http" alt="contrib.rocks image" />
</a>

(Made with [contrib.rocks](https://contrib.rocks))

## Versioning
This library uses [Semantic Versioning](https://semver.org/) to give meaning to the version numbers. For the versions available, see the [releases](https://github.com/dennisdoomen/mockly.http/releases) on this repository.

## Credits
This library wouldn't have been possible without the following tools, packages and companies:

* [FluentAssertions](https://fluentassertions.com/) - Fluent API for asserting the results of unit tests by [Dennis Doomen](https://github.com/dennisdoomen)
* [Nuke](https://nuke.build/) - Smart automation for DevOps teams and CI/CD pipelines by [Matthias Koch](https://github.com/matkoch)
* [xUnit](https://xunit.net/) - Community-focused unit testing tool for .NET by [Brad Wilson](https://github.com/bradwilson)
* [Coverlet](https://github.com/coverlet-coverage/coverlet) - Cross platform code coverage for .NET by [Toni Solarin-Sodara](https://github.com/tonerdo)
* [GitVersion](https://gitversion.net/) - From git log to SemVer in no time
* [ReportGenerator](https://reportgenerator.io/) - Converts coverage reports by [Daniel Palme](https://github.com/danielpalme)
* [StyleCopyAnalyzer](https://github.com/DotNetAnalyzers/StyleCopAnalyzers) - StyleCop rules for .NET
* [Roslynator](https://github.com/dotnet/roslynator) - A set of code analysis tools for C# by [Josef Pihrt](https://github.com/josefpihrt)
* [CSharpCodingGuidelines](https://github.com/bkoelman/CSharpGuidelinesAnalyzer) - Roslyn analyzers by [Bart Koelman](https://github.com/bkoelman) to go with the [C# Coding Guidelines](https://csharpcodingguidelines.com/)
* [Meziantou](https://github.com/meziantou/Meziantou.Framework) - Another set of awesome Roslyn analyzers by [Gérald Barré](https://github.com/meziantou)

## Related Projects

You may also be interested in:

* [FluentAssertions](https://fluentassertions.com/) - The assertion library that Mockly.Http integrates with
* [Mockly.Net](https://github.com/dennisdoomen/mockly.net) - A general-purpose mocking framework (if it exists)

## License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.


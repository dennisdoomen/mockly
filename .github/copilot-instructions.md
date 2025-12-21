# GitHub Copilot Instructions for Mockly

## Project Overview

**Mockly** is a fluent HTTP mocking library for .NET that simplifies testing code that depends on `HttpClient`. The library provides:

- Fluent API for configuring HTTP request mocks
- Wildcard pattern matching for URLs
- Request capture and inspection capabilities
- Integration with FluentAssertions for expressive test assertions
- Support for .NET Framework 4.7.2+ and .NET 8.0+

### Key Design Principles

1. **Fluent API First**: All APIs should support method chaining with a natural, readable flow
2. **Sensible Defaults**: The library should work out of the box with minimal configuration
3. **Fail-Fast**: By default, unexpected requests throw exceptions to catch configuration issues early
4. **Extensibility**: Allow custom matchers, response generators, and request validators
5. **Performance**: Cache regex patterns and optimize hot paths

## Code Style and Conventions

### General Guidelines

- Follow the [C# Coding Guidelines](https://csharpcodingguidelines.com/)
- Code must pass all Roslyn analyzers: StyleCop, CSharpGuidelinesAnalyzer, Roslynator, and Meziantou.Analyzer
- Treat warnings as errors (`TreatWarningsAsErrors` is enabled)
- Use C# 11.0 language features appropriately
- Max line length: 130 characters

### Naming Conventions

- Use descriptive names that clearly convey intent
- Methods that start a fluent chain use present tense verbs (e.g., `ForGet`, `WithPath`, `RespondsWithStatus`)
- Avoid `this.` qualifier unless absolutely necessary
- Public APIs should have XML documentation comments

### Code Organization

- Keep related functionality in the same file
- Use `#pragma warning disable` sparingly and only for specific, documented reasons
- Namespace declarations: Use file-scoped namespaces where possible
- Usings: Place at top of file, sort system namespaces first

### Testing Practices

- All tests use xUnit framework
- Follow Arrange-Act-Assert (AAA) pattern consistently
- Test class structure: Nested classes group related test scenarios
- Test method naming: Use descriptive names like `Can_create_basic_mock_for_get_request`
- Use FluentAssertions for all assertions
- Code coverage must not decrease with new changes

### Example Test Structure

```csharp
public class HttpMockSpecs
{
    public class BasicUsage
    {
        [Fact]
        public async Task Can_create_basic_mock_for_get_request()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("api/test").RespondsWithStatus(HttpStatusCode.OK);

            // Act
            var response = await mock.GetClient().GetAsync("https://localhost/api/test");

            // Assert
            response.Should().Be200Ok();
        }
    }
}
```

## Building and Testing

### Prerequisites

- .NET SDK 8.0+ (specified in `global.json`)
- Windows, Linux, or macOS

### Build Commands

```bash
# Using PowerShell (Windows)
./build.ps1

# Using Bash (Linux/macOS)
./build.sh

# Using Nuke directly (if installed)
nuke
```

### Common Build Targets

- **Default**: Build, test, and package
- Use `--help` to see all available targets
- Use `--plan` to see what the build script will do

### Running Tests

Tests are automatically run during the build. The project uses:
- xUnit for test framework
- FluentAssertions for assertions
- Coverlet for code coverage
- Coverage reports are generated in `TestResults/reports/`

## API Design Guidelines

### Making API Changes

⚠️ **IMPORTANT**: All public API changes must go through an approval process:

1. Propose the API change in a separate issue first
2. Discuss and get the issue labeled with `api-approved`
3. After implementation, run `AcceptApiChanges.ps1` (Windows) or `AcceptApiChanges.sh` (Unix)
4. This generates/updates API verification files that must be committed

### Fluent Builder Pattern

The library heavily uses fluent builders. When extending:

- Return `this` or the appropriate builder type for chaining
- Allow configuration to be built up incrementally
- Support both specific and general configuration methods
- Example: `ForGet()` returns `RequestMockBuilder`, which has methods like `WithPath()`, `WithQuery()`, `RespondsWithStatus()`

### Wildcard Pattern Matching

- Use `*` as wildcard in paths and query strings
- Patterns are converted to regex and cached
- Be mindful of performance when matching
- Document pattern syntax in XML comments

## Dependency Management

### Adding Dependencies

- Avoid adding new dependencies unless absolutely necessary
- Prefer using existing .NET BCL types
- When adding analyzer packages, add to `Directory.Build.props` with conditions for `net8.0` only
- All analyzer packages should use `<PrivateAssets>all</PrivateAssets>`

### NuGet Package Structure

The project produces multiple packages:
- `Mockly` - Core library
- `FluentAssertions.Mockly.v7` - Assertions for FluentAssertions 7.x
- `FluentAssertions.Mockly.v8` - Assertions for FluentAssertions 8.x

## Target Frameworks

- Multi-target: `net472` and `net8.0`
- Use conditional compilation (`#if NET472_OR_GREATER`) when needed
- Analyzers only run on `net8.0` target to speed up builds
- Keep API surface compatible across both frameworks

## Common Patterns

### Request Matching

```csharp
// URL pattern matching
mock.ForGet("https://api.example.com/users/*?q=*")
    .RespondsWithStatus(HttpStatusCode.OK);

// Custom matchers
mock.ForGet()
    .WithPath("/api/data")
    .With(request => request.Headers.Contains("X-API-Key"))
    .RespondsWithStatus(HttpStatusCode.OK);
```

### Response Configuration

```csharp
// JSON responses (auto-serialization)
mock.ForGet().WithPath("/api/user")
    .RespondsWithJsonContent(new { Id = 1, Name = "John" });

// Status codes only
mock.ForPost().WithPath("/api/create")
    .RespondsWithStatus(HttpStatusCode.Created);

// Custom responses
mock.ForGet().WithPath("/api/custom")
    .RespondsWith(request => new HttpResponseMessage(HttpStatusCode.OK));
```

### Request Capture

```csharp
var capturedRequests = new RequestCollection();
mock.ForPatch()
    .WithPath("/api/update")
    .CollectingRequestIn(capturedRequests)
    .RespondsWithStatus(HttpStatusCode.NoContent);
```

## Contributing Workflow

1. **Fork and branch**: Create a feature branch from `main`
2. **Implement**: Make your changes following the style guidelines
3. **Test**: Ensure tests pass and coverage doesn't decrease
4. **API Changes**: Run `AcceptApiChanges.ps1`/`.sh` if you changed public APIs
5. **Pull Request**: Target the `main` branch
6. **Review**: Address feedback from maintainers

## EditorConfig

The project uses `.editorconfig` for consistent formatting:
- Indent style: spaces (4 spaces for C#, 2 spaces for project/config files)
- End of line: CRLF
- Insert final newline: true
- Trim trailing whitespace: true
- Max line length: 130 characters for C#
- New line preferences: Open braces on new line for all constructs (`csharp_new_line_before_open_brace = all`)
- Brace placement: Before `else`, `catch`, `finally` on new lines
- Indentation: Block contents indented, labels one less than current
- Naming: Avoid `this.` qualifier unless necessary

## Common Issues and Solutions

### Build Failures

- Ensure you have .NET 8.0 SDK installed
- Run `dotnet restore` if dependencies are missing
- Check that all analyzer warnings are addressed

### Test Failures

- Tests should be isolated and not depend on external services
- Use `HttpMock` to intercept all HTTP calls
- Async tests should properly await operations

### API Verification Failures

- Run `AcceptApiChanges.ps1` or `AcceptApiChanges.sh` after API changes
- Commit the generated verification files

## Documentation

- XML documentation is required for all public APIs
- README.md should be updated for significant features
- Include code examples for new features
- Keep documentation in sync with implementation

## Security

- Never commit secrets or API keys
- Validate and sanitize input in custom matchers
- Be cautious with regex patterns (avoid ReDoS vulnerabilities)

## Performance Considerations

- Regex patterns are cached for efficient reuse
- Prefetch body content by default (`PrefetchBody = true`) for matcher efficiency
- Avoid allocations in hot paths
- Consider async/await overhead in mock configuration

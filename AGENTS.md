# AGENTS.md

Guidance for AI coding agents working in the **Mockly** repository. Mockly is a fluent
HTTP mocking library for .NET that intercepts `HttpClient` calls for use in tests.

## Project overview

Mockly provides a fluent API for configuring HTTP request mocks, capturing request
details, and asserting on HTTP interactions. It intercepts `HttpClient` calls via a custom
`HttpMessageHandler`. Unmatched requests throw `UnexpectedRequestException` by default
(`FailOnUnexpectedCalls = true`). The default base address is `https://localhost/`.

Key design principles:

1. **Fluent API first** — all APIs support natural, readable method chaining.
2. **Sensible defaults** — works out of the box with minimal configuration.
3. **Fail-fast** — unexpected requests throw by default to catch misconfiguration early.
4. **Extensibility** — supports custom matchers, response generators, and validators.
5. **Performance** — regex patterns are cached; hot paths avoid allocations.

User-facing documentation lives at [mockly.org](https://mockly.org/) (source under
`website/docs/`). The `README.md` is the canonical high-level overview.

## Repository layout

| Path | Purpose |
| --- | --- |
| `Mockly/` | Core library (multi-targeted `net8.0;net472`). Produces the `Mockly` NuGet package. |
| `Mockly.Specs/` | xUnit specification tests (`net8.0` + `net472`). Main test suite. |
| `Mockly.ApiVerificationTests/` | Public API approval tests using `PublicApiGenerator` + `Verify`. |
| `Build/` | Nuke build automation (`Build.cs`, `_build.csproj`). |
| `website/` | Docusaurus documentation site (`website/docs/`). |
| `SKILL.md` | Mockly authoring skill, packaged into consuming repos' `.agents/skills/mockly/`. |
| `.junie/guidelines.md` | Older developer notes (some references use the legacy `Mockly.Http` naming). |
| `Directory.Build.props` | Shared build settings: `LangVersion`, warnings-as-errors, analyzers. |
| `global.json` | Pins the .NET SDK (8.0.100, roll-forward latestMajor). |

Core library files (under `Mockly/`): `HttpMock.cs`, `RequestMock.cs`,
`RequestMockBuilder.cs`, `RequestMockResponseBuilder.cs`, `Matcher.cs`,
`RequestCollection.cs`, `CapturedRequest.cs`, `RequestInfo.cs`, `IResponseBuilder.cs`,
`UnexpectedRequestException.cs`, `RequestMatchingException.cs`.

> Note: the README mentions `FluentAssertions.Mockly.v7` / `.v8` packages. Those projects
> are referenced via `InternalsVisibleTo` but are not present in this solution
> (`Mockly.sln` contains `Mockly`, `Mockly.Specs`, `Mockly.ApiVerificationTests`, and the
> Nuke `_build` project). The FluentAssertions v7 and v8 extensions for Mockly live in a
> separate repository:
> [dennisdoomen/fluentassertions.mockly](https://github.com/dennisdoomen/fluentassertions.mockly).
> Verify the current solution before assuming a project exists.

## Building

Prerequisites: the .NET SDKs for .NET Framework 4.7.2 and .NET 8.0.

Preferred (consistent with CI), from the repo root:

```powershell
./build.ps1            # Windows
```

```bash
./build.sh             # Linux/macOS
```

These bootstrap Nuke (`Build/Build.cs`). Use `./build.ps1 --help` to list targets and
`./build.ps1 --plan` to preview what runs. The default flow builds, runs InspectCode,
runs tests, runs API checks, generates a coverage report, scans packages, and packs.

Plain SDK build also works:

```bash
dotnet build Mockly.sln -c Debug
```

Build notes:

- `TreatWarningsAsErrors` is enabled — keep the build warning-clean.
- Analyzers (StyleCop, CSharpGuidelinesAnalyzer, Roslynator, Meziantou, BannedApi) run
  **only** on the `net8.0` target to keep multi-target builds fast. Fix analyzer issues on
  the `net8.0` build first.
- `LangVersion` is 11 via `Directory.Build.props`; the `Mockly` project overrides to 14.

## Testing

Run the full suite:

```bash
dotnet test -c Debug
```

Run a single test or a class by fully-qualified name:

```bash
dotnet test --filter FullyQualifiedName=Mockly.Specs.HttpMockSpecs+BasicUsage.Can_create_basic_mock_for_get_request
dotnet test --filter FullyQualifiedName~Mockly.Specs.HttpMockSpecs+BasicUsage
```

Testing conventions:

- xUnit (`[Fact]`, `[Theory]`) with the **Arrange-Act-Assert** pattern.
- Group scenarios using **nested classes** inside the spec class (see
  `Mockly.Specs/HttpMockSpecs.cs`).
- Use descriptive snake-case method names, e.g. `Can_create_basic_mock_for_get_request`.
- Use **FluentAssertions** for all assertions.
- Tests are run in Debug so FluentAssertions can report variable names.
- Code coverage must not decrease; CI gathers coverage via Coverlet.
- Tests must be isolated — never hit real external services; use `HttpMock` to intercept.

Example:

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
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
```

## Public API changes (important)

Mockly reviews every public API addition, change, or deletion.

1. Propose the API change in a separate issue and get it labeled `api-approved` **before**
   opening a PR.
2. After implementing, the API approval tests in `Mockly.ApiVerificationTests/` compare the
   compiled public surface against the baselines in
   `Mockly.ApiVerificationTests/ApprovedApi/`.
3. When the approval test fails for an intended change, run `AcceptApiChanges.ps1`
   (Windows) or `AcceptApiChanges.sh` (Unix) — or Rider's Verify Support plugin — to update
   the approved files, then commit them.
4. Build the solution before running the approval tests so the `Mockly.dll` exists for each
   target framework (the test discovers TFMs dynamically from `Mockly.csproj`).

## Code style and conventions

- Follow the C# coding guidelines as captured in the
  [csharp-guidelines skill](https://github.com/dennisdoomen/CSharpGuidelines/tree/main/Skills/csharp-guidelines).
- Multi-target compatible: keep the API surface working on both `net472` and `net8.0`. Use
  `#if NET472_OR_GREATER` guards only when necessary and keep divergence minimal.
- File-scoped namespaces; `using` directives at the top, system namespaces first.
- Avoid the `this.` qualifier unless required.
- Max line length 130; indent with 4 spaces; CRLF line endings; final newline; trim
  trailing whitespace (enforced by `.editorconfig`).
- Open braces on a new line for all constructs; `else`/`catch`/`finally` on new lines.
- Public APIs require XML documentation comments.
- Use `#pragma warning disable` sparingly and only with a documented reason.
- Comment only code that genuinely needs clarification.

## Dependency management

- Avoid new dependencies; prefer existing .NET BCL types.
- Add analyzer packages to `Directory.Build.props` conditioned on `net8.0`, with
  `<PrivateAssets>all</PrivateAssets>`.
- Never commit secrets or API keys.
- Be cautious with regex patterns to avoid ReDoS; validate input in custom matchers.

## Common usage patterns (for writing tests and examples)

```csharp
var mock = new HttpMock();

// Matching
mock.ForGet().WithPath("/api/users/*").RespondsWithJsonContent(user);
mock.ForGet("https://api.example.com/users/*?q=*").RespondsWithStatus(HttpStatusCode.OK);
mock.ForGet().WithPath("/api/search").WithQuery("?q=*").RespondsWithStatus(HttpStatusCode.OK);
mock.ForGet().WithPath("/api/secure").With(req => req.Headers.Contains("X-API-Key"))
    .RespondsWithStatus(HttpStatusCode.OK);

// Responses
mock.ForGet().WithPath("/api/user").RespondsWithJsonContent(new { Id = 1, Name = "Alice" });
mock.ForDelete().WithPath("/api/items/1").RespondsWithStatus(HttpStatusCode.NoContent);
mock.ForGet().WithPath("/odata/items").RespondsWithODataResult(new { Id = 1 });

// Invocation limits
mock.ForGet().WithPath("/api/once").RespondsWithStatus(HttpStatusCode.OK).Once();

// Request capture
var captured = new RequestCollection();
mock.ForPatch().WithPath("/api/update").CollectingRequestsIn(captured);

HttpClient client = mock.GetClient();
```

`SKILL.md` contains a detailed, up-to-date cheat sheet of the fluent API surface (URL,
body, response, capture, invocation-limit, reset/clear, and assertion APIs). Consult it
when writing tests with Mockly.

## Contributing workflow

1. Work on a feature branch; target PRs at `main`.
2. Follow the style guidelines and keep the build warning-clean.
3. Cover changes with AAA-structured tests; do not decrease coverage.
4. For any public API change, follow the approval process above and commit updated approved
   API files.
5. Keep the docs (`README.md`, `website/docs/`) in sync with behavior changes.

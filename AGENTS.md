# AGENTS.md

Guidance for AI coding agents working in **Mockly**, a fluent HTTP mocking library for .NET
that intercepts `HttpClient` calls in tests. For the user-facing overview see
[`README.md`](./README.md) and [mockly.org](https://mockly.org/); for the fluent API cheat
sheet see [`SKILL.md`](./SKILL.md).

## Layout

- `Mockly/` — core library, multi-targeted `net8.0;net472` (the `Mockly` NuGet package).
- `Mockly.Specs/` — xUnit test suite (`net8.0` + `net472`).
- `Mockly.ApiVerificationTests/` — public API approval tests (`PublicApiGenerator` + Verify).
- `Build/` — Nuke build automation. `website/` — Docusaurus docs.

The FluentAssertions v7/v8 extensions live in a separate repo:
[dennisdoomen/fluentassertions.mockly](https://github.com/dennisdoomen/fluentassertions.mockly)
(referenced here only via `InternalsVisibleTo`).

## Build & test

```bash
./build.ps1        # or ./build.sh — Nuke build used by CI (build, test, API checks, pack)
dotnet build Mockly.sln -c Debug
dotnet test -c Debug
dotnet test --filter FullyQualifiedName~Mockly.Specs.HttpMockSpecs+BasicUsage
```

- `TreatWarningsAsErrors` is on; analyzers run **only** on `net8.0` — fix issues there first.
- Tests run in Debug so FluentAssertions can report variable names. Keep coverage non-decreasing.

## Conventions

- C# style: follow the
  [csharp-guidelines skill](https://github.com/dennisdoomen/CSharpGuidelines/tree/main/Skills/csharp-guidelines);
  `.editorconfig` is authoritative (4-space indent, 130-col lines, braces on new lines).
- Tests: xUnit, Arrange-Act-Assert, scenarios grouped in nested classes (see
  `Mockly.Specs/HttpMockSpecs.cs`), snake-case method names, FluentAssertions for assertions.
- Chain-starting fluent methods use present-tense verbs (`ForGet`, `WithPath`, `RespondsWithStatus`).
- Keep the public API working on both `net472` and `net8.0`; XML-doc public members. Use
  `#if NET472_OR_GREATER` for the rare TFM-specific divergence and keep it minimal.
- Avoid new dependencies; never commit secrets; guard against ReDoS in matchers. Add any new
  analyzer packages to `Directory.Build.props` conditioned on `net8.0` with `<PrivateAssets>all</PrivateAssets>`.

## Public API changes

Public API changes need an `api-approved` issue first. When the approval tests in
`Mockly.ApiVerificationTests/` fail for an intended change, run `AcceptApiChanges.ps1` /
`AcceptApiChanges.sh` (or Rider's Verify Support) and commit the updated `ApprovedApi/` files.

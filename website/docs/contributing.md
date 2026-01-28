---
sidebar_position: 6
---

# Contributing

Your contributions are always welcome!

## Contribution Guidelines

Please have a look at the [contribution guidelines](https://github.com/dennisdoomen/mockly/blob/main/CONTRIBUTING.md) first.

## Contributing Workflow

1. **Fork and branch**: Create a feature branch from `main`
2. **Implement**: Make your changes following the style guidelines
3. **Test**: Ensure tests pass and coverage doesn't decrease
4. **API Changes**: Run `AcceptApiChanges.ps1`/`.sh` if you changed public APIs
5. **Pull Request**: Target the `main` branch
6. **Review**: Address feedback from maintainers

## Code Style

The project follows the [C# Coding Guidelines](https://csharpcodingguidelines.com/) and uses multiple Roslyn analyzers:

- StyleCop
- CSharpGuidelinesAnalyzer
- Roslynator
- Meziantou.Analyzer

All warnings are treated as errors.

## API Changes

⚠️ **IMPORTANT**: All public API changes must go through an approval process:

1. Propose the API change in a separate issue first
2. Discuss and get the issue labeled with `api-approved`
3. After implementation, run `AcceptApiChanges.ps1` (Windows) or `AcceptApiChanges.sh` (Unix)
4. This generates/updates API verification files that must be committed

## Previous Contributors

<a href="https://github.com/dennisdoomen/mockly/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=dennisdoomen/mockly" alt="contrib.rocks image" />
</a>

(Made with [contrib.rocks](https://contrib.rocks))

## Code of Conduct

Please read and follow our [Code of Conduct](https://github.com/dennisdoomen/mockly/blob/main/CODE_OF_CONDUCT.md).

## Versioning

This library uses [Semantic Versioning](https://semver.org/) to give meaning to the version numbers. For the versions available, see the [releases](https://github.com/dennisdoomen/mockly/releases) on this repository.

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

* [FluentAssertions](https://fluentassertions.com/) - The assertion library that Mockly integrates with
* [PackageGuard](https://github.com/dennisdoomen/packageguard) - Get a grip on your open-source packages
* [Reflectify](https://github.com/dennisdoomen/reflectify) - Reflection extensions without causing dependency pains
* [Pathy](https://github.com/dennisdoomen/pathy) - Fluently building and using file and directory paths without binary dependencies
* [.NET Library Starter Kit](https://github.com/dennisdoomen/dotnet-library-starter-kit) - A battle-tested starter kit for building open-source and internal NuGet libraries

## License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/dennisdoomen/mockly/blob/main/LICENSE) file for details.

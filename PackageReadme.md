
> [!TIP]
> What to do next after generating the template

The template makes a lot of assumptions, so after generating the project, there's a couple of things you can tweak.

* Update the `Readme.md` and `PackageReadme.md` with information about your library
* Review the guidelines in `CONTRIBUTING.md` to see if it aligns with how you want to handle contributions
* Review the issue templates under `.github/issue_template`
* Set-up labels in GitHub matching those in the `release.yml` so you can label pull requests accordingly
* Adjust the .NET frameworks this library should target
* Adjust the root namespace and assembly names
* Alter the coverage service that is being used.
* Determine if you want to use API verification against snapshots
* Study the Nuke `build.cs` file or invoking it through `build.ps1 -plan` to see how it works
* See if all dependencies are up-to-date
* Configure NuGet auditing (see next paragraph)
* Fine-tune the allowed open-source licenses and packages in the `.\packageguard\config.json`
* Store the PackageGuard cache that appears under `.\packageguard` after a first build in source control to speed-up successive runs

> [!NOTE]
> Before the first time the build script has run on your new solution, the `.nuspec` file is still called `nuspec`. This was needed because `dotnet pack` refuses to include the `.nuspec` file in the template package this repository produces. This file is automatically renamed after the first time the `build.ps1` script is run. 

> [!TIP]
> Also check-out the [main repository](https://github.com/dennisdoomen/dotnet-library-starter-kit) for additional information on these generated solutions.

## About

### What's this?

Add stuff like:
* Mockly.Http offers
* what .NET, C# other versions of dependencies it supports

### What's so special about that?

* What makes it different from other libraries?
* Why did you create it.
* What problem does it solve?

### Who created this?
* Something about you, your company, your team, etc.

## How do I use it?
* Code examples
* Where to find more examples

```csharp
Some example code showing your library
```

## Download
This library can be installed by adding the GitHub Packages feed to your package manager:

  `dotnet nuget add source --username USERNAME --password ${{ secrets.GITHUB_TOKEN }} --store-password-in-clear-text --name github "https://nuget.pkg.github.com/NAMESPACE/index.json"` 

Read more about [GitHub Packages](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry).

Then, install the package using the following command-line:

  `dotnet add package mockly.http`

## Building

To build this repository locally, you need the following:
* The [.NET SDKs](https://dotnet.microsoft.com/en-us/download/visual-studio-sdks) for .NET 4.7 and 8.0.
* Visual Studio, JetBrains Rider or Visual Studio Code with the C# DevKit

You can also build, run the unit tests and package the code using the following command-line:

`build.ps1`

Or, if you have, the [Nuke tool installed](https://nuke.build/docs/getting-started/installation/):

`nuke`

Also try using `--help` to see all the available options or `--plan` to see what the scripts does.


## Versioning
This library uses [Semantic Versioning](https://semver.org/) to give meaning to the version numbers. For the versions available, see the [tags](/releases) on this repository.

## Credits
This library wouldn't have been possible without the following tools, packages and companies:

* [Nuke](https://nuke.build/) - Smart automation for DevOps teams and CI/CD pipelines by [Matthias Koch](https://github.com/matkoch)
* [xUnit](https://xunit.net/) - Community-focused unit testing tool for .NET by [Brad Wilson](https://github.com/bradwilson)
* [Coverlet](https://github.com/coverlet-coverage/coverlet) - Cross platform code coverage for .NET by [Toni Solarin-Sodara](https://github.com/tonerdo)
* [Polysharp](https://github.com/Sergio0694/PolySharp) - Generated, source-only polyfills for C# language features by [Sergio Pedri](https://github.com/Sergio0694)
* [GitVersion](https://gitversion.net/) - From git log to SemVer in no time
* [ReportGenerator](https://reportgenerator.io/) - Converts coverage reports by [Daniel Palme](https://github.com/danielpalme)
* [StyleCopyAnalyzer](https://github.com/DotNetAnalyzers/StyleCopAnalyzers) - StyleCop rules for .NET
* [Roslynator](https://github.com/dotnet/roslynator) - A set of code analysis tools for C# by [Josef Pihrt](https://github.com/josefpihrt)
* [CSharpCodingGuidelines](https://github.com/bkoelman/CSharpGuidelinesAnalyzer) - Roslyn analyzers by [Bart Koelman](https://github.com/bkoelman) to go with the [C# Coding Guidelines](https://csharpcodingguidelines.com/)
* [Meziantou](https://github.com/meziantou/Meziantou.Framework) - Another set of awesome Roslyn analyzers by [Gérald Barré](https://github.com/meziantou)
* [Verify](https://github.com/VerifyTests/Verify) - Snapshot testing by [Simon Cropp](https://github.com/SimonCropp)

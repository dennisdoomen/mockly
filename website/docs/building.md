---
sidebar_position: 5
---

# Building

Information about building the Mockly project from source.

## Prerequisites

To build this repository locally, you need the following:

* The [.NET SDKs](https://dotnet.microsoft.com/en-us/download/visual-studio-sdks) for .NET 4.7 and 8.0.
* Visual Studio, JetBrains Rider or Visual Studio Code with the C# DevKit

## Building from Command Line

You can build, run the unit tests and package the code using the following command-line:

### PowerShell

```powershell
./build.ps1
```

### Bash

```bash
./build.sh
```

### Using Nuke

Or, if you have the [Nuke tool installed](https://nuke.build/docs/getting-started/installation/):

```bash
nuke
```

## Build Options

Also try using `--help` to see all the available options or `--plan` to see what the scripts does.

```bash
./build.ps1 --help
./build.ps1 --plan
```

## Target Frameworks

The project uses multi-targeting:
- `net472` - .NET Framework 4.7.2
- `net8.0` - .NET 8.0

Analyzers only run on `net8.0` target to speed up builds.

## NuGet Packages

The project produces multiple packages:
- `Mockly` - Core library
- `FluentAssertions.Mockly.v7` - Assertions for FluentAssertions 7.x
- `FluentAssertions.Mockly.v8` - Assertions for FluentAssertions 8.x

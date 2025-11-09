using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Coverlet;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Tools.ReportGenerator;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.ReportGenerator.ReportGeneratorTasks;
using static Serilog.Log;

class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode
    public static int Main() => Execute<Build>(x => x.Default);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    GitHubActions GitHubActions => GitHubActions.Instance;

    string BranchSpec => GitHubActions?.Ref;

    string BuildNumber => GitHubActions?.RunNumber.ToString();

    [Parameter(
        "Set the URI specifying the location of the NuGet artifacts API, which is used to publish packages generated during the build process.")]
    readonly string NugetArtifactsApiUri = "https://api.nuget.org/v3/index.json";

    [Parameter("The API key used to authenticate and authorize access to the NuGet artifacts API.")]
    [Secret]
    readonly string NugetArtifactsApiKey;

    [Parameter("The key to use for scanning packages on GitHub")]
    [Secret]
    readonly string GitHubApiKey;

    [Solution(GenerateProjects = true)]
    readonly Solution Solution;

    [GitVersion(Framework = "net8.0", NoFetch = true, NoCache = true)]
    readonly GitVersion GitVersion;

    AbsolutePath ArtifactsDirectory => RootDirectory / "Artifacts";

    AbsolutePath TestResultsDirectory => ArtifactsDirectory / "TestResults";

    AbsolutePath CoverageResultsFile => TestResultsDirectory / "Cobertura.xml";

    [NuGetPackage("PackageGuard", "PackageGuard.dll")]
    Tool PackageGuard;

    [NuGetPackage("JetBrains.ReSharper.GlobalTools", "inspectcode.exe")]
    Tool InspectCode;

    string SemVer;

    Target CalculateNugetVersion => _ => _
        .Executes(() =>
        {
            SemVer = GitVersion.SemVer;
            if (IsPullRequest)
            {
                Information(
                    "Branch spec {branchspec} is a pull request. Adding build number {buildnumber}",
                    BranchSpec, BuildNumber);

                SemVer = string.Join('.', GitVersion.SemVer.Split('.').Take(3).Union(new[]
                {
                    BuildNumber
                }));
            }

            Information("SemVer = {semver}", SemVer);
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution)
                .EnableNoCache());
        });

    Target Compile => _ => _
        .DependsOn(CalculateNugetVersion)
        .DependsOn(Restore)
        .Executes(() =>
        {
            ReportSummary(s => s
                .WhenNotNull(SemVer, (summary, semVer) => summary
                    .AddPair("Version", semVer)));

            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoLogo()
                .EnableNoRestore()
				.SetVersion(SemVer)
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion.InformationalVersion) );
        });

    Target RunInspectCode => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            InspectCode($"Mockly.Http.sln -o={ArtifactsDirectory / "CodeIssues.sarif"} --no-build");
        });

    Target RunTests => _ => _
        .DependsOn(Compile, RunInspectCode)
        .Executes(() =>
        {
            TestResultsDirectory.CreateOrCleanDirectory();
            var project = Solution.GetProject("Mockly.Http.Specs");

            DotNetTest(s => s
                // We run tests in debug mode so that Fluent Assertions can show the names of variables
                .SetConfiguration(Configuration.Debug)
                // To prevent the machine language to affect tests sensitive to the current thread's culture
                .SetProcessEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
                .SetDataCollector("XPlat Code Coverage")
                .SetCollectCoverage(true)
                .SetCoverletOutputFormat(CoverletOutputFormat.cobertura)
                .SetResultsDirectory(TestResultsDirectory)
                .SetProjectFile(project)
                .CombineWith(project.GetTargetFrameworks(),
                    (ss, framework) => ss
                        .SetFramework(framework)
                        .AddLoggers($"trx;LogFileName={framework}.trx")
                ));
        });

    Target ApiChecks => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var project = Solution.GetProject("Mockly.Http.ApiVerificationTests");

            DotNetTest(s => s
                .SetConfiguration(Configuration)
                .SetProcessEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
                .SetResultsDirectory(TestResultsDirectory)
                .SetProjectFile(project)
                .AddLoggers($"trx;LogFileName={project!.Name}.trx"));
        });

    Target ScanPackages => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            Environment.SetEnvironmentVariable("GITHUB_API_KEY", GitHubApiKey);
            PackageGuard($"--config-path={RootDirectory / ".packageguard" / "config.json"} --use-caching {RootDirectory}");
        });

    Target GenerateCodeCoverageReport => _ => _
        .DependsOn(RunTests)
        .Executes(() =>
        {
            ReportGenerator(s => s
				.AddReports(TestResultsDirectory / "**/coverage.cobertura.xml")
                .AddReportTypes(ReportTypes.lcov, ReportTypes.Html)
                .SetTargetDirectory(TestResultsDirectory / "reports")
                .AddFileFilters("-*.g.cs"));

            string link = TestResultsDirectory / "reports" / "index.html";
            Information($"Code coverage report: \x1b]8;;file://{link.Replace('\\', '/')}\x1b\\{link}\x1b]8;;\x1b\\");
        });


    Target Pack => _ => _
        .DependsOn(ScanPackages)
        .DependsOn(CalculateNugetVersion)
        .DependsOn(ApiChecks)
        .DependsOn(GenerateCodeCoverageReport)
        .Executes(() =>
        {
            ReportSummary(s => s
                .WhenNotNull(SemVer, (c, semVer) => c
                    .AddPair("Packed version", semVer)));

            // Because of limitations in the template package that was used to create this build script,
            // we need to rename the nuspec files back to .nuspec files.
            RootDirectory.GlobFiles("**/nuspec").ForEach(p =>
            {
                p.Rename(".nuspec", ExistsPolicy.FileOverwrite);
            });

            // Pack main Mockly.Http library
            DotNetPack(s => s
                .SetProject(Solution.GetProject("Mockly.Http"))
                .SetOutputDirectory(ArtifactsDirectory)
                .SetConfiguration(Configuration)
                .EnableNoBuild()
                .EnableNoLogo()
                .EnableNoRestore()
                .EnableContinuousIntegrationBuild() // Necessary for deterministic builds
                .SetVersion(SemVer));

            // Pack FluentAssertions.Mockly.Http.v7 extension
            DotNetPack(s => s
                .SetProject(Solution.GetProject("FluentAssertions.Mockly.Http.v7"))
                .SetOutputDirectory(ArtifactsDirectory)
                .SetConfiguration(Configuration)
                .EnableNoBuild()
                .EnableNoLogo()
                .EnableNoRestore()
                .EnableContinuousIntegrationBuild() // Necessary for deterministic builds
                .SetVersion(SemVer));

            // Pack FluentAssertions.Mockly.Http.v8 extension
            DotNetPack(s => s
                .SetProject(Solution.GetProject("FluentAssertions.Mockly.Http.v8"))
                .SetOutputDirectory(ArtifactsDirectory)
                .SetConfiguration(Configuration)
                .EnableNoBuild()
                .EnableNoLogo()
                .EnableNoRestore()
                .EnableContinuousIntegrationBuild() // Necessary for deterministic builds
                .SetVersion(SemVer));
        });


    Target Push => _ => _
        .DependsOn(Pack)
        .OnlyWhenDynamic(() => IsTag)
        .ProceedAfterFailure()
        .Executes(() =>
        {
            var packages = ArtifactsDirectory.GlobFiles("*.nupkg");

            Assert.NotEmpty(packages);

            DotNetNuGetPush(s => s
                .SetApiKey(NugetArtifactsApiKey)
                .EnableSkipDuplicate()
                .SetSource(NugetArtifactsApiUri)
                .EnableNoSymbols()
                .CombineWith(packages,
                    (v, path) => v.SetTargetPath(path)));

            // Attest packages for provenance (requires GitHub CLI and OIDC token)
            if (GitHubActions != null)
            {
                foreach (var package in packages)
                {
                    try
                    {
                        Information($"Attesting package: {package}");
                        ProcessTasks.StartProcess("gh", $"attestation attest --predicate-type https://slsa.dev/provenance/v1 {package}")
                            .AssertZeroExitCode();
                    }
                    catch (Exception ex)
                    {
                        Warning($"Failed to attest package {package}: {ex.Message}");
                    }
                }
            }
        });

    Target Default => _ => _
		.DependsOn(Pack)
        .DependsOn(Push);

    bool IsPullRequest => GitHubActions?.IsPullRequest ?? false;

    bool IsTag => BranchSpec != null && BranchSpec.Contains("refs/tags", StringComparison.OrdinalIgnoreCase);
}

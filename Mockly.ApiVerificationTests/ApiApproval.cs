using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using Pathy;
using PublicApiGenerator;
using VerifyTests;
using VerifyTests.DiffPlex;
using VerifyXunit;
using Xunit;

namespace Mockly.ApiVerificationTests;

public class ApiApproval
{
    private static readonly ChainablePath SourcePath = ChainablePath.Current / ".." / ".." / ".." / "..";

    static ApiApproval()
    {
        VerifyDiffPlex.Initialize(OutputType.Minimal);
    }

    [Theory]
    [ClassData(typeof(TargetFrameworksTheoryData))]
    public Task ApproveApi(string framework)
    {
        var configuration = typeof(ApiApproval).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var assemblyFile = SourcePath / "Mockly" / "bin" / configuration / framework / "Mockly.dll";
        var assembly = Assembly.LoadFile(assemblyFile);
        var publicApi = assembly.GeneratePublicApi(options: null);

        return Verifier
            .Verify(publicApi)
            .ScrubLinesContaining("FrameworkDisplayName")
            .UseDirectory("ApprovedApi")
            .UseFileName(framework)
            .DisableDiff();
    }

    // Approve public API for FluentAssertions extension packages as well
    [Theory]
    [ClassData(typeof(AssertionsV7FrameworksTheoryData))]
    public Task ApproveApi_FluentAssertions_v7(string framework)
    {
        var configuration = typeof(ApiApproval).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        const string projectName = "FluentAssertions.Mockly.v7";

        var assemblyFile = SourcePath / projectName / "bin" / configuration / framework / (projectName + ".dll");
        var assembly = Assembly.LoadFile(assemblyFile);
        var publicApi = assembly.GeneratePublicApi(options: null);

        return Verifier
            .Verify(publicApi)
            .ScrubLinesContaining("FrameworkDisplayName")
            .UseDirectory("ApprovedApi")
            .UseFileName($"{projectName}.{framework}")
            .DisableDiff();
    }

    [Theory]
    [ClassData(typeof(AssertionsV8FrameworksTheoryData))]
    public Task ApproveApi_FluentAssertions_v8(string framework)
    {
        var configuration = typeof(ApiApproval).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        const string projectName = "FluentAssertions.Mockly.v8";

        var assemblyFile = SourcePath / projectName / "bin" / configuration / framework / (projectName + ".dll");
        var assembly = Assembly.LoadFile(assemblyFile);
        var publicApi = assembly.GeneratePublicApi(options: null);

        return Verifier
            .Verify(publicApi)
            .ScrubLinesContaining("FrameworkDisplayName")
            .UseDirectory("ApprovedApi")
            .UseFileName($"{projectName}.{framework}")
            .DisableDiff();
    }

    private class TargetFrameworksTheoryData : TheoryData<string>
    {
        public TargetFrameworksTheoryData()
        {
            var csproj = SourcePath / "Mockly" / "Mockly.csproj";
            var project = XDocument.Load(csproj);
            var targetFrameworks = project.XPathSelectElement("/Project/PropertyGroup/TargetFrameworks");
            AddRange(targetFrameworks!.Value.Split(';'));
        }
    }

    private class AssertionsV7FrameworksTheoryData : TheoryData<string>
    {
        public AssertionsV7FrameworksTheoryData()
        {
            var csproj = SourcePath / "FluentAssertions.Mockly.v7" / "FluentAssertions.Mockly.v7.csproj";
            var project = XDocument.Load(csproj);
            var targetFrameworks = project.XPathSelectElement("/Project/PropertyGroup/TargetFrameworks");
            if (targetFrameworks is not null)
            {
                AddRange(targetFrameworks.Value.Split(';'));
            }
            else
            {
                // Fallback if single TargetFramework is used
                var targetFramework = project.XPathSelectElement("/Project/PropertyGroup/TargetFramework");
                if (targetFramework is not null)
                    Add(targetFramework.Value);
            }
        }
    }

    private class AssertionsV8FrameworksTheoryData : TheoryData<string>
    {
        public AssertionsV8FrameworksTheoryData()
        {
            var csproj = SourcePath / "FluentAssertions.Mockly.v8" / "FluentAssertions.Mockly.v8.csproj";
            var project = XDocument.Load(csproj);
            var targetFrameworks = project.XPathSelectElement("/Project/PropertyGroup/TargetFrameworks");
            if (targetFrameworks is not null)
            {
                AddRange(targetFrameworks.Value.Split(';'));
            }
            else
            {
                // Fallback if single TargetFramework is used
                var targetFramework = project.XPathSelectElement("/Project/PropertyGroup/TargetFramework");
                if (targetFramework is not null)
                    Add(targetFramework.Value);
            }
        }
    }
}

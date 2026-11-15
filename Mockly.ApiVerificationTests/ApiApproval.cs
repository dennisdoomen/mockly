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
    public Task ApproveApi(string project, string framework)
    {
        var configuration = typeof(ApiApproval).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var assemblyFile = SourcePath / project / "bin" / configuration / framework / $"{project}.dll";
        var assembly = Assembly.LoadFile(assemblyFile);
        string publicApi = assembly.GeneratePublicApi(options: null).ReplaceLineEndings("\n");

        // The approved API of the core package predates the add-on packages, so it keeps its original file names
        string fileName = project == "Mockly" ? framework : $"{project}.{framework}";

        return Verifier
            .Verify(publicApi)
            .ScrubLinesContaining("FrameworkDisplayName")
            .UseDirectory("ApprovedApi")
            .UseFileName(fileName)
            .DisableDiff();
    }

    private class TargetFrameworksTheoryData : TheoryData<string, string>
    {
        private static readonly string[] Projects = { "Mockly", "Mockly.OpenApi" };

        public TargetFrameworksTheoryData()
        {
            foreach (string project in Projects)
            {
                var csproj = SourcePath / project / $"{project}.csproj";
                var document = XDocument.Load(csproj);
                var targetFrameworks = document.XPathSelectElement("/Project/PropertyGroup/TargetFrameworks");

                foreach (string framework in targetFrameworks!.Value.Split(';'))
                {
                    Add(project, framework);
                }
            }
        }
    }
}

using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace OverHorizon.Tests;

public sealed class ArchitectureTests
{
    private static readonly string[] ClientOnlyProjects =
    [
        "OverHorizon.Rendering",
        "OverHorizon.Audio",
        "OverHorizon.Client",
    ];

    [Fact]
    public void ProjectDependencies_FormStrictAcyclicGraph()
    {
        var root = FindRepositoryRoot();
        var graph = LoadSrcProjectGraph(root);

        graph.Should().HaveCount(11);

        var visitState = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var project in graph.Keys)
        {
            HasCycle(project, graph, visitState).Should().BeFalse($"project '{project}' must not participate in a dependency cycle");
        }
    }

    [Fact]
    public void CoreProject_HasZeroProjectAndPackageDependencies()
    {
        var root = FindRepositoryRoot();
        var coreCsproj = Path.Combine(root, "src", "OverHorizon.Core", "OverHorizon.Core.csproj");
        var doc = XDocument.Load(coreCsproj);

        doc.Descendants("ProjectReference").Should().BeEmpty("OverHorizon.Core is the pure Domain layer");
        doc.Descendants("PackageReference").Should().BeEmpty("OverHorizon.Core must not depend on external packages");
    }

    [Fact]
    public void ServerAndSharedProjects_NeverReferenceClientOnlyProjects()
    {
        var root = FindRepositoryRoot();
        var graph = LoadSrcProjectGraph(root);

        foreach (var (project, references) in graph)
        {
            if (ClientOnlyProjects.Contains(project, StringComparer.Ordinal))
            {
                continue;
            }

            references.Should().NotContain(
                ClientOnlyProjects,
                $"server/shared project '{project}' must remain headless and independent of client presentation");
        }
    }

    [Fact]
    public void SourceFiles_DoNotExceed500Lines()
    {
        var root = FindRepositoryRoot();
        var srcDir = Path.Combine(root, "src");
        var csFiles = Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        foreach (var file in csFiles)
        {
            var lineCount = File.ReadLines(file).Count();
            lineCount.Should().BeLessThanOrEqualTo(500, $"file '{Path.GetRelativePath(root, file)}' must not become a God class (>500 lines)");
        }
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OverHorizon.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate OverHorizon.sln above test base directory.");
    }

    private static Dictionary<string, string[]> LoadSrcProjectGraph(string repoRoot)
    {
        var srcDir = Path.Combine(repoRoot, "src");
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var csproj in Directory.EnumerateFiles(srcDir, "*.csproj", SearchOption.AllDirectories))
        {
            var projectName = Path.GetFileNameWithoutExtension(csproj);
            var doc = XDocument.Load(csproj);
            var refs = doc.Descendants("ProjectReference")
                .Select(static el => el.Attribute("Include")?.Value)
                .Where(static v => !string.IsNullOrWhiteSpace(v))
                .Select(static v => Path.GetFileNameWithoutExtension(v!.Replace('\\', '/')))
                .ToArray();

            result[projectName] = refs;
        }

        return result;
    }

    private static bool HasCycle(
        string node,
        Dictionary<string, string[]> graph,
        Dictionary<string, int> state)
    {
        if (state.TryGetValue(node, out var current))
        {
            return current == 1;
        }

        state[node] = 1;
        if (graph.TryGetValue(node, out var neighbors))
        {
            foreach (var next in neighbors)
            {
                if (HasCycle(next, graph, state))
                {
                    return true;
                }
            }
        }

        state[node] = 2;
        return false;
    }
}

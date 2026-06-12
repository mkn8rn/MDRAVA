using System.Xml.Linq;

namespace MDRAVA.Tests;

internal static class ArchitectureBoundaryTests
{
    public static void ProjectReferencesPreserveLayeredDependencyDirection()
    {
        var root = FindRepositoryRoot();

        AssertReferences(
            root,
            "MDRAVA.BLL/MDRAVA.BLL.csproj",
            []);
        AssertReferences(
            root,
            "MDRAVA.INF/MDRAVA.INF.csproj",
            ["MDRAVA.BLL/MDRAVA.BLL.csproj"]);
        AssertReferences(
            root,
            "MDRAVA.API/MDRAVA.API.csproj",
            [
                "MDRAVA.BLL/MDRAVA.BLL.csproj",
                "MDRAVA.INF/MDRAVA.INF.csproj"
            ]);
    }

    private static void AssertReferences(
        string root,
        string projectPath,
        IReadOnlyList<string> expectedReferences)
    {
        var projectFile = Path.Combine(root, NormalizePath(projectPath));
        var actual = XDocument.Load(projectFile)
            .Descendants("ProjectReference")
            .Select(static element => element.Attribute("Include")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(value => NormalizeProjectReference(root, projectFile, value!))
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var expected = expectedReferences
            .Select(NormalizePath)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        AssertEx.Equal(string.Join("|", expected), string.Join("|", actual));
    }

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "MDRAVA.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not find MDRAVA.slnx from the current test process.");
    }

    private static string NormalizeProjectReference(string root, string projectFile, string include)
    {
        var projectDirectory = Path.GetDirectoryName(projectFile)
            ?? throw new InvalidOperationException($"Project path has no directory: {projectFile}");
        var fullPath = Path.GetFullPath(Path.Combine(projectDirectory, include));
        return NormalizePath(Path.GetRelativePath(root, fullPath));
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }
}

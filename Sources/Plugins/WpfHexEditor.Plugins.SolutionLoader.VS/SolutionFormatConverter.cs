// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.VS
// File: SolutionFormatConverter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-04-05
// Description:
//     Bidirectional conversion between .sln (text) and .slnx (XML) solution formats.
//     Reads via the existing loaders, then serialises to the target format.
//
// Architecture Notes:
//     Uses VsSolutionLoader / SlnxSolutionLoader as readers.
//     Writes .slnx via System.Xml.Linq, writes .sln via string builder.
//     Original file is always preserved — caller receives the new file path.
// ==========================================================

using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Plugins.SolutionLoader.VS.VsModels;

namespace WpfHexEditor.Plugins.SolutionLoader.VS;

/// <summary>
/// Converts between Visual Studio <c>.sln</c> and <c>.slnx</c> solution file formats.
/// The original file is always preserved; the new file is written alongside it.
/// </summary>
public static class SolutionFormatConverter
{
    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Converts a <c>.sln</c> file to <c>.slnx</c> (XML-based format).
    /// Writes <c>{name}.slnx</c> in the same directory.
    /// Returns the absolute path of the created <c>.slnx</c> file.
    /// </summary>
    public static async Task<string> ConvertSlnToSlnxAsync(
        string slnPath, CancellationToken ct = default)
    {
        if (!File.Exists(slnPath))
            throw new FileNotFoundException("Solution file not found.", slnPath);

        var loader   = new VsSolutionLoader();
        var solution = (VsSolution)await loader.LoadAsync(slnPath, ct).ConfigureAwait(false);

        var solutionDir = Path.GetDirectoryName(slnPath)!;
        var slnxPath    = Path.Combine(solutionDir,
            Path.GetFileNameWithoutExtension(slnPath) + ".slnx");

        var doc = BuildSlnxDocument(solution, solutionDir);
        await File.WriteAllTextAsync(slnxPath, doc.ToString(), ct).ConfigureAwait(false);

        return slnxPath;
    }

    /// <summary>
    /// Converts a <c>.slnx</c> file to <c>.sln</c> (classic text format).
    /// Writes <c>{name}.sln</c> in the same directory.
    /// Returns the absolute path of the created <c>.sln</c> file.
    /// </summary>
    public static async Task<string> ConvertSlnxToSlnAsync(
        string slnxPath, CancellationToken ct = default)
    {
        if (!File.Exists(slnxPath))
            throw new FileNotFoundException("Solution file not found.", slnxPath);

        var loader   = new SlnxSolutionLoader();
        var solution = (VsSolution)await loader.LoadAsync(slnxPath, ct).ConfigureAwait(false);

        var solutionDir = Path.GetDirectoryName(slnxPath)!;
        var slnPath     = Path.Combine(solutionDir,
            Path.GetFileNameWithoutExtension(slnxPath) + ".sln");

        var slnContent = BuildSlnContent(solution, solutionDir);
        await File.WriteAllTextAsync(slnPath, slnContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), ct)
                  .ConfigureAwait(false);

        return slnPath;
    }

    // -----------------------------------------------------------------------
    // .slnx builder
    // -----------------------------------------------------------------------

    private static XDocument BuildSlnxDocument(VsSolution solution, string solutionDir)
    {
        var root = new XElement("Solution");

        // Collect which projects are inside a solution folder.
        var folderedProjectNames = CollectFolderedProjectNames(solution.RootFolders);

        // Emit solution folders recursively.
        foreach (var folder in solution.RootFolders)
            root.Add(BuildSlnxFolder(folder, solution, solutionDir));

        // Emit root-level projects (not in any folder).
        foreach (var project in solution.Projects.OfType<VsProject>()
                     .Where(p => !folderedProjectNames.Contains(p.Name)))
        {
            var relPath = Path.GetRelativePath(solutionDir, project.ProjectFilePath)
                              .Replace('/', '\\');
            root.Add(new XElement("Project", new XAttribute("Path", relPath)));
        }

        // Emit <Properties> with active configuration.
        var config = solution.DefaultConfigurationName ?? "Debug";
        var platform = solution.DefaultPlatform ?? "Any CPU";
        root.Add(new XElement("Properties",
            new XElement("Property",
                new XAttribute("Name", "ActiveConfiguration"),
                new XAttribute("Value", $"{config}|{platform}"))));

        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
    }

    private static XElement BuildSlnxFolder(
        ISolutionFolder folder, VsSolution solution, string solutionDir)
    {
        var elem = new XElement("Folder", new XAttribute("Name", $"/{folder.Name}/"));

        // Nested folders.
        foreach (var child in folder.Children)
            elem.Add(BuildSlnxFolder(child, solution, solutionDir));

        // Projects in this folder.
        foreach (var projectName in folder.ProjectIds)
        {
            var project = solution.Projects.OfType<VsProject>()
                .FirstOrDefault(p => p.Name == projectName);
            if (project is null) continue;

            var relPath = Path.GetRelativePath(solutionDir, project.ProjectFilePath)
                              .Replace('/', '\\');
            elem.Add(new XElement("Project", new XAttribute("Path", relPath)));
        }

        // Loose file items.
        foreach (var fileItem in folder.FileItems)
            elem.Add(new XElement("File", new XAttribute("Path", fileItem)));

        return elem;
    }

    // -----------------------------------------------------------------------
    // .sln builder
    // -----------------------------------------------------------------------

    private static string BuildSlnContent(VsSolution solution, string solutionDir)
    {
        var sb = new StringBuilder();

        sb.AppendLine();
        sb.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
        sb.AppendLine("# Visual Studio Version 17");
        sb.AppendLine("VisualStudioVersion = 17.0.31903.59");
        sb.AppendLine("MinimumVisualStudioVersion = 10.0.40219.1");

        // Project entries.
        var projectGuids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in solution.Projects.OfType<VsProject>())
        {
            var typeGuid = GetProjectTypeGuid(project);
            var projGuid = EnsureGuidFormat(project.ProjectGuid);
            projectGuids[project.Name] = projGuid;

            var relPath = Path.GetRelativePath(solutionDir, project.ProjectFilePath)
                              .Replace('/', '\\');
            sb.AppendLine($"Project(\"{typeGuid}\") = \"{project.Name}\", \"{relPath}\", \"{projGuid}\"");
            sb.AppendLine("EndProject");
        }

        // Solution folder entries.
        var folderGuids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        EmitSlnFolderEntries(sb, solution.RootFolders, folderGuids);

        sb.AppendLine("Global");

        // SolutionConfigurationPlatforms.
        var config   = solution.DefaultConfigurationName ?? "Debug";
        var platform = solution.DefaultPlatform ?? "Any CPU";
        sb.AppendLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
        sb.AppendLine($"\t\tDebug|Any CPU = Debug|Any CPU");
        sb.AppendLine($"\t\tRelease|Any CPU = Release|Any CPU");
        sb.AppendLine("\tEndGlobalSection");

        // ProjectConfigurationPlatforms.
        sb.AppendLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");
        foreach (var (_, projGuid) in projectGuids)
        {
            sb.AppendLine($"\t\t{projGuid}.Debug|Any CPU.ActiveCfg = Debug|Any CPU");
            sb.AppendLine($"\t\t{projGuid}.Debug|Any CPU.Build.0 = Debug|Any CPU");
            sb.AppendLine($"\t\t{projGuid}.Release|Any CPU.ActiveCfg = Release|Any CPU");
            sb.AppendLine($"\t\t{projGuid}.Release|Any CPU.Build.0 = Release|Any CPU");
        }
        sb.AppendLine("\tEndGlobalSection");

        // NestedProjects.
        var nestedLines = BuildNestedProjectsLines(solution.RootFolders, projectGuids, folderGuids);
        if (nestedLines.Count > 0)
        {
            sb.AppendLine("\tGlobalSection(NestedProjects) = preSolution");
            foreach (var line in nestedLines)
                sb.AppendLine(line);
            sb.AppendLine("\tEndGlobalSection");
        }

        sb.AppendLine("EndGlobal");
        return sb.ToString();
    }

    private static void EmitSlnFolderEntries(
        StringBuilder sb,
        IReadOnlyList<ISolutionFolder> folders,
        Dictionary<string, string> folderGuids)
    {
        const string folderTypeGuid = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";

        foreach (var folder in folders)
        {
            var guid = EnsureGuidFormat(
                GenerateDeterministicGuid(folder.Name + "|folder"));
            folderGuids[folder.Name] = guid;

            sb.AppendLine($"Project(\"{folderTypeGuid}\") = \"{folder.Name}\", \"{folder.Name}\", \"{guid}\"");

            if (folder.FileItems.Count > 0)
            {
                sb.AppendLine("\tProjectSection(SolutionItems) = preProject");
                foreach (var file in folder.FileItems)
                    sb.AppendLine($"\t\t{file} = {file}");
                sb.AppendLine("\tEndProjectSection");
            }

            sb.AppendLine("EndProject");

            // Recurse into children.
            EmitSlnFolderEntries(sb, folder.Children, folderGuids);
        }
    }

    private static List<string> BuildNestedProjectsLines(
        IReadOnlyList<ISolutionFolder> folders,
        Dictionary<string, string> projectGuids,
        Dictionary<string, string> folderGuids)
    {
        var lines = new List<string>();
        BuildNestedCore(folders, projectGuids, folderGuids, lines);
        return lines;
    }

    private static void BuildNestedCore(
        IReadOnlyList<ISolutionFolder> folders,
        Dictionary<string, string> projectGuids,
        Dictionary<string, string> folderGuids,
        List<string> lines)
    {
        foreach (var folder in folders)
        {
            if (!folderGuids.TryGetValue(folder.Name, out var folderGuid)) continue;

            foreach (var projectName in folder.ProjectIds)
            {
                if (projectGuids.TryGetValue(projectName, out var projGuid))
                    lines.Add($"\t\t{projGuid} = {folderGuid}");
            }

            foreach (var child in folder.Children)
            {
                if (folderGuids.TryGetValue(child.Name, out var childGuid))
                    lines.Add($"\t\t{childGuid} = {folderGuid}");
            }

            BuildNestedCore(folder.Children, projectGuids, folderGuids, lines);
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static HashSet<string> CollectFolderedProjectNames(IReadOnlyList<ISolutionFolder> folders)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in folders)
        {
            foreach (var name in folder.ProjectIds) set.Add(name);
            foreach (var name in CollectFolderedProjectNames(folder.Children)) set.Add(name);
        }
        return set;
    }

    private static string GetProjectTypeGuid(VsProject project)
    {
        return project.Language?.ToLowerInvariant() switch
        {
            "f#"   => "{F2A71F9B-5D33-465A-A702-920D77279786}",
            "vb"   => "{F184B08F-C81C-45F6-A57F-5ABD9991F28F}",
            _      => "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}", // C#
        };
    }

    private static string EnsureGuidFormat(string? guid)
    {
        if (string.IsNullOrWhiteSpace(guid))
            guid = GenerateDeterministicGuid(Guid.NewGuid().ToString());

        if (!guid.StartsWith('{')) guid = "{" + guid;
        if (!guid.EndsWith('}'))  guid += "}";
        return guid.ToUpperInvariant();
    }

    private static string GenerateDeterministicGuid(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input.ToLowerInvariant()));
        return new Guid(hash.AsSpan(0, 16)).ToString("B").ToUpperInvariant();
    }
}

// ==========================================================
// Project: WpfHexEditor.App
// File: MainWindow.SolutionConvert.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-04-05
// Description:
//     Handles bidirectional .sln ↔ .slnx solution format conversion via
//     the ISolutionLoader extension registry + CommandPalette commands.
// ==========================================================

using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Xml.Linq;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.App;

public partial class MainWindow
{
    /// <summary>
    /// Converts the currently open VS solution file between .sln and .slnx formats.
    /// The original file is preserved; the converted file is written alongside it.
    /// Prompts the user to open the converted solution after conversion.
    /// </summary>
    private async Task OnConvertSolutionFormatAsync(bool toSlnx)
    {
        var currentPath = _solutionManager.CurrentSolution?.FilePath;
        if (currentPath is null)
        {
            MessageBox.Show(this,
                "No solution is currently open.",
                "Convert Solution Format",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var ext = Path.GetExtension(currentPath).ToLowerInvariant();

        if (toSlnx && ext != ".sln")
        {
            MessageBox.Show(this,
                "The current solution is not a .sln file.",
                "Convert Solution Format",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!toSlnx && ext != ".slnx")
        {
            MessageBox.Show(this,
                "The current solution is not a .slnx file.",
                "Convert Solution Format",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var targetExt  = toSlnx ? ".slnx" : ".sln";
        var sourceName = Path.GetFileName(currentPath);
        var targetName = Path.GetFileNameWithoutExtension(currentPath) + targetExt;

        var confirm = MessageBox.Show(this,
            $"Convert \"{sourceName}\" → \"{targetName}\"?\n\nThe original file will be preserved.",
            "Convert Solution Format",
            MessageBoxButton.OKCancel, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.OK) return;

        // Find a loader for the target format.
        var loaders = _ideHostContext?.ExtensionRegistry.GetExtensions<ISolutionLoader>() ?? [];
        var targetExt2 = targetExt.TrimStart('.');
        var loader = loaders.FirstOrDefault(l =>
            l.SupportedExtensions.Contains(targetExt2, StringComparer.OrdinalIgnoreCase));

        if (loader is null)
        {
            MessageBox.Show(this,
                $"No loader registered for {targetExt} format. Make sure the VS Solution Loader plugin is active.",
                "Convert Solution Format",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            // Load the source solution using its appropriate loader.
            var sourceExt    = ext.TrimStart('.');
            var sourceLoader = loaders.FirstOrDefault(l =>
                l.SupportedExtensions.Contains(sourceExt, StringComparer.OrdinalIgnoreCase));
            if (sourceLoader is null) throw new InvalidOperationException($"No loader for {ext}.");

            var solution = await sourceLoader.LoadAsync(currentPath);

            var solutionDir = Path.GetDirectoryName(currentPath)!;
            var newPath = Path.Combine(solutionDir, targetName);

            if (toSlnx)
                WriteSlnx(solution, solutionDir, newPath);
            else
                WriteSln(solution, solutionDir, newPath);


            OutputLogger.Info($"[Convert] Solution converted: {newPath}");

            var openResult = MessageBox.Show(this,
                $"Converted to \"{targetName}\".\n\nOpen the converted solution now?",
                "Convert Solution Format",
                MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (openResult == MessageBoxResult.Yes)
                await OpenSolutionAsync(newPath);
        }
        catch (Exception ex)
        {
            OutputLogger.Error($"[Convert] Conversion failed: {ex.Message}");
            MessageBox.Show(this,
                $"Conversion failed:\n{ex.Message}",
                "Convert Solution Format",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // -----------------------------------------------------------------------
    // .slnx writer
    // -----------------------------------------------------------------------

    private static void WriteSlnx(ISolution solution, string solutionDir, string destPath)
    {
        var root = new XElement("Solution");

        var folderedNames = CollectFolderedProjectNames(solution.RootFolders);

        foreach (var folder in solution.RootFolders)
            root.Add(BuildSlnxFolder(folder, solution, solutionDir));

        foreach (var project in solution.Projects.Where(p => !folderedNames.Contains(p.Name)))
        {
            var relPath = Path.GetRelativePath(solutionDir, project.ProjectFilePath)
                             .Replace('/', '\\');
            root.Add(new XElement("Project", new XAttribute("Path", relPath)));
        }

        root.Add(new XElement("Properties",
            new XElement("Property",
                new XAttribute("Name", "ActiveConfiguration"),
                new XAttribute("Value", "Debug|Any CPU"))));

        new XDocument(new XDeclaration("1.0", "utf-8", null), root)
            .Save(destPath);
    }

    private static XElement BuildSlnxFolder(ISolutionFolder folder, ISolution solution, string solutionDir)
    {
        var elem = new XElement("Folder", new XAttribute("Name", $"/{folder.Name}/"));

        foreach (var child in folder.Children)
            elem.Add(BuildSlnxFolder(child, solution, solutionDir));

        foreach (var projectName in folder.ProjectIds)
        {
            var project = solution.Projects.FirstOrDefault(p => p.Name == projectName);
            if (project is null) continue;
            var relPath = Path.GetRelativePath(solutionDir, project.ProjectFilePath)
                             .Replace('/', '\\');
            elem.Add(new XElement("Project", new XAttribute("Path", relPath)));
        }

        foreach (var fileItem in folder.FileItems)
            elem.Add(new XElement("File", new XAttribute("Path", fileItem)));

        return elem;
    }

    // -----------------------------------------------------------------------
    // .sln writer
    // -----------------------------------------------------------------------

    private static void WriteSln(ISolution solution, string solutionDir, string destPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
        sb.AppendLine("# Visual Studio Version 17");
        sb.AppendLine("VisualStudioVersion = 17.0.31903.59");
        sb.AppendLine("MinimumVisualStudioVersion = 10.0.40219.1");

        var projectGuids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in solution.Projects)
        {
            var typeGuid = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
            var projGuid = $"{{{GuidFromString(project.ProjectFilePath)}}}".ToUpperInvariant();
            projectGuids[project.Name] = projGuid;

            var relPath = Path.GetRelativePath(solutionDir, project.ProjectFilePath)
                             .Replace('/', '\\');
            sb.AppendLine($"Project(\"{typeGuid}\") = \"{project.Name}\", \"{relPath}\", \"{projGuid}\"");
            sb.AppendLine("EndProject");
        }

        var folderGuids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        EmitSlnFolderEntries(sb, solution.RootFolders, folderGuids);

        sb.AppendLine("Global");
        sb.AppendLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
        sb.AppendLine("\t\tDebug|Any CPU = Debug|Any CPU");
        sb.AppendLine("\t\tRelease|Any CPU = Release|Any CPU");
        sb.AppendLine("\tEndGlobalSection");
        sb.AppendLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");
        foreach (var (_, guid) in projectGuids)
        {
            sb.AppendLine($"\t\t{guid}.Debug|Any CPU.ActiveCfg = Debug|Any CPU");
            sb.AppendLine($"\t\t{guid}.Debug|Any CPU.Build.0 = Debug|Any CPU");
            sb.AppendLine($"\t\t{guid}.Release|Any CPU.ActiveCfg = Release|Any CPU");
            sb.AppendLine($"\t\t{guid}.Release|Any CPU.Build.0 = Release|Any CPU");
        }
        sb.AppendLine("\tEndGlobalSection");

        var nested = BuildNestedProjectsLines(solution.RootFolders, projectGuids, folderGuids);
        if (nested.Count > 0)
        {
            sb.AppendLine("\tGlobalSection(NestedProjects) = preSolution");
            foreach (var line in nested) sb.AppendLine(line);
            sb.AppendLine("\tEndGlobalSection");
        }

        sb.AppendLine("EndGlobal");

        File.WriteAllText(destPath, sb.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static void EmitSlnFolderEntries(
        StringBuilder sb,
        IReadOnlyList<ISolutionFolder> folders,
        Dictionary<string, string> folderGuids)
    {
        const string fType = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";
        foreach (var folder in folders)
        {
            var guid = $"{{{GuidFromString(folder.Name + "|folder")}}}".ToUpperInvariant();
            folderGuids[folder.Name] = guid;
            sb.AppendLine($"Project(\"{fType}\") = \"{folder.Name}\", \"{folder.Name}\", \"{guid}\"");
            if (folder.FileItems.Count > 0)
            {
                sb.AppendLine("\tProjectSection(SolutionItems) = preProject");
                foreach (var f in folder.FileItems)
                    sb.AppendLine($"\t\t{f} = {f}");
                sb.AppendLine("\tEndProjectSection");
            }
            sb.AppendLine("EndProject");
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
            foreach (var pname in folder.ProjectIds)
                if (projectGuids.TryGetValue(pname, out var pg))
                    lines.Add($"\t\t{pg} = {folderGuid}");
            foreach (var child in folder.Children)
                if (folderGuids.TryGetValue(child.Name, out var cg))
                    lines.Add($"\t\t{cg} = {folderGuid}");
            BuildNestedCore(folder.Children, projectGuids, folderGuids, lines);
        }
    }

    private static HashSet<string> CollectFolderedProjectNames(IReadOnlyList<ISolutionFolder> folders)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in folders)
        {
            foreach (var n in f.ProjectIds) set.Add(n);
            foreach (var n in CollectFolderedProjectNames(f.Children)) set.Add(n);
        }
        return set;
    }

    private static string GuidFromString(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input.ToLowerInvariant()));
        return new Guid(hash.AsSpan(0, 16)).ToString().ToUpperInvariant();
    }
}

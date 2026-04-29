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
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Xml.Linq;
using WpfHexEditor.App.Properties;
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
                AppResources.App_Convert_NoSolution,
                AppResources.App_Convert_Title,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var ext = Path.GetExtension(currentPath).ToLowerInvariant();

        if (toSlnx && ext != ".sln")
        {
            MessageBox.Show(this,
                AppResources.App_Convert_NotSln,
                AppResources.App_Convert_Title,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!toSlnx && ext != ".slnx")
        {
            MessageBox.Show(this,
                AppResources.App_Convert_NotSlnx,
                AppResources.App_Convert_Title,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var targetExt  = toSlnx ? ".slnx" : ".sln";
        var sourceName = Path.GetFileName(currentPath);
        var targetName = Path.GetFileNameWithoutExtension(currentPath) + targetExt;

        var confirm = MessageBox.Show(this,
            string.Format(AppResources.App_Convert_Confirm, sourceName, targetName, Environment.NewLine),
            AppResources.App_Convert_Title,
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
                string.Format(AppResources.App_Convert_NoLoader, targetExt),
                AppResources.App_Convert_Title,
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
                string.Format(AppResources.App_Convert_Success, targetName, Environment.NewLine),
                AppResources.App_Convert_Title,
                MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (openResult == MessageBoxResult.Yes)
                await OpenSolutionAsync(newPath);
        }
        catch (Exception ex)
        {
            OutputLogger.Error($"[Convert] Conversion failed: {ex.Message}");
            MessageBox.Show(this,
                string.Format(AppResources.App_Convert_Failed, Environment.NewLine, ex.Message),
                AppResources.App_Convert_Title,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // -----------------------------------------------------------------------
    // .whsln writer (native WH format)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Converts the currently open solution to the native .whsln format.
    /// Produces a JSON file compatible with SolutionSerializer v2.
    /// The original file is preserved.
    /// </summary>
    private async Task OnConvertToWhslnAsync()
    {
        var currentPath = _solutionManager.CurrentSolution?.FilePath;
        if (currentPath is null)
        {
            MessageBox.Show(this,
                AppResources.App_Convert_NoSolution,
                AppResources.App_Convert_Title,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var ext = Path.GetExtension(currentPath).ToLowerInvariant();
        if (ext == ".whsln")
        {
            MessageBox.Show(this,
                AppResources.App_Convert_AlreadyWhsln,
                AppResources.App_Convert_Title,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var sourceName = Path.GetFileName(currentPath);
        var targetName = Path.GetFileNameWithoutExtension(currentPath) + ".whsln";
        var solutionDir = Path.GetDirectoryName(currentPath)!;
        var newPath = Path.Combine(solutionDir, targetName);

        var confirm = MessageBox.Show(this,
            string.Format(AppResources.App_Convert_Confirm, sourceName, targetName, Environment.NewLine),
            AppResources.App_Convert_Title,
            MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        var loaders = _ideHostContext?.ExtensionRegistry.GetExtensions<ISolutionLoader>() ?? [];
        var srcExt = ext.TrimStart('.');
        var sourceLoader = loaders.FirstOrDefault(l =>
            l.SupportedExtensions.Contains(srcExt, StringComparer.OrdinalIgnoreCase));

        if (sourceLoader is null)
        {
            MessageBox.Show(this,
                string.Format(AppResources.App_Convert_NoLoaderSimple, ext),
                AppResources.App_Convert_Title,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var solution = await sourceLoader.LoadAsync(currentPath);
            await WriteWhslnAsync(solution, solutionDir, newPath);

            OutputLogger.Info($"[Convert] Solution converted: {newPath}");

            var openResult = MessageBox.Show(this,
                string.Format(AppResources.App_Convert_Success, targetName, Environment.NewLine),
                AppResources.App_Convert_Title,
                MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (openResult == MessageBoxResult.Yes)
                await OpenSolutionAsync(newPath);
        }
        catch (Exception ex)
        {
            OutputLogger.Error($"[Convert] Conversion failed: {ex.Message}");
            MessageBox.Show(this,
                string.Format(AppResources.App_Convert_Failed, Environment.NewLine, ex.Message),
                AppResources.App_Convert_Title,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Writes a .whsln JSON file (format version 2) from an <see cref="ISolution"/>.
    /// Builds the JSON directly so no dependency on internal SolutionSerializer types is needed.
    /// </summary>
    private static async Task WriteWhslnAsync(ISolution solution, string solutionDir, string destPath)
    {
        var projects = new JsonArray();
        foreach (var project in solution.Projects)
        {
            var relPath = Path.GetRelativePath(solutionDir, project.ProjectFilePath)
                             .Replace('\\', '/');
            var folderId = FindProjectFolderId(solution.RootFolders, project.Name);
            var obj = new JsonObject
            {
                ["name"] = project.Name,
                ["path"] = relPath,
            };
            if (folderId is not null)
                obj["solutionFolderId"] = folderId;
            projects.Add(obj);
        }

        var root = new JsonObject
        {
            ["version"]  = 2,
            ["name"]     = solution.Name,
            ["modified"] = DateTimeOffset.UtcNow.ToString("O"),
            ["projects"] = projects,
        };

        if (solution.RootFolders.Count > 0)
            root["solutionFolders"] = BuildWhslnFolderArray(solution.RootFolders);

        if (solution.StartupProject is not null)
            root["startupProject"] = solution.StartupProject.Name;

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        Directory.CreateDirectory(solutionDir);
        await using var stream = File.Create(destPath);
        await JsonSerializer.SerializeAsync(stream, root, jsonOptions);
    }

    private static JsonArray BuildWhslnFolderArray(IReadOnlyList<ISolutionFolder> folders)
    {
        var arr = new JsonArray();
        foreach (var folder in folders)
        {
            var obj = new JsonObject
            {
                ["id"]   = folder.Id,
                ["name"] = folder.Name,
            };
            if (folder.ProjectIds.Count > 0)
            {
                var ids = new JsonArray();
                foreach (var id in folder.ProjectIds) ids.Add(id);
                obj["projectIds"] = ids;
            }
            if (folder.Children.Count > 0)
                obj["children"] = BuildWhslnFolderArray(folder.Children);
            if (folder.FileItems.Count > 0)
            {
                var files = new JsonArray();
                foreach (var f in folder.FileItems) files.Add(f);
                obj["fileItems"] = files;
            }
            arr.Add(obj);
        }
        return arr;
    }

    private static string? FindProjectFolderId(IReadOnlyList<ISolutionFolder> folders, string projectName)
    {
        foreach (var folder in folders)
        {
            if (folder.ProjectIds.Contains(projectName, StringComparer.OrdinalIgnoreCase))
                return folder.Id;
            var child = FindProjectFolderId(folder.Children, projectName);
            if (child is not null) return child;
        }
        return null;
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

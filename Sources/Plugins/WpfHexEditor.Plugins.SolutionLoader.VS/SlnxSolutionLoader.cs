// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.VS
// File: SlnxSolutionLoader.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-04-05
// Description:
//     ISolutionLoader implementation for Visual Studio .slnx files
//     (XML-based solution format introduced in VS 2022 17.10+).
//     Parses the XML structure, resolves project references via
//     VSProjectParser, and maps the result to ISolution / IProject.
//
// Architecture Notes:
//     Pattern: Adapter — converts VS .slnx XML model → WpfHexEditor ISolution
//     Reuses VsModels (VsSolution, VsProject) and VSProjectParser from the .sln loader.
//     Supports nested <Folder> elements, <File> items, and <Properties> for configuration.
// ==========================================================

using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Plugins.SolutionLoader.VS.VsModels;

namespace WpfHexEditor.Plugins.SolutionLoader.VS;

/// <summary>
/// Loads a Visual Studio <c>.slnx</c> file (XML-based solution format) and
/// converts it to an <see cref="ISolution"/> in-memory model.
/// </summary>
public sealed class SlnxSolutionLoader : ISolutionLoader
{
    // -----------------------------------------------------------------------
    // ISolutionLoader
    // -----------------------------------------------------------------------

    public string LoaderName => "Visual Studio (slnx)";

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedExtensions { get; } = ["slnx"];

    /// <inheritdoc />
    public bool CanLoad(string filePath)
    {
        var ext = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
        return ext == "slnx";
    }

    /// <inheritdoc />
    public async Task<ISolution> LoadAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Solution file not found.", filePath);

        var xml = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        var doc = XDocument.Parse(xml);
        var root = doc.Root ?? throw new InvalidDataException($"Invalid .slnx file: {filePath}");

        var solutionDir = Path.GetDirectoryName(filePath)!;

        // ---- Collect all project paths and folder structure ------------------
        var projectPaths = new List<(string AbsolutePath, string? FolderName)>();
        var rootFolders = new List<SlnxFolder>();

        foreach (var element in root.Elements())
        {
            if (element.Name.LocalName == "Project")
            {
                var path = ResolveProjectPath(element, solutionDir);
                if (path is not null)
                    projectPaths.Add((path, null));
            }
            else if (element.Name.LocalName == "Folder")
            {
                var folder = ParseFolder(element, solutionDir, projectPaths);
                rootFolders.Add(folder);
            }
        }

        // ---- Parse configuration from <Properties> --------------------------
        var (defaultConfig, defaultPlatform) = ParseProperties(root);

        // ---- Load project files concurrently --------------------------------
        var projectTasks = projectPaths
            .Select(p => LoadProjectAsync(p.AbsolutePath, ct))
            .ToList();

        await Task.WhenAll(projectTasks).ConfigureAwait(false);

        var projects = projectTasks
            .Select(t => t.Result)
            .Where(p => p is not null)
            .Cast<VsProject>()
            .ToList();

        // ---- Build solution folder hierarchy --------------------------------
        var solutionFolders = rootFolders
            .Select(f => BuildSolutionFolder(f, projects))
            .ToList();

        // ---- Determine startup project --------------------------------------
        var startupProject = await ResolveStartupProjectAsync(filePath, projects, ct)
                                   .ConfigureAwait(false);

        var solution = new VsSolution
        {
            Name                     = Path.GetFileNameWithoutExtension(filePath),
            FilePath                 = filePath,
            Projects                 = projects,
            RootFolders              = solutionFolders.Cast<ISolutionFolder>().ToList(),
            DefaultConfigurationName = defaultConfig,
            DefaultPlatform          = defaultPlatform,
        };
        solution.InitStartupProject(startupProject);
        return solution;
    }

    // -----------------------------------------------------------------------
    // XML parsing helpers
    // -----------------------------------------------------------------------

    private static string? ResolveProjectPath(XElement projectElement, string solutionDir)
    {
        var relativePath = projectElement.Attribute("Path")?.Value;
        if (string.IsNullOrWhiteSpace(relativePath)) return null;

        return Path.GetFullPath(Path.Combine(solutionDir, relativePath.Replace('\\', '/')));
    }

    private static SlnxFolder ParseFolder(
        XElement folderElement, string solutionDir,
        List<(string AbsolutePath, string? FolderName)> allProjects)
    {
        var name = folderElement.Attribute("Name")?.Value ?? "Unnamed";
        var folder = new SlnxFolder(name);

        foreach (var child in folderElement.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "Project":
                    var path = ResolveProjectPath(child, solutionDir);
                    if (path is not null)
                    {
                        folder.ProjectPaths.Add(path);
                        allProjects.Add((path, name));
                    }
                    break;

                case "File":
                    var filePath = child.Attribute("Path")?.Value;
                    if (!string.IsNullOrWhiteSpace(filePath))
                        folder.FileItems.Add(filePath);
                    break;

                case "Folder":
                    folder.Children.Add(ParseFolder(child, solutionDir, allProjects));
                    break;
            }
        }

        return folder;
    }

    private static (string? Config, string? Platform) ParseProperties(XElement root)
    {
        var properties = root.Element("Properties");
        if (properties is null) return (null, null);

        foreach (var prop in properties.Elements("Property"))
        {
            var name = prop.Attribute("Name")?.Value;
            if (name is "ActiveConfiguration" or "DefaultConfiguration")
            {
                var value = prop.Attribute("Value")?.Value;
                if (value is not null && value.Contains('|'))
                {
                    var parts = value.Split('|', 2);
                    return (parts[0].Trim(), parts[1].Trim());
                }
            }
        }

        return (null, null);
    }

    // -----------------------------------------------------------------------
    // Project loading
    // -----------------------------------------------------------------------

    private static async Task<VsProject?> LoadProjectAsync(string absolutePath, CancellationToken ct)
    {
        var ext = Path.GetExtension(absolutePath).ToLowerInvariant();
        if (ext is not ".csproj" and not ".vbproj" and not ".fsproj" and not ".shproj")
            return null;

        if (!File.Exists(absolutePath)) return null;

        return await Task.Run(() =>
        {
            try
            {
                var parsed = VSProjectParser.Parse(absolutePath);
                return new VsProject
                {
                    Id               = parsed.Id,
                    Name             = parsed.Name,
                    ProjectFilePath  = parsed.ProjectFilePath,
                    Items            = parsed.Items,
                    RootFolders      = parsed.RootFolders,
                    ProjectType      = parsed.ProjectType,
                    TargetFramework  = parsed.TargetFramework,
                    Language         = parsed.Language,
                    OutputType       = parsed.OutputType,
                    AssemblyName     = parsed.AssemblyName,
                    RootNamespace    = parsed.RootNamespace,
                    ProjectGuid      = GenerateDeterministicGuid(absolutePath),
                    ProjectReferences  = parsed.ProjectReferences,
                    PackageReferences  = parsed.PackageReferences,
                    AssemblyReferences = parsed.AssemblyReferences,
                    AnalyzerReferences = parsed.AnalyzerReferences,
                };
            }
            catch
            {
                return new VsProject
                {
                    Name            = Path.GetFileNameWithoutExtension(absolutePath),
                    ProjectFilePath = absolutePath,
                    ProjectGuid     = GenerateDeterministicGuid(absolutePath),
                    ProjectType     = ext.TrimStart('.'),
                };
            }
        }, ct).ConfigureAwait(false);
    }

    // -----------------------------------------------------------------------
    // Solution folder hierarchy
    // -----------------------------------------------------------------------

    private static ISolutionFolder BuildSolutionFolder(SlnxFolder folder, List<VsProject> allProjects)
    {
        var sf = new SlnxSolutionFolder(
            GenerateDeterministicGuid(folder.Name),
            folder.Name);

        // Nested folders (recursive).
        foreach (var child in folder.Children)
            sf.AddChild(BuildSolutionFolder(child, allProjects));

        // Projects in this folder — match by absolute path.
        foreach (var projectPath in folder.ProjectPaths)
        {
            var project = allProjects.FirstOrDefault(p =>
                p.ProjectFilePath.Equals(projectPath, StringComparison.OrdinalIgnoreCase));
            if (project is not null)
                sf.AddProjectId(project.Name);
        }

        // Loose file items.
        foreach (var file in folder.FileItems)
            sf.AddFileItem(file);

        return sf;
    }

    // -----------------------------------------------------------------------
    // Startup project
    // -----------------------------------------------------------------------

    private static async Task<VsProject?> ResolveStartupProjectAsync(
        string filePath, IReadOnlyList<VsProject> projects, CancellationToken ct)
    {
        // Check .slnx.user sidecar (same format as .sln.user — JSON with startupProjectPath).
        var sidecarPath = filePath + ".user";
        if (File.Exists(sidecarPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(sidecarPath, ct).ConfigureAwait(false);
                var doc  = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("startupProjectPath", out var prop))
                {
                    var relPath = prop.GetString();
                    if (!string.IsNullOrWhiteSpace(relPath))
                    {
                        var solutionDir = Path.GetDirectoryName(filePath)!;
                        var absPath     = Path.GetFullPath(Path.Combine(solutionDir, relPath));
                        var match = projects.FirstOrDefault(p =>
                            p.ProjectFilePath.Equals(absPath, StringComparison.OrdinalIgnoreCase));
                        if (match is not null) return match;
                    }
                }
            }
            catch { /* Corrupt sidecar — fall through to heuristic. */ }
        }

        // Heuristic — reuse the .sln loader's logic.
        return VsSolutionLoader.DetermineStartupProject(projects);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Generates a deterministic GUID from a string (path or name) using SHA256.
    /// .slnx files do not contain project GUIDs, so we derive stable ones.
    /// </summary>
    private static string GenerateDeterministicGuid(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input.ToLowerInvariant()));
        // Use first 16 bytes to form a GUID.
        var guid = new Guid(hash.AsSpan(0, 16));
        return guid.ToString("B").ToUpperInvariant();
    }

    // -----------------------------------------------------------------------
    // Internal models
    // -----------------------------------------------------------------------

    private sealed class SlnxFolder(string name)
    {
        public string Name { get; } = name;
        public List<string> ProjectPaths { get; } = [];
        public List<string> FileItems { get; } = [];
        public List<SlnxFolder> Children { get; } = [];
    }

    private sealed class SlnxSolutionFolder : ISolutionFolder
    {
        private readonly List<string>           _projectIds = [];
        private readonly List<ISolutionFolder>  _children   = [];
        private readonly List<string>           _fileItems  = [];

        public string Id   { get; }
        public string Name { get; }

        public IReadOnlyList<string>          ProjectIds => _projectIds;
        public IReadOnlyList<ISolutionFolder> Children   => _children;
        public IReadOnlyList<string>          FileItems  => _fileItems;

        internal SlnxSolutionFolder(string id, string name) { Id = id; Name = name; }

        internal void AddProjectId(string id) => _projectIds.Add(id);
        internal void AddChild(ISolutionFolder child) => _children.Add(child);
        internal void AddFileItem(string relativePath) => _fileItems.Add(relativePath);
    }
}

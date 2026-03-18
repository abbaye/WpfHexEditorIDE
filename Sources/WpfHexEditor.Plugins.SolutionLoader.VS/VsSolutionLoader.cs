// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.VS
// File: VsSolutionLoader.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     ISolutionLoader implementation for Visual Studio .sln files.
//     Parses the SLN text format, resolves project references via
//     VSProjectParser, and maps the result to ISolution / IProject.
//
// Architecture Notes:
//     - Pattern: Adapter — converts VS solution model → WpfHexEditor ISolution
//     - Handles: nested solution folders, startup project, default config/platform
//     - Supports: SDK-style and legacy .csproj / .vbproj
// ==========================================================

using System.Text.RegularExpressions;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Plugins.SolutionLoader.VS.VsModels;

namespace WpfHexEditor.Plugins.SolutionLoader.VS;

/// <summary>
/// Loads a Visual Studio <c>.sln</c> file and converts it to an
/// <see cref="ISolution"/> in-memory model.
/// </summary>
public sealed class VsSolutionLoader : ISolutionLoader
{
    // -----------------------------------------------------------------------
    // Constants
    // -----------------------------------------------------------------------

    /// <summary>GUID for VS solution folder pseudo-projects.</summary>
    private const string SolutionFolderTypeGuid = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";

    // Pre-compiled patterns for .sln parsing.
    private static readonly Regex ProjectLineRegex = new(
        @"^Project\(""\{(?<typeGuid>[^}]+)\}""\)\s*=\s*""(?<name>[^""]+)""\s*,\s*""(?<path>[^""]+)""\s*,\s*""\{(?<guid>[^}]+)\}""",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex ConfigPlatformRegex = new(
        @"^\s*(?<guid>\{[^}]+\})\.(?<config>[^|]+)\|(?<platform>[^.]+)\.ActiveCfg\s*=\s*(?<activeCfg>.+)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex NestedProjectRegex = new(
        @"^\s*\{(?<child>[^}]+)\}\s*=\s*\{(?<parent>[^}]+)\}",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex GlobalSectionRegex = new(
        @"GlobalSection\(SolutionConfigurationPlatforms\).*?EndGlobalSection",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex SolutionConfigRegex = new(
        @"^\s*(?<cfg>[^|]+)\|(?<platform>.+)\s*=",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // -----------------------------------------------------------------------
    // ISolutionLoader
    // -----------------------------------------------------------------------

    public string LoaderName => "Visual Studio";

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedExtensions { get; } = ["sln", "csproj", "vbproj", "fsproj"];

    /// <inheritdoc />
    public bool CanLoad(string filePath)
    {
        var ext = System.IO.Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
        return SupportedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<ISolution> LoadAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Solution file not found.", filePath);

        var content = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        var solutionDir = System.IO.Path.GetDirectoryName(filePath)!;

        // ---- Parse raw entries -----------------------------------------------
        var rawEntries = ParseProjectEntries(content, solutionDir);
        var nestedMap  = ParseNestedProjects(content);           // child GUID → parent GUID
        var (defaultConfig, defaultPlatform) = ParseDefaultConfig(content);

        // ---- Separate projects from solution folders -------------------------
        var solutionFolderEntries = rawEntries
            .Where(e => e.TypeGuid.Equals(SolutionFolderTypeGuid, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(e => e.Guid, StringComparer.OrdinalIgnoreCase);

        var projectEntries = rawEntries
            .Where(e => !e.TypeGuid.Equals(SolutionFolderTypeGuid, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // ---- Load project files (I/O bound — run concurrently) ---------------
        var projectTasks = projectEntries
            .Select(e => LoadProjectAsync(e, ct))
            .ToList();

        await Task.WhenAll(projectTasks).ConfigureAwait(false);

        var projects = projectTasks
            .Select(t => t.Result)
            .Where(p => p != null)
            .Cast<VsProject>()
            .ToList();

        // ---- Build solution folder hierarchy ---------------------------------
        var rootFolders = BuildSolutionFolders(solutionFolderEntries, nestedMap, projects);

        // ---- Determine startup project ---------------------------------------
        // Priority: 1) user sidecar (.sln.user)  2) heuristic (Sample/Test exclusion)
        var startupProject = await ResolveStartupProjectAsync(filePath, projects, content, ct)
                                   .ConfigureAwait(false);

        var solution = new VsSolution
        {
            Name                     = System.IO.Path.GetFileNameWithoutExtension(filePath),
            FilePath                 = filePath,
            Projects                 = projects,
            RootFolders              = rootFolders,
            DefaultConfigurationName = defaultConfig,
            DefaultPlatform          = defaultPlatform,
        };
        solution.InitStartupProject(startupProject);
        return solution;
    }

    /// <summary>
    /// Returns the startup project for a VS solution.
    /// Checks the per-user sidecar (<c>.sln.user</c>) first; falls back to the
    /// heuristic when no user preference has been saved.
    /// </summary>
    private static async Task<VsProject?> ResolveStartupProjectAsync(
        string                filePath,
        IReadOnlyList<VsProject> projects,
        string                content,
        CancellationToken     ct)
    {
        // Try to read user preference from sidecar file.
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
                        var solutionDir = System.IO.Path.GetDirectoryName(filePath)!;
                        var absPath     = System.IO.Path.GetFullPath(
                                              System.IO.Path.Combine(solutionDir, relPath));
                        var match = projects.FirstOrDefault(p =>
                            p.ProjectFilePath.Equals(absPath, StringComparison.OrdinalIgnoreCase));
                        if (match is not null) return match;
                    }
                }
            }
            catch
            {
                // Corrupt sidecar — fall through to heuristic.
            }
        }

        return DetermineStartupProject(projects, content);
    }

    // -----------------------------------------------------------------------
    // Parsing helpers
    // -----------------------------------------------------------------------

    private static List<SlnProjectEntry> ParseProjectEntries(string content, string solutionDir)
    {
        var entries = new List<SlnProjectEntry>();

        foreach (Match m in ProjectLineRegex.Matches(content))
        {
            var relativePath = m.Groups["path"].Value.Trim();
            var absolutePath = System.IO.Path.IsPathRooted(relativePath)
                ? relativePath
                : System.IO.Path.GetFullPath(System.IO.Path.Combine(solutionDir, relativePath));

            entries.Add(new SlnProjectEntry(
                TypeGuid: $"{{{m.Groups["typeGuid"].Value}}}",
                Name    : m.Groups["name"].Value,
                Path    : absolutePath,
                Guid    : m.Groups["guid"].Value.ToUpperInvariant()));
        }

        return entries;
    }

    private static Dictionary<string, string> ParseNestedProjects(string content)
    {
        // Find NestedProjects section.
        var start = content.IndexOf("GlobalSection(NestedProjects)", StringComparison.OrdinalIgnoreCase);
        if (start < 0) return [];

        var end = content.IndexOf("EndGlobalSection", start, StringComparison.OrdinalIgnoreCase);
        if (end < 0) return [];

        var section = content[start..end];
        var map     = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match m in NestedProjectRegex.Matches(section))
            map[m.Groups["child"].Value.ToUpperInvariant()] = m.Groups["parent"].Value.ToUpperInvariant();

        return map;
    }

    private static (string? Config, string? Platform) ParseDefaultConfig(string content)
    {
        var sectionMatch = GlobalSectionRegex.Match(content);
        if (!sectionMatch.Success) return (null, null);

        var firstConfig = SolutionConfigRegex.Match(sectionMatch.Value);
        if (!firstConfig.Success) return (null, null);

        return (firstConfig.Groups["cfg"].Value.Trim(), firstConfig.Groups["platform"].Value.Trim());
    }

    // -----------------------------------------------------------------------
    // Project loading
    // -----------------------------------------------------------------------

    private static async Task<VsProject?> LoadProjectAsync(
        SlnProjectEntry entry, CancellationToken ct)
    {
        var ext = System.IO.Path.GetExtension(entry.Path).ToLowerInvariant();
        if (ext is not ".csproj" and not ".vbproj" and not ".fsproj") return null;

        if (!File.Exists(entry.Path)) return null;

        // VSProjectParser is synchronous XML parsing; run on thread pool to avoid
        // blocking the UI thread in large solutions.
        return await Task.Run(() =>
        {
            try
            {
                var parsed = VSProjectParser.Parse(entry.Path);

                // Override name/guid from the .sln entry (canonical source).
                return new VsProject
                {
                    Id               = parsed.Id,
                    Name             = entry.Name,
                    ProjectFilePath  = parsed.ProjectFilePath,
                    Items            = parsed.Items,
                    RootFolders      = parsed.RootFolders,
                    ProjectType      = parsed.ProjectType,
                    TargetFramework  = parsed.TargetFramework,
                    Language         = parsed.Language,
                    OutputType       = parsed.OutputType,
                    AssemblyName     = parsed.AssemblyName,
                    RootNamespace    = parsed.RootNamespace,
                    ProjectGuid      = entry.Guid,
                    ProjectReferences  = parsed.ProjectReferences,
                    PackageReferences  = parsed.PackageReferences,
                    AssemblyReferences = parsed.AssemblyReferences,
                    AnalyzerReferences = parsed.AnalyzerReferences,
                };
            }
            catch
            {
                // Return a minimal stub so the solution still loads.
                return new VsProject
                {
                    Name            = entry.Name,
                    ProjectFilePath = entry.Path,
                    ProjectGuid     = entry.Guid,
                    ProjectType     = System.IO.Path.GetExtension(entry.Path).TrimStart('.').ToLowerInvariant(),
                };
            }
        }, ct).ConfigureAwait(false);
    }

    // -----------------------------------------------------------------------
    // Solution folder hierarchy
    // -----------------------------------------------------------------------

    private static List<ISolutionFolder> BuildSolutionFolders(
        Dictionary<string, SlnProjectEntry> folderEntries,
        Dictionary<string, string> nestedMap,
        List<VsProject> projects)
    {
        // Create a VsSolutionFolder for every solution-folder entry.
        var folders = folderEntries.ToDictionary(
            kvp => kvp.Key,
            kvp => new VsSolutionFolder(kvp.Value.Guid, kvp.Value.Name),
            StringComparer.OrdinalIgnoreCase);

        // Wire project guids into their containing solution folder (if any).
        foreach (var project in projects)
        {
            if (nestedMap.TryGetValue(project.ProjectGuid, out var parentFolderGuid)
             && folders.TryGetValue(parentFolderGuid, out var parentFolder))
            {
                parentFolder.AddProjectId(project.Name);
            }
        }

        // Wire child solution folders into their parent.
        foreach (var (childGuid, folder) in folders)
        {
            if (nestedMap.TryGetValue(childGuid, out var parentGuid)
             && folders.TryGetValue(parentGuid, out var parentFolder))
            {
                parentFolder.AddChild(folder);
            }
        }

        // Root folders are those without a parent.
        return folders.Values
            .Where(f => !nestedMap.ContainsKey(f.Id))
            .Cast<ISolutionFolder>()
            .ToList();
    }

    // -----------------------------------------------------------------------
    // Startup project detection
    // -----------------------------------------------------------------------

    private static VsProject? DetermineStartupProject(
        IEnumerable<VsProject> projects, string content)
    {
        var executables = projects.Where(p =>
            p.OutputType.Equals("Exe",    StringComparison.OrdinalIgnoreCase) ||
            p.OutputType.Equals("WinExe", StringComparison.OrdinalIgnoreCase)).ToList();

        if (executables.Count == 0) return null;

        // Pass 1 — prefer projects not in Sample / Test / Sandbox folders.
        // Fixes #197 RC-5: WpfHexEditor.Sample.HexEditor was picked over
        // WpfHexEditor.App because it appeared first in the .sln file.
        var primary = executables.FirstOrDefault(p =>
            !ContainsSampleOrTestSegment(p.ProjectFilePath));

        // Pass 2 — fall back to original behaviour when every executable
        // lives in a sample or test folder (edge case, preserves compat).
        return primary ?? executables[0];
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="path"/> contains a
    /// path segment that identifies the project as a sample, test, or sandbox,
    /// disqualifying it from being auto-selected as the startup project.
    /// </summary>
    private static bool ContainsSampleOrTestSegment(string path)
    {
        // Normalise separators and test individual segments so that a project
        // named e.g. "TestResults.App" in a non-test folder is not excluded.
        var segments = path.Replace('\\', '/').Split('/');
        return segments.Any(s =>
            s.Contains("Sample",  StringComparison.OrdinalIgnoreCase) ||
            s.Contains("Test",    StringComparison.OrdinalIgnoreCase) ||
            s.Contains("Sandbox", StringComparison.OrdinalIgnoreCase));
    }

    // -----------------------------------------------------------------------
    // Private records / helpers
    // -----------------------------------------------------------------------

    private sealed record SlnProjectEntry(
        string TypeGuid,
        string Name,
        string Path,
        string Guid);

    /// <summary>Mutable solution folder used while building the tree.</summary>
    private sealed class VsSolutionFolder : ISolutionFolder
    {
        private readonly List<string>           _projectIds = [];
        private readonly List<ISolutionFolder>  _children   = [];

        public string Id   { get; }
        public string Name { get; }

        public IReadOnlyList<string>          ProjectIds => _projectIds;
        public IReadOnlyList<ISolutionFolder> Children   => _children;

        internal VsSolutionFolder(string id, string name) { Id = id; Name = name; }

        internal void AddProjectId(string id) => _projectIds.Add(id);
        internal void AddChild(VsSolutionFolder child) => _children.Add(child);
    }
}

// ==========================================================
// Project: WpfHexEditor.Core.Roslyn
// File: RoslynWorkspaceManager.cs
// Contributors: Claude Opus 4.6
// Created: 2026-04-01
// Description:
//     Manages Roslyn workspaces for in-process C#/VB.NET analysis.
//     Supports two modes: AdhocWorkspace (standalone files) and
//     MSBuildWorkspace (full solution with project graph + NuGet refs).
//     Hot-swap: transitions from Adhoc → MSBuild when solution is opened.
//
// Architecture Notes:
//     Owns either an AdhocWorkspace or MSBuildWorkspace. Documents are
//     tracked via _openDocuments. CurrentSolution always returns the
//     active workspace's immutable snapshot. Thread-safe via _solutionLock.
// ==========================================================

using System.Collections.Concurrent;
using System.IO;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace WpfHexEditor.Core.Roslyn;

/// <summary>
/// Manages Roslyn workspaces for in-process C#/VB.NET analysis.
/// Supports <see cref="AdhocWorkspace"/> (standalone) and <see cref="MSBuildWorkspace"/>
/// (full solution). Thread-safe via immutable Solution snapshots.
/// </summary>
internal sealed class RoslynWorkspaceManager : IDisposable
{
    private AdhocWorkspace _adhocWorkspace;
    private MSBuildWorkspace? _msbuildWorkspace;
    private readonly ConcurrentDictionary<string, DocumentId> _openDocuments = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ProjectId> _adhocProjects = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _solutionLock = new();

    // Default framework references for standalone files (no .csproj).
    private static readonly string[] s_defaultAssemblyNames =
    [
        "mscorlib",
        "System",
        "System.Core",
        "System.Runtime",
        "System.Collections",
        "System.Linq",
        "System.Threading.Tasks",
        "System.IO",
        "System.Net.Http",
        "System.Console",
        "System.Text.Json",
        "System.Text.RegularExpressions",
        "netstandard",
    ];

    private static readonly Lazy<IReadOnlyList<MetadataReference>> s_defaultReferences = new(BuildDefaultReferences);

    public RoslynWorkspaceManager()
    {
        var host = MefHostServices.DefaultHost;
        _adhocWorkspace = new AdhocWorkspace(host);
    }

    /// <summary>Whether a MSBuild solution is currently loaded.</summary>
    public bool IsSolutionLoaded => _msbuildWorkspace is not null;

    /// <summary>Current immutable solution snapshot — safe for concurrent reads.</summary>
    public Solution CurrentSolution => _msbuildWorkspace?.CurrentSolution ?? _adhocWorkspace.CurrentSolution;

    /// <summary>The underlying workspace (needed by some Roslyn services like CompletionService).</summary>
    public Workspace Workspace => (Workspace?)_msbuildWorkspace ?? _adhocWorkspace;

    /// <summary>All currently open document file paths.</summary>
    public IReadOnlyCollection<string> OpenDocumentPaths => _openDocuments.Keys.ToArray();

    // ── Document Lifecycle ────────────────────────────────────────────────────

    public DocumentId? GetDocumentId(string filePath)
    {
        // When MSBuild workspace is active, look up by file path in the solution.
        if (_msbuildWorkspace is not null)
        {
            var ids = CurrentSolution.GetDocumentIdsWithFilePath(filePath);
            if (ids.Length > 0) return ids[0];
        }
        return _openDocuments.TryGetValue(filePath, out var id) ? id : null;
    }

    public Document? GetDocument(string filePath)
    {
        var docId = GetDocumentId(filePath);
        return docId is not null ? CurrentSolution.GetDocument(docId) : null;
    }

    public void OpenDocument(string filePath, string languageId, string text)
    {
        if (_openDocuments.ContainsKey(filePath)) return;

        if (_msbuildWorkspace is not null)
        {
            // MSBuild mode: document may already exist in the project graph.
            var existingIds = CurrentSolution.GetDocumentIdsWithFilePath(filePath);
            if (existingIds.Length > 0)
            {
                _openDocuments[filePath] = existingIds[0];
                // Update text to match editor content (may differ from disk).
                UpdateDocument(filePath, text);
                return;
            }
            // File not in any project — add to a misc project.
        }

        // AdhocWorkspace or file not in MSBuild solution.
        var projectId = GetOrCreateAdhocProject(filePath, languageId);
        var docId = DocumentId.CreateNewId(projectId, debugName: filePath);

        lock (_solutionLock)
        {
            var workspace = ActiveMutableWorkspace;
            var solution = workspace.CurrentSolution
                .AddDocument(docId, Path.GetFileName(filePath), SourceText.From(text), filePath: filePath);
            workspace.TryApplyChanges(solution);
        }

        _openDocuments[filePath] = docId;
    }

    public void UpdateDocument(string filePath, string newText)
    {
        var docId = GetDocumentId(filePath);
        if (docId is null) return;

        lock (_solutionLock)
        {
            var workspace = ActiveMutableWorkspace;
            var solution = workspace.CurrentSolution
                .WithDocumentText(docId, SourceText.From(newText));
            workspace.TryApplyChanges(solution);
        }
    }

    public void CloseDocument(string filePath)
    {
        _openDocuments.TryRemove(filePath, out _);
        // Don't remove from solution — MSBuild documents persist.
        // Adhoc documents persist too (removed on project unload).
    }

    // ── Solution Lifecycle ────────────────────────────────────────────────────

    /// <summary>
    /// Loads a .sln or .csproj via MSBuildWorkspace, replacing the AdhocWorkspace.
    /// Already-open documents are migrated to the MSBuild solution.
    /// </summary>
    public async Task LoadSolutionAsync(string solutionOrProjectPath, CancellationToken ct)
    {
        var msbuild = CreateMsBuildWorkspace();

        var ext = Path.GetExtension(solutionOrProjectPath).ToLowerInvariant();
        if (ext == ".sln")
            await msbuild.OpenSolutionAsync(solutionOrProjectPath, cancellationToken: ct).ConfigureAwait(false);
        else
            await msbuild.OpenProjectAsync(solutionOrProjectPath, cancellationToken: ct).ConfigureAwait(false);

        ApplyNewMsBuildWorkspace(msbuild);
    }

    /// <summary>
    /// Loads individual .csproj / .vbproj files via MSBuildWorkspace.
    /// Format-agnostic: works regardless of whether the IDE opened a .sln, .slnx, .whsln, etc.
    /// This mirrors the VS project-system approach where Roslyn is driven by individual
    /// project paths rather than the solution file format.
    /// </summary>
    public async Task LoadProjectsAsync(IEnumerable<string> projectPaths, CancellationToken ct)
    {
        var msbuild = CreateMsBuildWorkspace();

        foreach (var path in projectPaths)
        {
            if (ct.IsCancellationRequested) break;
            await msbuild.OpenProjectAsync(path, cancellationToken: ct).ConfigureAwait(false);
        }

        ApplyNewMsBuildWorkspace(msbuild);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static MSBuildWorkspace CreateMsBuildWorkspace()
    {
        var ws = MSBuildWorkspace.Create();
        ws.WorkspaceFailed += (_, e) =>
            // Log but don't throw — partial solutions are still useful.
            System.Diagnostics.Debug.WriteLine($"[Roslyn] Workspace warning: {e.Diagnostic.Message}");
        return ws;
    }

    private void ApplyNewMsBuildWorkspace(MSBuildWorkspace msbuild)
    {
        lock (_solutionLock)
            _msbuildWorkspace = msbuild;

        // Migrate open documents: re-bind to their new DocumentIds in the MSBuild solution.
        foreach (var (filePath, _) in _openDocuments)
        {
            var ids = msbuild.CurrentSolution.GetDocumentIdsWithFilePath(filePath);
            if (ids.Length > 0)
                _openDocuments[filePath] = ids[0];
        }
    }

    /// <summary>
    /// Unloads the MSBuild solution and reverts to AdhocWorkspace.
    /// </summary>
    public void UnloadSolution()
    {
        lock (_solutionLock)
        {
            _msbuildWorkspace?.Dispose();
            _msbuildWorkspace = null;
        }

        // Re-create adhoc workspace for standalone files.
        _adhocWorkspace.Dispose();
        _adhocWorkspace = new AdhocWorkspace(MefHostServices.DefaultHost);
        _adhocProjects.Clear();
        _openDocuments.Clear();
    }

    public void Dispose()
    {
        _msbuildWorkspace?.Dispose();
        _adhocWorkspace.Dispose();
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    private Workspace ActiveMutableWorkspace => (Workspace?)_msbuildWorkspace ?? _adhocWorkspace;

    private ProjectId GetOrCreateAdhocProject(string filePath, string languageId)
    {
        var dir = Path.GetDirectoryName(filePath) ?? ".";
        if (_adhocProjects.TryGetValue(dir, out var existingId))
            return existingId;

        var roslynLanguage = languageId switch
        {
            "csharp" => LanguageNames.CSharp,
            "vbnet"  => LanguageNames.VisualBasic,
            _        => LanguageNames.CSharp,
        };

        var references = TryResolveProjectReferences(filePath) ?? s_defaultReferences.Value;

        var projectId = ProjectId.CreateNewId(debugName: $"Standalone_{Path.GetFileName(dir)}");
        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            name: $"Standalone_{Path.GetFileName(dir)}",
            assemblyName: $"Standalone_{Path.GetFileName(dir)}",
            language: roslynLanguage,
            metadataReferences: references);

        lock (_solutionLock)
        {
            var solution = _adhocWorkspace.CurrentSolution.AddProject(projectInfo);
            _adhocWorkspace.TryApplyChanges(solution);
        }

        _adhocProjects[dir] = projectId;
        return projectId;
    }

    /// <summary>
    /// Fix 8: Scans upward for nearest .csproj/.vbproj and resolves PackageReference
    /// assemblies from NuGet cache. Returns null if no project file found.
    /// </summary>
    private static IReadOnlyList<MetadataReference>? TryResolveProjectReferences(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        while (dir is not null)
        {
            var csproj = Directory.GetFiles(dir, "*.csproj").FirstOrDefault()
                      ?? Directory.GetFiles(dir, "*.vbproj").FirstOrDefault();
            if (csproj is not null)
                return ResolveFromProjectFile(csproj);
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static IReadOnlyList<MetadataReference> ResolveFromProjectFile(string projectFilePath)
    {
        var refs = new List<MetadataReference>(s_defaultReferences.Value);

        try
        {
            var doc = XDocument.Load(projectFilePath);
            var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
            var nugetRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

            foreach (var pkgRef in doc.Descendants(ns + "PackageReference"))
            {
                var name = pkgRef.Attribute("Include")?.Value;
                var version = pkgRef.Attribute("Version")?.Value;
                if (name is null || version is null) continue;

                // Look for assemblies in the NuGet cache.
                var pkgDir = Path.Combine(nugetRoot, name.ToLowerInvariant(), version, "lib");
                if (!Directory.Exists(pkgDir)) continue;

                // Prefer net8.0, then net7.0, then netstandard2.0, then any TFM.
                var tfmDir = new[] { "net8.0", "net7.0", "net6.0", "netstandard2.1", "netstandard2.0" }
                    .Select(tfm => Path.Combine(pkgDir, tfm))
                    .FirstOrDefault(Directory.Exists);

                if (tfmDir is null)
                {
                    var any = Directory.GetDirectories(pkgDir).FirstOrDefault();
                    if (any is not null) tfmDir = any;
                }

                if (tfmDir is null) continue;

                foreach (var dll in Directory.GetFiles(tfmDir, "*.dll"))
                {
                    try { refs.Add(MetadataReference.CreateFromFile(dll)); }
                    catch { /* skip unloadable assemblies */ }
                }
            }
        }
        catch { /* project file parsing failure — fall back to defaults */ }

        return refs;
    }

    private static IReadOnlyList<MetadataReference> BuildDefaultReferences()
    {
        var refs = new List<MetadataReference>();
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (trustedPlatformAssemblies is null) return refs;

        var paths = trustedPlatformAssemblies.Split(Path.PathSeparator);
        var targetNames = new HashSet<string>(s_defaultAssemblyNames, StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (targetNames.Contains(name) && File.Exists(path))
                refs.Add(MetadataReference.CreateFromFile(path));
        }

        return refs;
    }
}

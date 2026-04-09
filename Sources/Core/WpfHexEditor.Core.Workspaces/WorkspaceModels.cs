// ==========================================================
// Project: WpfHexEditor.Core.Workspaces
// File: WorkspaceModels.cs
// Description:
//     Data records for workspace snapshots stored inside .whidews ZIP files.
//     Manifest, open-file entries, settings override, and full state.
// Architecture: Pure data records — no WPF or App dependencies.
// ==========================================================

namespace WpfHexEditor.Core.Workspaces;

/// <summary>Top-level workspace metadata written to manifest.json.</summary>
public sealed record WorkspaceManifest(
    string  Name,
    string  Version   = "1.0",
    string? CreatedAt = null,
    string? Author    = null);

/// <summary>A single document that was open when the workspace was saved.</summary>
public sealed record OpenFileEntry(
    string  Path,
    string? EditorId   = null,
    int     CursorLine = 0,
    int     CursorCol  = 0);

/// <summary>Solution reference stored in solution.json.</summary>
public sealed record WorkspaceSolutionState(
    string? SolutionPath);

/// <summary>Partial settings overrides stored in settings.json (null = use global).</summary>
public sealed record WorkspaceSettingsOverride(
    string? ThemeName,
    string? ActiveBuildConfigName = null,
    string? ActiveBuildPlatform   = null);

/// <summary>
/// A delta passed into SaveAsync / NewAsync to capture the current IDE state.
/// The App layer constructs this just before writing the .whidews file.
/// </summary>
public sealed record WorkspaceCapture(
    string                       LayoutJson,
    string?                      SolutionPath,
    IReadOnlyList<OpenFileEntry> OpenFiles,
    string?                      ThemeName,
    string?                      ActiveBuildConfigName = null,
    string?                      ActiveBuildPlatform   = null,
    WorkspacePluginState?        PluginStates          = null);

/// <summary>
/// Per-plugin state bag stored as <c>plugins.json</c> inside the .whidews archive.
/// Each entry is keyed by plugin ID; the value is a raw JSON string produced by
/// <c>IWorkspacePersistable.CaptureWorkspaceState()</c>.
/// </summary>
public sealed class WorkspacePluginState
{
    /// <summary>Plugin state entries keyed by stable plugin ID.</summary>
    public Dictionary<string, string> Entries { get; init; } = new();
}

/// <summary>Full in-memory representation of a .whidews workspace.</summary>
public sealed class WorkspaceState
{
    public WorkspaceManifest            Manifest     { get; init; } = new("Unnamed");
    public string                       Layout       { get; init; } = string.Empty;
    public WorkspaceSolutionState       Solution     { get; init; } = new(null);
    public IReadOnlyList<OpenFileEntry> Files        { get; init; } = [];
    public WorkspaceSettingsOverride    Settings     { get; init; } = new(null);
    public WorkspacePluginState         PluginStates { get; init; } = new();
}

/// <summary>Event arguments for workspace-opened notifications.</summary>
public sealed class WorkspaceOpenedEventArgs(string name, string path) : EventArgs
{
    public string Name { get; } = name;
    public string Path { get; } = path;
}

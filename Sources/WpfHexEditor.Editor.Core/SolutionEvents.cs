//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

// ── Solution ─────────────────────────────────────────────────────────────────

public enum SolutionChangeKind { Opened, Closed, Modified }

public sealed class SolutionChangedEventArgs : EventArgs
{
    public ISolution?          Solution { get; set; }
    public SolutionChangeKind  Kind     { get; set; }
}

// ── Project ──────────────────────────────────────────────────────────────────

public enum ProjectChangeKind { Added, Removed, Modified }

public sealed class ProjectChangedEventArgs : EventArgs
{
    public IProject         Project { get; set; } = null!;
    public ProjectChangeKind Kind   { get; set; }
}

// ── Item ─────────────────────────────────────────────────────────────────────

public sealed class ProjectItemEventArgs : EventArgs
{
    public IProjectItem Item    { get; set; } = null!;
    public IProject     Project { get; set; } = null!;
    /// <summary>
    /// Set by the inline-rename path; <see langword="null"/> means "ask via dialog".
    /// </summary>
    public string?      NewName { get; set; }
}

public sealed class ProjectItemActivatedEventArgs : EventArgs
{
    public IProjectItem Item    { get; set; } = null!;
    public IProject     Project { get; set; } = null!;
}

// ── Item move (DragDrop) ──────────────────────────────────────────────────

/// <summary>
/// Fired when the user drags a file node to a new folder in the Solution Explorer.
/// </summary>
public sealed class ItemMoveRequestedEventArgs : EventArgs
{
    public IProjectItem Item           { get; set; } = null!;
    public IProject     Project        { get; set; } = null!;
    /// <summary>
    /// Id of the target virtual folder, or <see langword="null"/> for the project root.
    /// </summary>
    public string?      TargetFolderId { get; set; }
}

// ── Folder ───────────────────────────────────────────────────────────────────

public sealed class FolderRenameEventArgs : EventArgs
{
    public IVirtualFolder Folder  { get; init; } = null!;
    public IProject       Project { get; init; } = null!;
    public string         NewName { get; init; } = string.Empty;
}

public sealed class FolderDeleteEventArgs : EventArgs
{
    public IVirtualFolder Folder  { get; init; } = null!;
    public IProject       Project { get; init; } = null!;
}

public sealed class FolderCreateRequestedEventArgs : EventArgs
{
    public IProject Project        { get; init; } = null!;
    /// <summary>
    /// Id of the parent virtual folder, or <see langword="null"/> for the project root.
    /// </summary>
    public string?  ParentFolderId { get; init; }
    /// <summary>
    /// <see langword="true"/> to also create the corresponding physical directory on disk.
    /// </summary>
    public bool     CreatePhysical { get; init; }
}

public sealed class FolderFromDiskRequestedEventArgs : EventArgs
{
    public IProject Project        { get; init; } = null!;
    /// <summary>
    /// Id of the parent virtual folder, or <see langword="null"/> for the project root.
    /// </summary>
    public string?  ParentFolderId { get; init; }
}

// ── Project rename (panel → host) ────────────────────────────────────────────

/// <summary>
/// Raised by <see cref="ISolutionExplorerPanel"/> when the user commits an inline rename on a project node.
/// </summary>
public sealed class ProjectRenameRequestedEventArgs : EventArgs
{
    public IProject Project { get; init; } = null!;
    public string   NewName { get; init; } = string.Empty;
}

// ── Solution rename (panel → host) ────────────────────────────────────────────

/// <summary>
/// Raised by <see cref="ISolutionExplorerPanel"/> when the user commits an inline rename on the solution node.
/// </summary>
public sealed class SolutionRenameRequestedEventArgs : EventArgs
{
    public ISolution Solution { get; init; } = null!;
    public string    NewName  { get; init; } = string.Empty;
}

// ── Item renamed ──────────────────────────────────────────────────────────────

/// <summary>
/// Raised by <see cref="ISolutionManager"/> after a project item has been successfully renamed on disk and in the model.
/// </summary>
public sealed class ProjectItemRenamedEventArgs : EventArgs
{
    public IProject     Project         { get; init; } = null!;
    public IProjectItem Item            { get; init; } = null!;
    /// <summary>
    /// Name the item had before the rename.
    /// </summary>
    public string       OldName         { get; init; } = string.Empty;
    /// <summary>
    /// Absolute file path the item had before the rename.
    /// </summary>
    public string       OldAbsolutePath { get; init; } = string.Empty;
}

// ── Solution Explorer — shell actions ────────────────────────────────────────

/// <summary>
/// Fired when the user chooses "Open With…" from the Solution Explorer context menu.
/// The host shows an editor-picker dialog and opens the file in the chosen editor.
/// </summary>
public sealed class OpenWithRequestedEventArgs : EventArgs
{
    public IProjectItem Item    { get; init; } = null!;
    public IProject     Project { get; init; } = null!;
}

/// <summary>
/// Fired when the user chooses "Include in Project" on a physical file that is
/// not yet a project item (Show All Files mode).
/// </summary>
public sealed class PhysicalFileIncludeRequestedEventArgs : EventArgs
{
    /// <summary>Absolute path of the physical file to include.</summary>
    public string   PhysicalPath   { get; init; } = string.Empty;
    public IProject Project        { get; init; } = null!;
    /// <summary>Id of the virtual folder to place the item in, or <see langword="null"/> for the project root.</summary>
    public string?  TargetFolderId { get; init; }
}

// ── Format upgrade ────────────────────────────────────────────────────────────

/// <summary>
/// Raised by <see cref="ISolutionManager"/> immediately after a solution (and its projects)
/// are loaded from disk in an older format version.
/// The host should present a non-blocking UI (e.g. an InfoBar) prompting the user to upgrade
/// or continue in read-only mode.
/// </summary>
public sealed class FormatUpgradeRequiredEventArgs : EventArgs
{
    public ISolution Solution { get; init; } = null!;

    /// <summary>
    /// The highest format version found on disk (lowest common denominator across all files).
    /// </summary>
    public int FromVersion { get; init; }

    /// <summary>
    /// The format version the application writes (the current application format version).
    /// </summary>
    public int ToVersion { get; init; }

    /// <summary>
    /// Absolute paths of all files that would be upgraded (solution + projects).
    /// </summary>
    public IReadOnlyList<string> AffectedFiles { get; init; } = [];
}

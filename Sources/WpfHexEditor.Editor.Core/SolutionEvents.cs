//////////////////////////////////////////////
// Apache 2.0  - 2026
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
    /// <summary>Set by the inline-rename path; <see langword="null"/> means "ask via dialog".</summary>
    public string?      NewName { get; set; }
}

public sealed class ProjectItemActivatedEventArgs : EventArgs
{
    public IProjectItem Item    { get; set; } = null!;
    public IProject     Project { get; set; } = null!;
}

// ── Item move (DragDrop) ──────────────────────────────────────────────────

/// <summary>Fired when the user drags a file node to a new folder in the Solution Explorer.</summary>
public sealed class ItemMoveRequestedEventArgs : EventArgs
{
    public IProjectItem Item           { get; set; } = null!;
    public IProject     Project        { get; set; } = null!;
    /// <summary>Id of the target virtual folder, or <see langword="null"/> for the project root.</summary>
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

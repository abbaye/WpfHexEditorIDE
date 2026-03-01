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
}

public sealed class ProjectItemActivatedEventArgs : EventArgs
{
    public IProjectItem Item    { get; set; } = null!;
    public IProject     Project { get; set; } = null!;
}

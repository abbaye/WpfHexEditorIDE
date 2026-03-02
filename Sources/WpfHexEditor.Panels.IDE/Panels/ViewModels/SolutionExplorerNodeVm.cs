//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Panels.IDE.ViewModels;

// ── Base ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Base class for all nodes displayed in the Solution Explorer tree.
/// </summary>
public abstract class SolutionExplorerNodeVm : INotifyPropertyChanged
{
    private bool _isExpanded = true;
    private bool _isSelected;

    public abstract string DisplayName { get; }
    /// <summary>
    /// Segoe MDL2 / Fluent icon glyph shown before the name.
    /// </summary>
    public abstract string Icon { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// True while an inline rename TextBox is active on this node.
    /// </summary>
    public virtual bool IsEditing => false;

    public ObservableCollection<SolutionExplorerNodeVm> Children { get; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ── Solution node ─────────────────────────────────────────────────────────────

public sealed class SolutionNodeVm : SolutionExplorerNodeVm
{
    private readonly ISolution _solution;
    private bool   _isEditing;
    private string _editingName = string.Empty;

    public SolutionNodeVm(ISolution solution)
    {
        _solution = solution;
    }

    public string Label => $"Solution '{_solution.Name}' ({_solution.Projects.Count} project{(_solution.Projects.Count == 1 ? "" : "s")})";
    public override string DisplayName => Label;
    public override string Icon => "\uE8B7"; // Fluent: FolderOpen

    public ISolution Source => _solution;

    // ── Inline rename ───────────────────────────────────────────────────────

    public override bool IsEditing => _isEditing;

    private void SetIsEditing(bool value) { _isEditing = value; OnPropertyChanged(nameof(IsEditing)); }

    public string EditingName
    {
        get => _editingName;
        set { _editingName = value; OnPropertyChanged(); }
    }

    public void   BeginEdit()  { EditingName = _solution.Name; SetIsEditing(true); }
    public string CommitEdit() { var name = _editingName.Trim(); SetIsEditing(false); return name; }
    public void   CancelEdit() => SetIsEditing(false);
}

// ── Project node ──────────────────────────────────────────────────────────────

public sealed class ProjectNodeVm : SolutionExplorerNodeVm
{
    private readonly IProject _project;
    private bool _isModified;

    public ProjectNodeVm(IProject project)
    {
        _project    = project;
        _isModified = project.IsModified;
    }

    public override string DisplayName => _isModified ? $"{_project.Name} *" : _project.Name;

    public override string Icon => _project.ProjectType switch
    {
        "rom-hacking"          => "\uE7FC", // Gamepad
        "patch-development"    => "\uE8AD", // Repair
        "translation"          => "\uE8C1", // Globe
        "binary-analysis"      => "\uE773", // Search
        "forensics"            => "\uEADF", // BugAdd
        "firmware-analysis"    => "\uE8B8", // Chips (Circuit)
        "network-capture"      => "\uE839", // Network (Wireless)
        "scientific-data"      => "\uE9D9", // Beaker
        "media-inspection"     => "\uE8B9", // Pictures
        "reverse-engineering"  => "\uE8A2", // Library (Disassemble)
        "decompilation"        => "\uE8F1", // Code
        "crypto-analysis"      => "\uE1F6", // Lock
        "format-definition"    => "\uE8D6", // Dictionary
        "text-script"          => "\uE8F1", // Code
        "scratch"              => "\uE70F", // Edit
        _                      => "\uE8F1", // Code (default / "empty")
    };

    public bool IsModified
    {
        get => _isModified;
        set { _isModified = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
    }

    public IProject Source => _project;

    // ── Inline rename ───────────────────────────────────────────────────────

    private bool   _isEditing;
    private string _editingName = string.Empty;

    public override bool IsEditing => _isEditing;

    private void SetIsEditing(bool value) { _isEditing = value; OnPropertyChanged(nameof(IsEditing)); }

    public string EditingName
    {
        get => _editingName;
        set { _editingName = value; OnPropertyChanged(); }
    }

    public void   BeginEdit()  { EditingName = _project.Name; SetIsEditing(true); }
    public string CommitEdit() { var name = _editingName.Trim(); SetIsEditing(false); return name; }
    public void   CancelEdit() => SetIsEditing(false);
}

// ── Virtual folder node ───────────────────────────────────────────────────────

public sealed class FolderNodeVm : SolutionExplorerNodeVm
{
    public FolderNodeVm(IVirtualFolder folder)
    {
        Folder = folder;
    }

    public override string DisplayName => Folder.Name;
    public override string Icon => "\uE8B7"; // Fluent: Folder

    public IVirtualFolder Folder   { get; }
    /// <summary>
    /// The project that owns this virtual folder.
    /// </summary>
    public IProject?      Project  { get; init; }

    // ── Inline rename ───────────────────────────────────────────────────────

    private bool   _isEditing;
    private string _editingName = string.Empty;

    public override bool IsEditing => _isEditing;

    private void SetIsEditing(bool value) { _isEditing = value; OnPropertyChanged(nameof(IsEditing)); }

    public string EditingName
    {
        get => _editingName;
        set { _editingName = value; OnPropertyChanged(); }
    }

    public void   BeginEdit()  { EditingName = Folder.Name; SetIsEditing(true); }
    public string CommitEdit() { var name = _editingName.Trim(); SetIsEditing(false); return name; }
    public void   CancelEdit() => SetIsEditing(false);
}

// ── File node ─────────────────────────────────────────────────────────────────

public sealed class FileNodeVm : SolutionExplorerNodeVm
{
    private readonly IProjectItem _item;
    private bool _isDefaultTbl;

    public FileNodeVm(IProjectItem item, bool isDefaultTbl = false)
    {
        _item         = item;
        _isDefaultTbl = isDefaultTbl;
        if (_item is INotifyPropertyChanged inpc)
            inpc.PropertyChanged += OnItemPropertyChanged;
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IProjectItem.IsModified))
        {
            OnPropertyChanged(nameof(IsModified));
            OnPropertyChanged(nameof(DisplayName));
        }
        else if (e.PropertyName is nameof(IProjectItem.Name))
        {
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public override string DisplayName => _item.IsModified ? $"{_item.Name} *" : _item.Name;

    public override string Icon => _item.ItemType switch
    {
        ProjectItemType.FormatDefinition => "\uE8A5", // Page
        ProjectItemType.Patch            => "\uE8AD", // Repair
        ProjectItemType.Tbl              => "\uE8FD", // TableGroup
        ProjectItemType.Json             => "\uE8A5", // Page
        ProjectItemType.Text             => "\uE8A5", // Page
        ProjectItemType.Script           => "\uE8F1", // Code
        ProjectItemType.Image            => "\uEB9F", // Photo2
        ProjectItemType.Tile             => "\uE80A", // Tiles2
        ProjectItemType.Audio            => "\uE768", // Play (audio)
        ProjectItemType.Comparison       => "\uE8C9", // SyncFolder (diff)
        _                                => "\uE8A5", // Page (Binary default)
    };

    /// <summary>
    /// True if this TBL file is designated as the project-default TBL; displayed in bold.
    /// </summary>
    public bool IsDefaultTbl
    {
        get => _isDefaultTbl;
        set { _isDefaultTbl = value; OnPropertyChanged(); }
    }

    public bool IsModified => _item.IsModified;

    /// <summary>
    /// True when the item's physical file lives outside the project directory.
    /// Such files show a small external-link badge and offer an "Import into Project" context menu action.
    /// </summary>
    public bool IsExternal
    {
        get
        {
            if (Project?.ProjectFilePath is not { } projFile) return false;
            var projDir = System.IO.Path.GetDirectoryName(projFile);
            if (string.IsNullOrEmpty(projDir) || string.IsNullOrEmpty(_item.AbsolutePath)) return false;
            return !_item.AbsolutePath.StartsWith(projDir, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── Inline rename ───────────────────────────────────────────────────────

    private bool   _isEditing;
    private string _editingName = string.Empty;

    /// <summary>
    /// True while the inline-rename TextBox is active.
    /// </summary>
    public override bool IsEditing => _isEditing;

    private void SetIsEditing(bool value) { _isEditing = value; OnPropertyChanged(nameof(IsEditing)); }

    /// <summary>
    /// Bound to the inline-rename TextBox text.
    /// </summary>
    public string EditingName
    {
        get => _editingName;
        set { _editingName = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Enters inline-rename mode, pre-filling the box with the current name.
    /// </summary>
    public void BeginEdit()
    {
        EditingName = _item.Name;
        SetIsEditing(true);
    }

    /// <summary>
    /// Leaves rename mode and returns the trimmed new name.
    /// </summary>
    public string CommitEdit()
    {
        var name  = _editingName.Trim();
        SetIsEditing(false);
        return name;
    }

    /// <summary>
    /// Cancels rename mode without applying any change.
    /// </summary>
    public void CancelEdit() => SetIsEditing(false);

    // ── Changeset child node ────────────────────────────────────────────────

    /// <summary>
    /// Adds or removes the <see cref="ChangesetNodeVm"/> child depending on whether
    /// the companion <c>.whchg</c> file currently exists on disk.
    /// Must be called on the UI thread.
    /// </summary>
    public void RefreshChangesetChild()
    {
        var changesetPath = _item.AbsolutePath + ".whchg";
        var existing      = Children.OfType<ChangesetNodeVm>().FirstOrDefault();

        if (System.IO.File.Exists(changesetPath))
        {
            if (existing is null)
                Children.Add(new ChangesetNodeVm(changesetPath, _item, Project));
        }
        else
        {
            if (existing is not null)
                Children.Remove(existing);
        }
    }

    // ────────────────────────────────────────────────────────────────────────

    public IProjectItem Source => _item;
    public IProject?   Project { get; init; }
}

// ── Physical folder node (Show All Files mode) ───────────────────────────────

/// <summary>
/// Represents a physical directory when "Show All Files" is enabled.
/// </summary>
public sealed class PhysicalFolderNodeVm : SolutionExplorerNodeVm
{
    public PhysicalFolderNodeVm(string physicalPath)
    {
        PhysicalPath = physicalPath;
    }

    public string    PhysicalPath { get; }
    public IProject? Project      { get; init; }
    public override string DisplayName => System.IO.Path.GetFileName(PhysicalPath);
    public override string Icon        => "\uE8D5";
}

// ── Physical file node (Show All Files mode) ─────────────────────────────────

/// <summary>Represents a physical file when "Show All Files" is enabled.
/// <see cref="IsInProject"/> is <see langword="true"/> when the file is already a project item.</summary>
public sealed class PhysicalFileNodeVm : SolutionExplorerNodeVm
{
    public PhysicalFileNodeVm(string physicalPath)
    {
        PhysicalPath = physicalPath;
    }

    public string        PhysicalPath { get; }
    public IProject?     Project      { get; init; }
    public IProjectItem? LinkedItem   { get; init; }
    public bool          IsInProject  => LinkedItem is not null;
    public override string DisplayName => System.IO.Path.GetFileName(PhysicalPath);
    public override string Icon        => "\uE8A5";
}

// ── Changeset node (.whchg companion file) ────────────────────────────────────

/// <summary>
/// Represents the <c>.whchg</c> companion file for a <see cref="FileNodeVm"/>.
/// Shown as a child of the owning <see cref="FileNodeVm"/> when the companion
/// file exists on disk (like <c>.xaml.cs</c> nesting in Visual Studio).
/// </summary>
public sealed class ChangesetNodeVm : SolutionExplorerNodeVm
{
    public ChangesetNodeVm(string changesetPath, IProjectItem ownerItem, IProject? project)
    {
        ChangesetPath = changesetPath;
        OwnerItem     = ownerItem;
        Project       = project;
    }

    public string        ChangesetPath { get; }
    public IProjectItem  OwnerItem     { get; }
    public IProject?     Project       { get; }

    public override string DisplayName => System.IO.Path.GetFileName(ChangesetPath);
    /// <summary>Segoe MDL2 "Repair" glyph — matches the Patch project type icon.</summary>
    public override string Icon        => "\uE8AD";
}

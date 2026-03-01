//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.WindowPanels.Panels.ViewModels;

// ── Base ─────────────────────────────────────────────────────────────────────

/// <summary>Base class for all nodes displayed in the Solution Explorer tree.</summary>
public abstract class SolutionExplorerNodeVm : INotifyPropertyChanged
{
    private bool _isExpanded = true;
    private bool _isSelected;

    public abstract string DisplayName { get; }
    /// <summary>Segoe MDL2 / Fluent icon glyph shown before the name.</summary>
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

    public ObservableCollection<SolutionExplorerNodeVm> Children { get; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ── Solution node ─────────────────────────────────────────────────────────────

public sealed class SolutionNodeVm : SolutionExplorerNodeVm
{
    private readonly ISolution _solution;

    public SolutionNodeVm(ISolution solution)
    {
        _solution = solution;
        Label = $"Solution '{solution.Name}' ({solution.Projects.Count} project{(solution.Projects.Count == 1 ? "" : "s")})";
    }

    public string Label { get; }
    public override string DisplayName => Label;
    public override string Icon => "\uE8B7"; // Fluent: FolderOpen

    public ISolution Source => _solution;
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
    public override string Icon => "\uE8F1"; // Fluent: Code

    public bool IsModified
    {
        get => _isModified;
        set { _isModified = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
    }

    public IProject Source => _project;
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

    public IVirtualFolder Folder { get; }
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
    }

    public override string DisplayName => _item.Name;

    public override string Icon => _item.ItemType switch
    {
        ProjectItemType.FormatDefinition => "\uE8A5", // Page (document icon)
        ProjectItemType.Patch            => "\uE8AD", // Repair
        ProjectItemType.Tbl              => "\uE8FD", // TableGroup
        ProjectItemType.Json             => "\uE8A5", // Page
        ProjectItemType.Text             => "\uE8A5", // Page
        _                                => "\uE8A5", // Binary → Page
    };

    /// <summary>True if this TBL file is designated as the project-default TBL; displayed in bold.</summary>
    public bool IsDefaultTbl
    {
        get => _isDefaultTbl;
        set { _isDefaultTbl = value; OnPropertyChanged(); }
    }

    public bool IsModified => _item.IsModified;

    public IProjectItem Source => _item;
    public IProject?   Project { get; init; }
}

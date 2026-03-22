//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Panels.IDE.ViewModels;

// -- Base ---------------------------------------------------------------------

/// <summary>
/// Base class for all nodes displayed in the Solution Explorer tree.
/// </summary>
public abstract class SolutionExplorerNodeVm : INotifyPropertyChanged
{
    private bool _isExpanded = true;
    private bool _isSelected;
    private bool _isSearchVisible = true;

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

    /// <summary>
    /// Set to <see langword="false"/> while a search is active and this node
    /// does not match the query (nor is an ancestor of a matching node).
    /// Bound to <see cref="System.Windows.Visibility"/> via a DataTrigger in the panel XAML.
    /// Reset to <see langword="true"/> when the search box is cleared.
    /// </summary>
    public bool IsSearchVisible
    {
        get => _isSearchVisible;
        set { if (_isSearchVisible == value) return; _isSearchVisible = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// True for nodes that support on-demand child expansion (outline load).
    /// When true a <see cref="LoadingNodeVm"/> sentinel is injected at build time
    /// so the TreeView shows an expand arrow; the sentinel is replaced once the async
    /// outline parse completes.
    /// </summary>
    public bool SupportsExpansion { get; set; }

    public ObservableCollection<SolutionExplorerNodeVm> Children { get; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// -- Solution node -------------------------------------------------------------

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

    // -- Inline rename -------------------------------------------------------

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

// -- Project node --------------------------------------------------------------

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

    private bool _isStartup;

    /// <summary>
    /// True when this project is the solution's startup project.
    /// Drives the bold font weight in the tree (VS behaviour).
    /// </summary>
    public bool IsStartup
    {
        get => _isStartup;
        set { _isStartup = value; OnPropertyChanged(); }
    }

    public IProject Source => _project;

    /// <summary>
    /// VS-like language color for the project icon.
    /// Resolved from <see cref="IProjectWithReferences.Language"/> when available.
    /// </summary>
    public string LanguageColor =>
        (_project is IProjectWithReferences r ? r.Language : null) switch
        {
            "C#"  => "#4FC1FF",   // VS blue for C#
            "VB"  => "#C8A018",   // VS amber for VB
            "F#"  => "#C586C0",   // VS purple for F#
            _     => "#4EC9B0",   // teal default
        };

    // -- Inline rename -------------------------------------------------------

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

// -- Virtual folder node -------------------------------------------------------

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

    /// <summary>
    /// Relative path of this folder within the project tree (computed at build time,
    /// e.g. "Images" or "Tables/Subtables"). Used for physical-directory auto-detection.
    /// </summary>
    public string? ComputedRelPath { get; init; }

    /// <summary>
    /// True when the folder is backed by a physical directory on disk.
    /// Uses the explicit <see cref="IVirtualFolder.PhysicalRelativePath"/> when set,
    /// otherwise falls back to checking whether a directory at <see cref="ComputedRelPath"/>
    /// exists relative to the project file.
    /// </summary>
    public bool IsPhysicallyBacked
    {
        get
        {
            if (Folder.PhysicalRelativePath is not null) return true;
            if (ComputedRelPath is null || Project?.ProjectFilePath is null) return false;
            var projectDir = System.IO.Path.GetDirectoryName(Project.ProjectFilePath);
            if (projectDir is null) return false;
            return System.IO.Directory.Exists(
                System.IO.Path.Combine(projectDir, ComputedRelPath.Replace('/', System.IO.Path.DirectorySeparatorChar)));
        }
    }

    // -- Inline rename -------------------------------------------------------

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

// -- File node -----------------------------------------------------------------

public sealed class FileNodeVm : SolutionExplorerNodeVm
{
    // Shared static reference set once by SolutionExplorerPanel after it creates the clipboard manager.
    // Using a static avoids threading all nodes with a per-instance reference while still enabling
    // the IsPendingCut binding without coupling the VM layer to the service layer.
    private static WpfHexEditor.Panels.IDE.Services.SolutionClipboardManager? _sharedClipboard;

    /// <summary>
    /// Provides the shared clipboard manager used to evaluate <see cref="IsPendingCut"/>.
    /// Must be called once from <c>SolutionExplorerPanel</c> after the manager is created.
    /// </summary>
    public static void SetSharedClipboard(WpfHexEditor.Panels.IDE.Services.SolutionClipboardManager manager)
        => _sharedClipboard = manager;

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
    /// True when the file has been cut (Ctrl+X) and is awaiting a paste operation.
    /// The tree renders these nodes at reduced opacity (0.45) as a visual cue.
    /// </summary>
    public bool IsPendingCut => _sharedClipboard?.IsPendingCut(_item.AbsolutePath) ?? false;

    /// <summary>
    /// Triggers a PropertyChanged notification for <see cref="IsPendingCut"/> so the
    /// data binding updates the node's opacity in the tree.
    /// Call this after any Copy or Cut operation on the clipboard manager.
    /// </summary>
    public void RefreshPendingCut() => OnPropertyChanged(nameof(IsPendingCut));

    private bool _isModifiedExternally;

    /// <summary>
    /// True when the file has been modified externally (by another process).
    /// Shows a warning overlay icon + tooltip in the Solution Explorer.
    /// Set by <see cref="WpfHexEditor.Panels.IDE.Services.SolutionFileWatcher"/>.
    /// </summary>
    public bool IsModifiedExternally
    {
        get => _isModifiedExternally;
        set { _isModifiedExternally = value; OnPropertyChanged(); }
    }

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

    // -- Inline rename -------------------------------------------------------

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

    // -- Changeset child node ------------------------------------------------

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

    // ------------------------------------------------------------------------

    public IProjectItem Source => _item;
    public IProject?   Project { get; init; }
}

// -- Solution Folder node (VS-like — holds Projects at solution level) --------

/// <summary>
/// Represents a VS-like Solution Folder node in the Solution Explorer tree.
/// Solution Folders group <see cref="ProjectNodeVm"/>s logically; they hold no file items.
/// </summary>
public sealed class SolutionFolderNodeVm : SolutionExplorerNodeVm
{
    private bool   _isEditing;
    private string _editingName = string.Empty;

    public SolutionFolderNodeVm(ISolutionFolder folder, ISolution solution)
    {
        Folder   = folder;
        Solution = solution;
    }

    public ISolutionFolder Folder   { get; }
    public ISolution       Solution { get; }

    public override string DisplayName => Folder.Name;
    /// <summary>Segoe MDL2 "FolderOpen" glyph — distinct colour from project-level FolderNodeVm.</summary>
    public override string Icon => "\uE8B7";

    // -- Inline rename ---------------------------------------------------------

    public override bool IsEditing => _isEditing;

    private void SetIsEditing(bool v) { _isEditing = v; OnPropertyChanged(nameof(IsEditing)); }

    public string EditingName
    {
        get => _editingName;
        set { _editingName = value; OnPropertyChanged(); }
    }

    public void   BeginEdit()  { EditingName = Folder.Name; SetIsEditing(true); }
    public string CommitEdit() { var n = _editingName.Trim(); SetIsEditing(false); return n; }
    public void   CancelEdit() => SetIsEditing(false);
}

// -- Physical folder node (Show All Files mode) -------------------------------

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

// -- Physical file node (Show All Files mode) ---------------------------------

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

// -- Dependent file node (convention-nested companion) ------------------------

/// <summary>
/// Represents a file that is nested under a parent <see cref="FileNodeVm"/>
/// by naming convention (e.g. <c>Foo.xaml.cs</c> under <c>Foo.xaml</c>,
/// <c>Foo.Designer.cs</c> under <c>Foo.cs</c>).
/// Visual rendering is dimmed (82% opacity) and italic to signal dependency.
/// All file-level operations (Open, Rename, Delete) are supported.
/// </summary>
public sealed class DependentFileNodeVm : SolutionExplorerNodeVm
{
    private readonly IProjectItem _item;
    private bool   _isEditing;
    private string _editingName = string.Empty;

    public DependentFileNodeVm(IProjectItem item, IProject? project)
    {
        _item   = item;
        Project = project;
    }

    public override string DisplayName => _item.Name;

    /// <summary>Segoe MDL2 U+E71B (Link) — signals the dependency relationship.</summary>
    public override string Icon => "\uE71B";

    public IProjectItem Source  => _item;
    public IProject?    Project { get; init; }

    // -- Inline rename -------------------------------------------------------

    public override bool IsEditing => _isEditing;

    private void SetIsEditing(bool v) { _isEditing = v; OnPropertyChanged(nameof(IsEditing)); }

    public string EditingName
    {
        get => _editingName;
        set { _editingName = value; OnPropertyChanged(); }
    }

    public void   BeginEdit()  { EditingName = _item.Name; SetIsEditing(true); }
    public string CommitEdit() { var n = _editingName.Trim(); SetIsEditing(false); return n; }
    public void   CancelEdit() => SetIsEditing(false);
}

// -- Loading sentinel node (async source outline in progress) -----------------

/// <summary>
/// Placeholder node shown as the sole child of an expandable <see cref="FileNodeVm"/>
/// while the source outline is being loaded asynchronously.
/// Replaced by <see cref="SourceTypeNodeVm"/> instances when the parse completes.
/// </summary>
public sealed class LoadingNodeVm : SolutionExplorerNodeVm
{
    public override string DisplayName => "Loading\u2026";
    /// <summary>Segoe MDL2 U+E9F5 (ProgressRingDots).</summary>
    public override string Icon => "\uE9F5";
}

// -- Source type node (class/struct/interface/enum/record) --------------------

/// <summary>
/// Represents a type declaration found by the regex source scanner inside a .cs file.
/// Children are <see cref="SourceMemberNodeVm"/> instances.
/// Double-click navigates to <see cref="LineNumber"/> in the code editor.
/// </summary>
public sealed class SourceTypeNodeVm : SolutionExplorerNodeVm
{
    public SourceTypeNodeVm(
        WpfHexEditor.Core.SourceAnalysis.Models.SourceTypeModel model,
        string fileAbsolutePath)
    {
        Model            = model;
        FileAbsolutePath = fileAbsolutePath;
    }

    public WpfHexEditor.Core.SourceAnalysis.Models.SourceTypeModel Model { get; }
    public string FileAbsolutePath { get; }

    public override string DisplayName => Model.Name;

    /// <summary>
    /// Segoe MDL2 U+E8F1 (Code) — same glyph for all type kinds;
    /// colour differentiation is applied in XAML via DataTrigger on Model.Kind.
    /// </summary>
    public override string Icon => "\uE8F1";

    /// <summary>1-based line number of the type declaration.</summary>
    public int LineNumber => Model.LineNumber;
}

// -- Source member node (method/property/field/event/constructor) -------------

/// <summary>
/// Represents a member declaration inside a <see cref="SourceTypeNodeVm"/>.
/// Leaf node — no children.
/// Double-click navigates to <see cref="LineNumber"/> in the code editor.
/// </summary>
public sealed class SourceMemberNodeVm : SolutionExplorerNodeVm
{
    public SourceMemberNodeVm(
        WpfHexEditor.Core.SourceAnalysis.Models.SourceMemberModel model,
        string fileAbsolutePath)
    {
        Model            = model;
        FileAbsolutePath = fileAbsolutePath;
    }

    public WpfHexEditor.Core.SourceAnalysis.Models.SourceMemberModel Model { get; }
    public string FileAbsolutePath { get; }

    public override string DisplayName
        => Model.Kind == WpfHexEditor.Core.SourceAnalysis.Models.SourceMemberKind.Constructor
            ? $"{Model.Name}()"
            : string.IsNullOrEmpty(Model.ReturnType)
                ? Model.Name
                : $"{Model.Name} : {Model.ReturnType}";

    public override string Icon => Model.Kind switch
    {
        WpfHexEditor.Core.SourceAnalysis.Models.SourceMemberKind.Constructor => "\uE8C7",
        WpfHexEditor.Core.SourceAnalysis.Models.SourceMemberKind.Method      => "\uE8C7",
        WpfHexEditor.Core.SourceAnalysis.Models.SourceMemberKind.Property    => "\uE7C1",
        WpfHexEditor.Core.SourceAnalysis.Models.SourceMemberKind.Field       => "\uE8D2",
        WpfHexEditor.Core.SourceAnalysis.Models.SourceMemberKind.Event       => "\uE7A8",
        _                                                                     => "\uE8A5",
    };

    /// <summary>1-based line number of the member declaration.</summary>
    public int LineNumber => Model.LineNumber;
}

// -- References container node -------------------------------------------------

/// <summary>
/// "References" group node displayed directly under a <see cref="ProjectNodeVm"/>.
/// Children are <see cref="ProjectReferenceNodeVm"/> and <see cref="PackageReferenceNodeVm"/>.
/// </summary>
public sealed class ReferencesContainerNodeVm : SolutionExplorerNodeVm
{
    public override string DisplayName => "References";
    /// <summary>Segoe MDL2 "Link" glyph — matches VS References folder.</summary>
    public override string Icon        => "\uE71D";
}

// -- Project reference node ----------------------------------------------------

/// <summary>
/// A project-to-project reference under the <see cref="ReferencesContainerNodeVm"/>.
/// Displays the referenced project name; stores the absolute <c>.csproj</c> path.
/// </summary>
public sealed class ProjectReferenceNodeVm : SolutionExplorerNodeVm
{
    public ProjectReferenceNodeVm(string referencePath)
    {
        ReferencePath = referencePath;
    }

    public string ReferencePath { get; }

    public override string DisplayName =>
        System.IO.Path.GetFileNameWithoutExtension(ReferencePath);

    /// <summary>Segoe MDL2 "ProjectCollection" glyph.</summary>
    public override string Icon => "\uEA3C";
}

// -- Package reference node ----------------------------------------------------

/// <summary>
/// A NuGet / package reference under the <see cref="ReferencesContainerNodeVm"/>.
/// Displays the package identifier and optional version.
/// </summary>
public sealed class PackageReferenceNodeVm : SolutionExplorerNodeVm
{
    public PackageReferenceNodeVm(PackageReferenceInfo info)
    {
        Info = info;
    }

    public PackageReferenceInfo Info { get; }

    public override string DisplayName =>
        string.IsNullOrEmpty(Info.Version)
            ? Info.Id
            : $"{Info.Id} ({Info.Version})";

    /// <summary>Segoe MDL2 "Shop" / package box glyph.</summary>
    public override string Icon => "\uE7B8";
}

// -- Assembly reference node ---------------------------------------------------

/// <summary>
/// A <c>&lt;Reference&gt;</c> assembly under the <see cref="ReferencesContainerNodeVm"/>.
/// BCL / framework assemblies use a different icon from external DLL references.
/// </summary>
public sealed class AssemblyReferenceNodeVm : SolutionExplorerNodeVm
{
    public AssemblyReferenceNodeVm(AssemblyReferenceInfo info)
    {
        Info = info;
    }

    public AssemblyReferenceInfo Info { get; }

    public override string DisplayName => Info.Name;

    /// <summary>
    /// Segoe MDL2 "Library" glyph for BCL/framework assemblies;
    /// "Code" glyph for external DLLs that have a HintPath.
    /// </summary>
    public override string Icon =>
        Info.IsFrameworkRef ? "\uE8F1" : "\uE7EE";

    /// <summary>Tooltip text shown in the UI (HintPath or "Framework Assembly").</summary>
    public string Tooltip =>
        Info.HintPath ?? "Framework Assembly";
}

// -- Analyzers container node --------------------------------------------------

/// <summary>
/// "Analyzers" sub-folder node under <see cref="ReferencesContainerNodeVm"/>,
/// mirroring the Visual Studio Analyzers group.
/// Children are <see cref="AnalyzerNodeVm"/> entries.
/// </summary>
public sealed class AnalyzersContainerNodeVm : SolutionExplorerNodeVm
{
    public override string DisplayName => "Analyzers";

    /// <summary>Segoe MDL2 "Diagnostic" / medical glyph.</summary>
    public override string Icon => "\uE9D9";
}

// -- Analyzer node -------------------------------------------------------------

/// <summary>
/// A single Roslyn analyzer DLL under the <see cref="AnalyzersContainerNodeVm"/>.
/// </summary>
public sealed class AnalyzerNodeVm : SolutionExplorerNodeVm
{
    public AnalyzerNodeVm(AnalyzerReferenceInfo info)
    {
        Info = info;
    }

    public AnalyzerReferenceInfo Info { get; }

    public override string DisplayName =>
        System.IO.Path.GetFileNameWithoutExtension(Info.HintPath);

    /// <summary>Segoe MDL2 "Diagnostic" glyph — matches parent container.</summary>
    public override string Icon => "\uE9D9";

    /// <summary>Full path to the analyzer DLL.</summary>
    public string Tooltip => Info.HintPath;
}

// -- Changeset node (.whchg companion file) ------------------------------------

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

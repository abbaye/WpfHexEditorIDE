//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Event args for "Add New Item" / "Add Existing Item" requests from the Solution Explorer context menu
/// or from an external drag-and-drop operation (Windows Explorer).
/// </summary>
public sealed class AddItemRequestedEventArgs : EventArgs
{
    /// <summary>
    /// Project that should receive the new item.
    /// </summary>
    public IProject Project { get; set; } = null!;

    /// <summary>
    /// Virtual folder id to place the item in, or <see langword="null"/> for the project root.
    /// </summary>
    public string? TargetFolderId { get; set; }

    /// <summary>
    /// When set, the host should import exactly these file paths without showing a file-picker dialog.
    /// <see langword="null"/> means the normal "open file dialog" flow.
    /// Populated by external drag-and-drop from Windows Explorer.
    /// </summary>
    public IReadOnlyList<string>? FilePaths { get; set; }
}

/// <summary>
/// Contract for the Solution Explorer panel.
/// <para>
/// The host sets the active solution via <see cref="SetSolution"/> and listens
/// to <see cref="ItemActivated"/> to open files in the appropriate editor.
/// The host also listens to <see cref="ItemSelected"/> (single-click) to drive
/// the Properties panel.
/// </para>
/// <para>
/// The panel never controls document visibility — that is the host's responsibility.
/// </para>
/// </summary>
public interface ISolutionExplorerPanel
{
    /// <summary>
    /// Replaces the tree with the given solution, or clears it when <see langword="null"/>.
    /// </summary>
    void SetSolution(ISolution? solution);

    /// <summary>
    /// Highlights the tree node that corresponds to <paramref name="absolutePath"/>.
    /// </summary>
    void SyncWithFile(string absolutePath);

    /// <summary>
    /// Fired when the user double-clicks an item (or presses Enter). The host opens the file.
    /// </summary>
    event EventHandler<ProjectItemActivatedEventArgs>? ItemActivated;

    /// <summary>
    /// Fired when the user single-clicks an item. The host updates the Properties panel.
    /// </summary>
    event EventHandler<ProjectItemEventArgs>? ItemSelected;

    /// <summary>
    /// Fired when the user commits an inline rename on the solution node.
    /// </summary>
    event EventHandler<SolutionRenameRequestedEventArgs>? SolutionRenameRequested;

    /// <summary>
    /// Fired when the user commits an inline rename on a project node.
    /// </summary>
    event EventHandler<ProjectRenameRequestedEventArgs>? ProjectRenameRequested;

    /// <summary>
    /// Fired when the user requests to rename an item via the context menu.
    /// </summary>
    event EventHandler<ProjectItemEventArgs>? ItemRenameRequested;

    /// <summary>
    /// Fired when the user requests to delete an item via the context menu.
    /// </summary>
    event EventHandler<ProjectItemEventArgs>? ItemDeleteRequested;

    /// <summary>
    /// Raised when the user requests to delete an item from disk (send to Recycle Bin).
    /// </summary>
    event EventHandler<ProjectItemEventArgs>? ItemDeleteFromDiskRequested;

    /// <summary>
    /// Fired when the user drags a file node to a new folder or the project root.
    /// </summary>
    event EventHandler<ItemMoveRequestedEventArgs>? ItemMoveRequested;

    /// <summary>
    /// Fired when the user requests to close the solution from the Solution node context menu.
    /// </summary>
    event EventHandler? CloseSolutionRequested;

    /// <summary>
    /// Fired when the user chooses "Add New Item…" from the context menu on a project or folder node.
    /// </summary>
    event EventHandler<AddItemRequestedEventArgs>? AddNewItemRequested;

    /// <summary>
    /// Fired when the user chooses "Add Existing Item…" from the context menu on a project or folder node.
    /// </summary>
    event EventHandler<AddItemRequestedEventArgs>? AddExistingItemRequested;

    /// <summary>
    /// Fired when the user chooses "Import Built-in Format…" from the context menu on a project node.
    /// </summary>
    event EventHandler<AddItemRequestedEventArgs>? ImportFormatDefinitionRequested;

    /// <summary>
    /// Fired when the user chooses "Import Built-in Syntax…" from the context menu on a project node.
    /// </summary>
    event EventHandler<AddItemRequestedEventArgs>? ImportSyntaxDefinitionRequested;

    /// <summary>
    /// Fired when the user chooses "Convert to TBLX…" from the context menu on a .tbl file node.
    /// The host is responsible for showing the conversion dialog and producing the .tblx output.
    /// </summary>
    event EventHandler<ProjectItemEventArgs>? ConvertTblRequested;

    /// <summary>
    /// Fired when the user chooses "Open With…" from the context menu on a file node.
    /// The host shows an editor-picker dialog and opens the file in the chosen editor.
    /// </summary>
    event EventHandler<OpenWithRequestedEventArgs>? OpenWithRequested;

    /// <summary>
    /// Fired when the user selects a specific editor from the "Open With ›" submenu.
    /// <see cref="OpenWithSpecificEditorEventArgs.FactoryId"/> is <see langword="null"/>
    /// to open with the Hex Editor fallback.
    /// </summary>
    event EventHandler<OpenWithSpecificEditorEventArgs>? OpenWithSpecificRequested;

    /// <summary>
    /// Provides the registry of available editor factories so the panel can build
    /// the "Open With ›" submenu dynamically.
    /// Call this once after registering factories, before the panel is shown.
    /// </summary>
    void SetEditorRegistry(IReadOnlyList<IEditorFactory> factories);

    /// <summary>
    /// Fired when the user chooses "Include in Project" on a physical file node
    /// (visible only in Show All Files mode when the file is not yet part of the project).
    /// </summary>
    event EventHandler<PhysicalFileIncludeRequestedEventArgs>? PhysicalFileIncludeRequested;

    /// <summary>
    /// Fired when the user chooses "Import into Project…" on a file node whose physical
    /// path is located outside the project directory.
    /// The host copies the file into the project directory and updates the item reference.
    /// </summary>
    event EventHandler<ImportExternalFileRequestedEventArgs>? ImportExternalFileRequested;

    /// <summary>
    /// Fired when the user chooses "Save All" from the solution node context menu.
    /// </summary>
    event EventHandler? SaveAllRequested;

    /// <summary>
    /// Fired when the user requests to rename a virtual folder.
    /// </summary>
    event EventHandler<FolderRenameEventArgs>? FolderRenameRequested;

    /// <summary>
    /// Fired when the user requests to delete a virtual folder.
    /// </summary>
    event EventHandler<FolderDeleteEventArgs>? FolderDeleteRequested;

    /// <summary>
    /// Fired when the user requests creation of a new virtual (or physical) folder.
    /// </summary>
    event EventHandler<FolderCreateRequestedEventArgs>?  FolderCreateRequested;

    /// <summary>
    /// Fired when the user chooses "Add Existing Folder…" to import a directory from disk.
    /// </summary>
    event EventHandler<FolderFromDiskRequestedEventArgs>? FolderFromDiskRequested;

    /// <summary>
    /// Enters inline-rename mode on the tree node that corresponds to <paramref name="folder"/>.
    /// Call this immediately after creating a new folder to give it a VS-like creation-rename flow.
    /// </summary>
    void BeginFolderRename(IVirtualFolder folder);

    // -- Solution Folder events ------------------------------------------------

    /// <summary>
    /// Fired when the user requests creation of a new Solution Folder from the context menu.
    /// </summary>
    event EventHandler<SolutionFolderCreateRequestedEventArgs>? SolutionFolderCreateRequested;

    /// <summary>
    /// Fired when the user commits an inline rename on a Solution Folder node.
    /// </summary>
    event EventHandler<SolutionFolderRenameRequestedEventArgs>? SolutionFolderRenameRequested;

    /// <summary>
    /// Fired when the user requests deletion of a Solution Folder (context menu "Remove").
    /// Projects inside the folder are moved back to the solution root.
    /// </summary>
    event EventHandler<SolutionFolderDeleteRequestedEventArgs>? SolutionFolderDeleteRequested;

    /// <summary>
    /// Fired when the user drops a project node onto a Solution Folder or onto the solution root.
    /// </summary>
    event EventHandler<ProjectMovedEventArgs>? ProjectMoveRequested;

    /// <summary>
    /// Enters inline-rename mode on the tree node that corresponds to <paramref name="folder"/>.
    /// Call this immediately after creating a new Solution Folder.
    /// </summary>
    void BeginSolutionFolderRename(ISolutionFolder folder);

    /// <summary>
    /// Gets or sets whether the tree displays the physical file system under the project directory
    /// instead of the virtual folder organisation.
    /// </summary>
    bool ShowAllFiles { get; set; }

    /// <summary>
    /// Fired when the user chooses "Properties" from the context menu.
    /// <see cref="NodePropertiesEventArgs.Item"/> is <see langword="null"/> when the target is the
    /// project node itself; otherwise it is the selected file node.
    /// </summary>
    event EventHandler<NodePropertiesEventArgs>? PropertiesRequested;

    /// <summary>
    /// Fired when the user chooses "Apply to Disk" on a <c>.whchg</c> changeset node.
    /// The host calls <see cref="ISolutionManager.WriteItemToDiskAsync"/> then reloads the editor.
    /// </summary>
    event EventHandler<ProjectItemEventArgs>? WriteToDiskRequested;

    /// <summary>
    /// Fired when the user chooses "Discard Changes" on a <c>.whchg</c> changeset node.
    /// The host calls <see cref="ISolutionManager.DiscardChangesetAsync"/>.
    /// </summary>
    event EventHandler<ProjectItemEventArgs>? DiscardChangesetRequested;

    /// <summary>
    /// Refreshes the changeset child node under the given item's tree node.
    /// Called by the host whenever <c>FileMonitorService.ChangesetFileChanged</c> fires.
    /// </summary>
    void RefreshChangesetNode(IProjectItem item);
}

/// <summary>
/// Event args for context-menu "Properties" requests from the Solution Explorer.
/// </summary>
public sealed class NodePropertiesEventArgs : EventArgs
{
    /// <summary>Project that owns the target node.</summary>
    public IProject?     Project { get; init; }

    /// <summary>The file item, or <see langword="null"/> when the target is the project node itself.</summary>
    public IProjectItem? Item    { get; init; }
}

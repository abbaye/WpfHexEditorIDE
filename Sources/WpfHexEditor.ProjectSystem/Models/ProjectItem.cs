//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.ProjectSystem.Models;

internal sealed class ProjectItem : IProjectItem, INotifyPropertyChanged
{
    private bool             _isModified;
    private EditorConfigDto? _editorConfig;

    public string          Id           { get; set; } = Guid.NewGuid().ToString();
    public string          Name         { get; set; } = "";
    public string          RelativePath { get; set; } = "";
    public string          AbsolutePath { get; set; } = "";
    public ProjectItemType ItemType     { get; set; } = ProjectItemType.Binary;

    public bool IsModified
    {
        get => _isModified;
        set { _isModified = value; OnPropertyChanged(); }
    }

    public EditorConfigDto? EditorConfig
    {
        get => _editorConfig;
        set { _editorConfig = value; OnPropertyChanged(); }
    }

    // ── LinkedItems ──────────────────────────────────────────────────────

    private readonly ObservableCollection<ItemLink> _linkedItems = [];

    public IReadOnlyList<IItemLink> LinkedItems => _linkedItems;

    internal ObservableCollection<ItemLink> LinkedItemsMutable => _linkedItems;

    // ── Bookmarks ────────────────────────────────────────────────────────

    public IReadOnlyList<BookmarkDto>? Bookmarks { get; set; }

    // ── Internal persistence ─────────────────────────────────────────────

    /// <summary>
    /// Raw bytes of unsaved in-memory modifications (null = clean).
    /// </summary>
    internal byte[]? UnsavedModifications { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}

/// <summary>
/// Concrete runtime implementation of <see cref="IItemLink"/>.
/// </summary>
internal sealed class ItemLink : IItemLink
{
    public string ItemId { get; set; } = "";
    public string Role   { get; set; } = "";
}

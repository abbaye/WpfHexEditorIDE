//////////////////////////////////////////////
// Project: WpfHexEditor.Shell.Panels
// File: Panels/BookmarksPanel.xaml.cs
// Description:
//     Shows all bookmarks from the active HexEditor in a sortable ListView.
//     Double-click or Enter navigates to the bookmark offset.
//     Toolbar: Refresh, Clear All, bookmark count.
// Architecture:
//     Consumes IHexEditorService from SDK to query bookmarks.
//     NavigateRequested event raised to host for offset navigation.
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Models.Bookmarks;
using WpfHexEditor.Shell.Panels.Properties;

namespace WpfHexEditor.Shell.Panels.Panels;

/// <summary>
/// IDE panel showing all bookmarks from the active editor.
/// </summary>
public partial class BookmarksPanel : UserControl
{
    /// <summary>
    /// Raised when the user double-clicks a bookmark to navigate to its offset.
    /// The host (MainWindow) subscribes and calls HexEditor.SetPosition().
    /// </summary>
    public event Action<long>? NavigateToOffsetRequested;

    /// <summary>
    /// Raised when the user clicks Clear All to remove all bookmarks.
    /// </summary>
    public event Action? ClearAllRequested;

    /// <summary>
    /// Callback to retrieve current bookmarks from the active editor.
    /// Set by the host at panel creation time.
    /// </summary>
    public Func<IReadOnlyList<BookmarkRow>>? GetBookmarks { get; set; }

    public BookmarksPanel()
    {
        InitializeComponent();
    }

    /// <summary>Refreshes the bookmark list from the active editor.</summary>
    public void Refresh()
    {
        var bookmarks = GetBookmarks?.Invoke() ?? [];
        BookmarkList.ItemsSource = bookmarks;
        CountText.Text = $"{bookmarks.Count} bookmark{(bookmarks.Count == 1 ? "" : "s")}";
        EmptyText.Visibility = bookmarks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        BookmarkList.Visibility = bookmarks.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Clears the displayed list (e.g. when no editor is active).</summary>
    public void Clear()
    {
        BookmarkList.ItemsSource = null;
        CountText.Text = ShellPanelsResources.BookmarksPanel_EmptyCount;
        EmptyText.Visibility = Visibility.Visible;
        BookmarkList.Visibility = Visibility.Collapsed;
    }

    // ── Event handlers ───────────────────────────────────────────────────────

    private void OnRefreshClick(object sender, RoutedEventArgs e) => Refresh();

    private void OnClearAllClick(object sender, RoutedEventArgs e)
    {
        ClearAllRequested?.Invoke();
        Refresh();
    }

    private void OnBookmarkSelected(object sender, SelectionChangedEventArgs e)
    {
        if (BookmarkList.SelectedItem is BookmarkRow row)
            NavigateToOffsetRequested?.Invoke(row.Offset);
    }

    private void OnBookmarkDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (BookmarkList.SelectedItem is BookmarkRow row)
            NavigateToOffsetRequested?.Invoke(row.Offset);
    }
}

/// <summary>
/// View model row for the bookmarks ListView.
/// </summary>
public sealed class BookmarkRow
{
    public long   Offset      { get; init; }
    public string OffsetHex   { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category    { get; init; } = "Default";

    /// <summary>Creates a BookmarkRow from a Core BookMark.</summary>
    public static BookmarkRow From(BookMark bm) => new()
    {
        Offset      = bm.BytePositionInStream,
        OffsetHex   = $"0x{bm.BytePositionInStream:X8}",
        Description = bm.Description ?? string.Empty,
        Category    = bm is EnhancedBookmark eb ? eb.Category : "Default",
    };
}

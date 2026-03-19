// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: DesignHistoryPanel.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-18
// Updated: 2026-03-19 — Filter TextBox, checkpoint toggle, keyboard navigation
// Description:
//     Code-behind for the Design History dockable panel.
//     Wires ViewModel events and forwards JumpRequested to the plugin host
//     so XamlDesignerSplitHost can execute the jump.
//     Handles filter TextBox, checkpoint context menu, and Up/Down/Enter
//     keyboard navigation on the history ListView.
//
// Architecture Notes:
//     VS-Like Panel Pattern. Follows OnLoaded/OnUnloaded lifecycle rule:
//     OnUnloaded must NOT dispose _vm — only unsubscribe events.
//     Context menu items bound via Tag carry the DesignHistoryEntryViewModel
//     so Click handlers can resolve the target entry without hitting the ListView
//     SelectedItem.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Editor.XamlDesigner.ViewModels;

namespace WpfHexEditor.Editor.XamlDesigner.Panels;

/// <summary>
/// Design History dockable panel — displays the full undo/redo history,
/// allows jump-to-state, checkpoint starring, and live text filtering.
/// </summary>
public partial class DesignHistoryPanel : UserControl
{
    private readonly DesignHistoryPanelViewModel _vm = new();

    public DesignHistoryPanel()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Exposes the ViewModel for plugin wiring (Manager, JumpRequested).</summary>
    public DesignHistoryPanelViewModel ViewModel => _vm;

    /// <summary>
    /// Forwarded from <see cref="DesignHistoryPanelViewModel.JumpRequested"/>.
    /// The plugin wires this to <c>XamlDesignerSplitHost.JumpToHistoryEntry</c>.
    /// </summary>
    public event EventHandler<JumpToEntryEventArgs>? JumpRequested;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // ViewModel events (safe re-entry unsubscribe first).
        _vm.JumpRequested -= OnVmJumpRequested;
        _vm.JumpRequested += OnVmJumpRequested;

        // Filter TextBox.
        TbxFilter.TextChanged -= OnFilterTextChanged;
        TbxFilter.TextChanged += OnFilterTextChanged;
        TbxFilter.TextChanged -= OnFilterPlaceholderUpdate;
        TbxFilter.TextChanged += OnFilterPlaceholderUpdate;

        // Context menu items on the ListView rows.
        HistoryList.ContextMenuOpening -= OnHistoryListContextMenuOpening;
        HistoryList.ContextMenuOpening += OnHistoryListContextMenuOpening;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Do NOT dispose _vm — only unsubscribe (panel lifecycle rule).
        _vm.JumpRequested -= OnVmJumpRequested;

        TbxFilter.TextChanged -= OnFilterTextChanged;
        TbxFilter.TextChanged -= OnFilterPlaceholderUpdate;

        HistoryList.ContextMenuOpening -= OnHistoryListContextMenuOpening;
    }

    // ── Event forwarding ──────────────────────────────────────────────────────

    private void OnVmJumpRequested(object? sender, JumpToEntryEventArgs e)
        => JumpRequested?.Invoke(this, e);

    // ── Filter TextBox ────────────────────────────────────────────────────────

    private void OnFilterTextChanged(object sender, TextChangedEventArgs e)
        => _vm.FilterText = TbxFilter.Text;

    private void OnFilterPlaceholderUpdate(object sender, TextChangedEventArgs e)
        => TbxFilterPlaceholder.Visibility =
               string.IsNullOrEmpty(TbxFilter.Text)
                   ? Visibility.Visible
                   : Visibility.Collapsed;

    // ── ListView click handler ────────────────────────────────────────────────

    /// <summary>
    /// Handles left-click on a history list item to trigger jump-to-state.
    /// Using MouseLeftButtonUp rather than SelectionChanged to avoid firing on
    /// programmatic selection changes triggered by RebuildEntries().
    /// </summary>
    internal void OnListMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (HistoryList.SelectedItem is DesignHistoryEntryViewModel entry)
            _vm.JumpToEntryCommand.Execute(entry);
    }

    // ── Keyboard navigation ───────────────────────────────────────────────────

    /// <summary>
    /// Up/Down arrows move the selection; Enter fires jump-to-state on the selected entry.
    /// Prevents the default ListView scroll-only behavior from consuming the keys.
    /// </summary>
    internal void OnListPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                MoveListSelection(+1);
                e.Handled = true;
                break;

            case Key.Up:
                MoveListSelection(-1);
                e.Handled = true;
                break;

            case Key.Return:
                if (HistoryList.SelectedItem is DesignHistoryEntryViewModel entry)
                    _vm.JumpToEntryCommand.Execute(entry);
                e.Handled = true;
                break;
        }
    }

    private void MoveListSelection(int delta)
    {
        int count = HistoryList.Items.Count;
        if (count == 0) return;

        int current = HistoryList.SelectedIndex;
        int next    = System.Math.Clamp(current + delta, 0, count - 1);

        if (next == current) return;

        HistoryList.SelectedIndex = next;
        HistoryList.ScrollIntoView(HistoryList.SelectedItem);
    }

    // ── Context menu ──────────────────────────────────────────────────────────

    /// <summary>
    /// Wires the "Toggle Checkpoint" and "Jump to State" context menu items
    /// when the context menu opens on a row.
    /// </summary>
    private void OnHistoryListContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (e.OriginalSource is not FrameworkElement fe) return;

        var entry = ResolveEntryFromVisual(fe);
        if (entry is null) return;

        // Find the ContextMenu from the row's Grid.
        var grid = FindParentWithContextMenu(fe);
        if (grid?.ContextMenu is not ContextMenu menu) return;

        foreach (var item in menu.Items)
        {
            if (item is not MenuItem mi) continue;
            mi.Click -= OnContextMenuClick;
            mi.Click += OnContextMenuClick;
            mi.DataContext = entry;
        }
    }

    private void OnContextMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi) return;
        if (mi.DataContext is not DesignHistoryEntryViewModel entry) return;

        // Discriminate by MenuItem name (set in XAML).
        var name = mi.Name;
        if (name == "CtxToggleCheckpoint")
            _vm.ToggleCheckpointCommand.Execute(entry);
        else if (name == "CtxJumpToState")
            _vm.JumpToEntryCommand.Execute(entry);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Walks up the visual tree from <paramref name="element"/> to find the nearest
    /// DataContext that is a <see cref="DesignHistoryEntryViewModel"/>.
    /// </summary>
    private static DesignHistoryEntryViewModel? ResolveEntryFromVisual(DependencyObject element)
    {
        var current = element;
        while (current is not null)
        {
            if (current is FrameworkElement fe &&
                fe.DataContext is DesignHistoryEntryViewModel vm)
                return vm;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    /// <summary>
    /// Walks up the visual tree to find the first <see cref="FrameworkElement"/>
    /// with a non-null ContextMenu.
    /// </summary>
    private static FrameworkElement? FindParentWithContextMenu(DependencyObject element)
    {
        var current = element;
        while (current is not null)
        {
            if (current is FrameworkElement fe && fe.ContextMenu is not null)
                return fe;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}

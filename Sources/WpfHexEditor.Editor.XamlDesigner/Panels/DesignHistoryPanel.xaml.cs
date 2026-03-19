// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: DesignHistoryPanel.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-18
// Description:
//     Code-behind for the Design History dockable panel.
//     Wires ViewModel events and forwards JumpRequested to the plugin host
//     so XamlDesignerSplitHost can execute the jump.
//
// Architecture Notes:
//     VS-Like Panel Pattern. Follows OnLoaded/OnUnloaded lifecycle rule:
//     OnUnloaded must NOT dispose _vm — only unsubscribe events.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Editor.XamlDesigner.ViewModels;

namespace WpfHexEditor.Editor.XamlDesigner.Panels;

/// <summary>
/// Design History dockable panel — displays the full undo/redo history
/// and allows jump-to-state by clicking an entry.
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
        _vm.JumpRequested -= OnVmJumpRequested;
        _vm.JumpRequested += OnVmJumpRequested;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Do NOT dispose _vm — only unsubscribe (panel lifecycle rule).
        _vm.JumpRequested -= OnVmJumpRequested;
    }

    // ── Event forwarding ──────────────────────────────────────────────────────

    private void OnVmJumpRequested(object? sender, JumpToEntryEventArgs e)
        => JumpRequested?.Invoke(this, e);

    // ── ListView click handler ────────────────────────────────────────────────

    /// <summary>
    /// Handles left-click on a history list item to trigger jump-to-state.
    /// Using MouseLeftButtonUp rather than SelectionChanged to avoid firing on
    /// programmatic selection changes triggered by RebuildEntries().
    /// </summary>
    private void OnListMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (HistoryList.SelectedItem is DesignHistoryEntryViewModel entry)
            _vm.JumpToEntryCommand.Execute(entry);
    }
}

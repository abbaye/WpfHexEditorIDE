// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: ResourceBrowserPanel.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Updated: 2026-03-19
// Description:
//     Code-behind for the Resource Browser dockable panel.
//     Wires ViewModel events and exposes FindUsagesRequested /
//     GoToDefinitionRequested for plugin wiring.
//     Added: F2 key handler, inline rename commit/cancel,
//            copy value handler, GoToDefinition relay.
//
// Architecture Notes:
//     VS-Like Panel Pattern. Follows OnLoaded/OnUnloaded lifecycle rule.
//     Never nulls _vm in OnUnloaded.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Editor.XamlDesigner.ViewModels;

namespace WpfHexEditor.Editor.XamlDesigner.Panels;

/// <summary>
/// Resource Browser dockable panel — shows all application resources.
/// </summary>
public partial class ResourceBrowserPanel : UserControl
{
    private ResourceBrowserPanelViewModel _vm = new();

    // ── Constructor ───────────────────────────────────────────────────────────

    public ResourceBrowserPanel()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public ResourceBrowserPanelViewModel ViewModel => _vm;

    /// <summary>Raised when the user requests Find Usages on a resource.</summary>
    public event EventHandler<string>? FindUsagesRequested;

    /// <summary>Raised when the user requests Go to Definition on a resource.</summary>
    public event EventHandler<(string key, int line)>? GoToDefinitionRequested;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm.FindUsagesRequested      -= OnFindUsages;
        _vm.FindUsagesRequested      += OnFindUsages;
        _vm.GoToDefinitionRequested  -= OnGoToDefinition;
        _vm.GoToDefinitionRequested  += OnGoToDefinition;

        if (_vm.EntriesView.IsEmpty)
            _vm.Scan();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // IMPORTANT: do NOT null _vm — panel may reload after dock/float.
        _vm.FindUsagesRequested      -= OnFindUsages;
        _vm.GoToDefinitionRequested  -= OnGoToDefinition;
    }

    // ── VM event relay ────────────────────────────────────────────────────────

    private void OnFindUsages(object? sender, string key)
        => FindUsagesRequested?.Invoke(this, key);

    private void OnGoToDefinition(object? sender, (string key, int line) args)
        => GoToDefinitionRequested?.Invoke(this, args);

    // ── Keyboard handling ─────────────────────────────────────────────────────

    private void OnListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.F2) return;
        if (_vm.SelectedEntry is { } entry)
            _vm.RenameCommand.Execute(entry);
        e.Handled = true;
    }

    // ── Inline edit handlers ──────────────────────────────────────────────────

    private void OnKeyEditLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: ResourceEntryViewModel entry })
            entry.CommitRename();
    }

    private void OnKeyEditKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: ResourceEntryViewModel entry }) return;

        if (e.Key == Key.Return) { entry.CommitRename(); e.Handled = true; }
        if (e.Key == Key.Escape) { entry.IsEditing = false; e.Handled = true; }
    }

    // ── Copy value handler ────────────────────────────────────────────────────

    private void OnCopyValueClick(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedEntry?.PreviewText is { Length: > 0 } text)
            System.Windows.Clipboard.SetText(text);
    }

    // ── Sort / scope toolbar handlers ─────────────────────────────────────────

    private void OnSortByClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn) btn.ContextMenu!.IsOpen = true;
    }

    private void OnSortModeSelected(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi)
            _vm.SortMode = mi.Tag?.ToString() ?? "Name";
    }

    private void OnScopeFilterClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn) btn.ContextMenu!.IsOpen = true;
    }

    private void OnScopeSelected(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi)
            _vm.ScopeFilter = mi.Tag?.ToString() ?? "All";
    }
}

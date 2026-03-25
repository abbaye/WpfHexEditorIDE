// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Controls/AssemblyTreeView.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Code-behind for the AssemblyTreeView composite control.
//     Forwards SelectedItemChanged as a public event (NodeSelected).
//     Handles context menu item clicks, delegating to the parent panel.
// ==========================================================

using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.Core.AssemblyAnalysis.Languages;
using WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Controls;

/// <summary>
/// Custom TreeView wrapper for the Assembly Explorer.
/// Raises <see cref="NodeSelected"/> when the selected item changes.
/// Context menu items delegate via events back to the hosting panel.
/// </summary>
public partial class AssemblyTreeView : UserControl
{
    // ── Events ────────────────────────────────────────────────────────────────

    public event EventHandler<AssemblyNodeViewModel>?  NodeSelected;
    public event EventHandler<AssemblyNodeViewModel>?  OpenInHexEditorRequested;
    public event EventHandler<AssemblyNodeViewModel>?  HighlightInHexEditorRequested;

    /// <summary>
    /// Raised when a language is selected from the "Decompile to…" submenu.
    /// Carries the target node and the chosen language identifier.
    /// </summary>
    public event EventHandler<(AssemblyNodeViewModel Node, string LanguageId)>? DecompileWithLanguageRequested;

    /// <summary>
    /// Backward-compatible alias — fires <see cref="DecompileWithLanguageRequested"/>
    /// with <c>LanguageId = "CSharp"</c>.  Used by "Go to Definition".
    /// </summary>
    public event EventHandler<AssemblyNodeViewModel>?  DecompileRequested;

    public event EventHandler<AssemblyNodeViewModel>?  CopyNameRequested;
    public event EventHandler<AssemblyNodeViewModel>?  CopyFullNameRequested;
    public event EventHandler<AssemblyNodeViewModel>?  CopyOffsetRequested;
    public event EventHandler<AssemblyNodeViewModel>?  CloseAssemblyRequested;
    public event EventHandler?                         CollapseAllRequested;
    public event EventHandler?                         CloseAllAssembliesRequested;
    public event EventHandler<AssemblyNodeViewModel>?  PinAssemblyRequested;
    public event EventHandler<AssemblyNodeViewModel>?  CompareWithRequested;
    public event EventHandler<AssemblyNodeViewModel>?  ExtractToProjectRequested;
    public event EventHandler<AssemblyNodeViewModel>?  ExportProjectRequested;
    public event EventHandler<AssemblyNodeViewModel>?  ShowInMetadataTablesRequested;
    public event EventHandler<AssemblyNodeViewModel>?  ExportILRequested;
    public event EventHandler<AssemblyNodeViewModel>?  ExportCSharpRequested;

    // ── ItemsSource passthrough ───────────────────────────────────────────────

    public IEnumerable? ItemsSource
    {
        get => InnerTreeView.ItemsSource;
        set => InnerTreeView.ItemsSource = value;
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    public AssemblyTreeView()
        => InitializeComponent();

    // ── Selection ─────────────────────────────────────────────────────────────

    private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is AssemblyNodeViewModel node)
            NodeSelected?.Invoke(this, node);
    }

    // ── Context menu ─────────────────────────────────────────────────────────

    private void OnContextMenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu) return;

        var node = InnerTreeView.SelectedItem as AssemblyNodeViewModel;
        var isRoot = node is AssemblyRootNodeViewModel;

        // "Highlight in Hex Editor" — requires a resolved PE offset.
        if (FindMenuItemByName(menu, "MenuHighlightInHex") is MenuItem menuHighlight)
            menuHighlight.IsEnabled = node?.PeOffset > 0;

        // "Open Assembly File in Hex Editor" — available for any node; uses OwnerFilePath.
        if (FindMenuItemByName(menu, "MenuOpenInHex") is MenuItem menuOpenInHex)
            menuOpenInHex.IsEnabled = node is not null;

        if (FindMenuItemByName(menu, "MenuCopyFull") is MenuItem menuCopyFull)
            menuCopyFull.IsEnabled = node is TypeNodeViewModel;

        if (FindMenuItemByName(menu, "MenuCopyOffset") is MenuItem menuCopyOffset)
            menuCopyOffset.IsEnabled = node is MethodNodeViewModel;

        // Rebuild cascading "Decompile to…" submenu dynamically (ADR-ASM-03).
        RebuildDecompileSubmenu(menu, node);

        if (FindMenuItemByName(menu, "MenuExtractToProject") is MenuItem menuExtract)
            menuExtract.IsEnabled = node is TypeNodeViewModel or MethodNodeViewModel or AssemblyRootNodeViewModel;

        // "Pin Assembly" — root nodes only; update header to reflect current pin state.
        if (FindMenuItemByName(menu, "MenuPin") is MenuItem menuPin)
        {
            menuPin.IsEnabled = isRoot;
            menuPin.Header    = isRoot && node is AssemblyRootNodeViewModel root
                ? (root.IsPinned ? "Unpin Assembly" : "Pin Assembly")
                : "Pin Assembly";
        }

        // "Compare with…" — root nodes only (two assemblies needed).
        if (FindMenuItemByName(menu, "MenuCompareWith") is MenuItem menuCompare)
            menuCompare.IsEnabled = isRoot;

        if (FindMenuItemByName(menu, "MenuCloseAssembly") is MenuItem menuCloseAssembly)
            menuCloseAssembly.IsEnabled = node is not null;

        if (FindMenuItemByName(menu, "MenuGoToDefinition") is MenuItem menuGoToDef)
            menuGoToDef.IsEnabled = node is TypeNodeViewModel or MethodNodeViewModel;

        if (FindMenuItemByName(menu, "MenuShowInMetadata") is MenuItem menuShowMeta)
            menuShowMeta.IsEnabled = node is TypeNodeViewModel or MethodNodeViewModel or FieldNodeViewModel;

        if (FindMenuItemByName(menu, "MenuExportIL") is MenuItem menuExportIL)
            menuExportIL.IsEnabled = node is TypeNodeViewModel or MethodNodeViewModel;

        if (FindMenuItemByName(menu, "MenuExportCSharp") is MenuItem menuExportCs)
            menuExportCs.IsEnabled = node is TypeNodeViewModel or MethodNodeViewModel;

        // "Export as C# Project…" — root nodes only (ASM-02-F).
        EnsureExportProjectMenuItem(menu, isRoot);
    }

    private static MenuItem? FindMenuItemByName(ContextMenu menu, string name)
    {
        foreach (var item in menu.Items)
        {
            if (item is MenuItem menuItem && menuItem.Name == name)
                return menuItem;
        }
        return null;
    }

    private void OnHighlightInHexEditor(object sender, RoutedEventArgs e)
    {
        if (InnerTreeView.SelectedItem is AssemblyNodeViewModel node)
            HighlightInHexEditorRequested?.Invoke(this, node);
    }

    private void OnOpenInHexEditor(object sender, RoutedEventArgs e)
    {
        if (InnerTreeView.SelectedItem is AssemblyNodeViewModel node)
            OpenInHexEditorRequested?.Invoke(this, node);
    }

    private void OnCopyName(object sender, RoutedEventArgs e)
    {
        if (InnerTreeView.SelectedItem is AssemblyNodeViewModel node)
            CopyNameRequested?.Invoke(this, node);
    }

    private void OnCopyFullName(object sender, RoutedEventArgs e)
    {
        if (InnerTreeView.SelectedItem is AssemblyNodeViewModel node)
            CopyFullNameRequested?.Invoke(this, node);
    }

    private void OnCopyOffset(object sender, RoutedEventArgs e)
    {
        if (InnerTreeView.SelectedItem is AssemblyNodeViewModel node)
            CopyOffsetRequested?.Invoke(this, node);
    }

    private void OnDecompile(object sender, RoutedEventArgs e)
    {
        // Fires backward-compat event (Go to Definition path — always C#).
        if (InnerTreeView.SelectedItem is AssemblyNodeViewModel node)
            DecompileRequested?.Invoke(this, node);
    }

    // ── Decompile submenu ────────────────────────────────────────────────────

    /// <summary>Active language injected from the panel so the checkmark stays in sync.</summary>
    private string? _activeLanguageId;

    /// <summary>Called by the hosting panel after construction and on every language change.</summary>
    public void SetActiveLanguageId(string languageId) => _activeLanguageId = languageId;

    /// <summary>
    /// Clears and rebuilds the items inside the <c>MenuDecompile</c> submenu host
    /// from <see cref="DecompilationLanguageRegistry.All"/> every time the context menu opens.
    /// This ensures any language registered after startup automatically appears.
    /// </summary>
    private void RebuildDecompileSubmenu(ContextMenu menu, AssemblyNodeViewModel? node)
    {
        if (FindMenuItemByName(menu, "MenuDecompile") is not MenuItem parent) return;

        var isDecompilable   = node is TypeNodeViewModel or MethodNodeViewModel or AssemblyRootNodeViewModel;
        var isIlCapable      = node is TypeNodeViewModel or MethodNodeViewModel;
        var activeLanguageId = _activeLanguageId ?? "CSharp";

        parent.IsEnabled = isDecompilable;
        parent.Items.Clear();

        foreach (var lang in DecompilationLanguageRegistry.All)
        {
            var langId  = lang.Id;
            var enabled = string.Equals(langId, "IL", StringComparison.OrdinalIgnoreCase)
                              ? isIlCapable
                              : isDecompilable;

            var item = new MenuItem
            {
                Header    = lang.DisplayName,
                IsEnabled = enabled,
                IsChecked = string.Equals(langId, activeLanguageId, StringComparison.OrdinalIgnoreCase),
                Icon      = MakeGlyphIcon(lang.GlyphCode),
                Foreground = menu.Foreground,
            };
            item.Click += (_, _) =>
            {
                if (InnerTreeView.SelectedItem is AssemblyNodeViewModel n)
                    DecompileWithLanguageRequested?.Invoke(this, (n, langId));
            };
            parent.Items.Add(item);
        }
    }

    private static UIElement MakeGlyphIcon(string glyph)
        => new TextBlock
        {
            Text       = glyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize   = 12,
        };

    private void OnCloseAssembly(object sender, RoutedEventArgs e)
    {
        if (InnerTreeView.SelectedItem is AssemblyNodeViewModel node)
            CloseAssemblyRequested?.Invoke(this, node);
    }

    private void OnPinAssembly(object sender, RoutedEventArgs e)
    {
        if (InnerTreeView.SelectedItem is AssemblyNodeViewModel node)
            PinAssemblyRequested?.Invoke(this, node);
    }

    private void OnCompareWith(object sender, RoutedEventArgs e)
    {
        if (InnerTreeView.SelectedItem is AssemblyNodeViewModel node)
            CompareWithRequested?.Invoke(this, node);
    }

    private void OnExtractToProject(object sender, RoutedEventArgs e)
    {
        if (InnerTreeView.SelectedItem is AssemblyNodeViewModel node)
            ExtractToProjectRequested?.Invoke(this, node);
    }

    private void OnCollapseAll(object sender, RoutedEventArgs e)
        => CollapseAllRequested?.Invoke(this, EventArgs.Empty);

    private void OnCloseAllAssemblies(object sender, RoutedEventArgs e)
        => CloseAllAssembliesRequested?.Invoke(this, EventArgs.Empty);

    private void OnExportProject(object sender, RoutedEventArgs e)
    {
        if (InnerTreeView.SelectedItem is AssemblyNodeViewModel node)
            ExportProjectRequested?.Invoke(this, node);
    }

    private void OnGoToDefinition(object sender, RoutedEventArgs e)
    {
        // Reuses the existing decompile path — opens the C# view for the type/method.
        if (InnerTreeView.SelectedItem is AssemblyNodeViewModel node)
            DecompileRequested?.Invoke(this, node);
    }

    private void OnShowInMetadataTables(object sender, RoutedEventArgs e)
    {
        if (InnerTreeView.SelectedItem is AssemblyNodeViewModel node)
            ShowInMetadataTablesRequested?.Invoke(this, node);
    }

    private void OnExportIL(object sender, RoutedEventArgs e)
    {
        if (InnerTreeView.SelectedItem is AssemblyNodeViewModel node)
            ExportILRequested?.Invoke(this, node);
    }

    private void OnExportCSharp(object sender, RoutedEventArgs e)
    {
        if (InnerTreeView.SelectedItem is AssemblyNodeViewModel node)
            ExportCSharpRequested?.Invoke(this, node);
    }

    /// <summary>
    /// Adds the "Export as C# Project…" item to the context menu at runtime
    /// if it is not already present. Called from <see cref="OnContextMenuOpened"/>.
    /// </summary>
    private void EnsureExportProjectMenuItem(ContextMenu menu, bool isRoot)
    {
        const string exportMenuName = "MenuExportProject";
        if (FindMenuItemByName(menu, exportMenuName) is MenuItem existing)
        {
            existing.IsEnabled = isRoot;
            return;
        }

        // Dynamically add the menu item after the separator.
        var sep = new Separator();
        menu.Items.Add(sep);

        var item = new MenuItem
        {
            Name      = exportMenuName,
            Header    = "Export as C# Project…",
            IsEnabled = isRoot
        };
        item.Click += OnExportProject;
        menu.Items.Add(item);
    }
}

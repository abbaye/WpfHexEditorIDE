// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Views/AssemblyExplorerPanel.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Code-behind for the main Assembly Explorer panel.
//     Initializes ToolbarOverflowManager with 6 collapsible groups.
//     Wires tree events to the ViewModel. Handles context menu actions.
//     Exposes SetContext() for injection and ApplyOptions() for settings reload.
//
// Architecture Notes:
//     Theme: all brushes via DynamicResource (PFP_* tokens).
//     Pattern: MVVM — code-behind is thin, delegates to ViewModel.
//     ToolbarOverflowManager group collapse order (index 0 = first):
//       TbgDecompile, TbgVisibility, TbgSort, TbgSync, TbgExpandCollapse, TbgFilter
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WpfHexEditor.Core.AssemblyAnalysis.Services;
using IAssemblyAnalysisEngine = WpfHexEditor.Core.AssemblyAnalysis.Services.IAssemblyAnalysisEngine;
using WpfHexEditor.Plugins.AssemblyExplorer.Options;
using WpfHexEditor.Plugins.AssemblyExplorer.Services;
using WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.UI;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Views;

/// <summary>
/// Main VS-Like dockable panel for the Assembly Explorer plugin.
/// </summary>
public partial class AssemblyExplorerPanel : UserControl
{
    private ToolbarOverflowManager? _overflowManager;

    // ── Constructor ───────────────────────────────────────────────────────────

    public AssemblyExplorerPanel(
        IAssemblyAnalysisEngine                  analysisEngine,
        DecompilerService                        decompiler,
        SDK.Contracts.Services.IHexEditorService hexEditor,
        SDK.Contracts.Services.IOutputService    output,
        IPluginEventBus                          eventBus)
    {
        InitializeComponent();

        ViewModel = new AssemblyExplorerViewModel(
            analysisEngine, decompiler, hexEditor, output);

        DataContext            = ViewModel;
        DetailPane.DataContext = ViewModel.DetailViewModel;

        // Wire tree ItemsSource
        MainTreeView.ItemsSource = ViewModel.RootNodes;

        // Wire EventBus publishing from ViewModel events
        ViewModel.AssemblyLoaded += (_, evt) => eventBus.Publish(evt);
        ViewModel.MemberSelected += (_, evt) => eventBus.Publish(evt);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public AssemblyExplorerViewModel ViewModel { get; }

    /// <summary>
    /// Called by the plugin entry point to inject the IDE host context
    /// for theme registration.
    /// </summary>
    public void SetContext(IIDEHostContext context)
    {
        context.Theme.RegisterThemeAwareControl(this);
        Unloaded += (_, _) => context.Theme.UnregisterThemeAwareControl(this);
    }

    /// <summary>
    /// Re-applies options from AssemblyExplorerOptions.Instance.
    /// Called by plugin after SaveOptions().
    /// </summary>
    public void ApplyOptions()
    {
        var opts = AssemblyExplorerOptions.Instance;
        ViewModel.SyncWithHexEditor = opts.AutoSyncWithHexEditor;
        ViewModel.ShowResources     = opts.ShowResources;
        ViewModel.ShowMetadata      = opts.ShowMetadataTables;

        // Font size applied to detail pane TextBox
        DetailPane.FontSize = opts.DecompilerFontSize;
    }

    // ── Loaded ────────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyOptions();

        _overflowManager = new ToolbarOverflowManager(
            toolbarContainer:      ToolbarBorder,
            alwaysVisiblePanel:    ToolbarRightPanel,
            overflowButton:        ToolbarOverflowButton,
            overflowMenu:          OverflowContextMenu,
            groupsInCollapseOrder: new FrameworkElement[]
            {
                TbgDecompile,       // [0] first to collapse — stub, lowest priority
                TbgVisibility,      // [1]
                TbgSort,            // [2]
                TbgSync,            // [3]
                TbgExpandCollapse,  // [4]
                TbgFilter           // [5] last to collapse — most important
            });

        // Capture natural widths after layout pass so the overflow manager
        // knows when each group overflows.
        Dispatcher.InvokeAsync(
            _overflowManager.CaptureNaturalWidths,
            DispatcherPriority.Loaded);
    }

    // ── Open Assembly dialog ──────────────────────────────────────────────────

    private void OnOpenAssemblyClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenAssemblyDialog
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedFilePath))
            _ = ViewModel.LoadAssemblyAsync(dialog.SelectedFilePath);
    }

    // ── Toolbar ───────────────────────────────────────────────────────────────

    private void OnToolbarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged)
            _overflowManager?.Update();
    }

    private void OnOverflowButtonClick(object sender, RoutedEventArgs e)
        => OverflowContextMenu.IsOpen = true;

    private void OnOverflowMenuOpened(object sender, RoutedEventArgs e)
        => _overflowManager?.SyncMenuVisibility();

    // ── Tree event handlers ───────────────────────────────────────────────────

    private void OnTreeNodeSelected(object? sender, AssemblyNodeViewModel node)
        => ViewModel.SelectedNode = node;

    private void OnOpenInHexEditor(object? sender, AssemblyNodeViewModel node)
    {
        if (node.PeOffset <= 0) return;
        // Directly trigger navigation — ViewModel.OnNodeSelected already does sync.
        // This handler covers the context menu path (user didn't click the row).
        ViewModel.OnNodeSelected(node);
    }

    private void OnDecompile(object? sender, AssemblyNodeViewModel node)
        => ViewModel.DetailViewModel.ShowNode(node);

    private void OnCopyName(object? sender, AssemblyNodeViewModel node)
        => SafeCopy(node.DisplayName);

    private void OnCopyFullName(object? sender, AssemblyNodeViewModel node)
    {
        var text = node is TypeNodeViewModel t ? t.Model.FullName : node.DisplayName;
        SafeCopy(text);
    }

    private void OnCopyOffset(object? sender, AssemblyNodeViewModel node)
        => SafeCopy(node.PeOffset > 0 ? $"0x{node.PeOffset:X}" : "0");

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SafeCopy(string text)
    {
        try   { Clipboard.SetText(text); }
        catch { /* Clipboard unavailable — silently ignore */ }
    }
}

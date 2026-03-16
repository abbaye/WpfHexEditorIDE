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

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WpfHexEditor.Core.AssemblyAnalysis.Services;
using IAssemblyAnalysisEngine = WpfHexEditor.Core.AssemblyAnalysis.Services.IAssemblyAnalysisEngine;
using WpfHexEditor.Plugins.AssemblyExplorer.Options;
using WpfHexEditor.Plugins.AssemblyExplorer.Services;
using WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;
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
        IAssemblyAnalysisEngine analysisEngine,
        IDecompilerBackend      decompilerBackend,
        DecompilerService       decompiler,
        IHexEditorService       hexEditor,
        IDocumentHostService?   documentHost,
        IOutputService          output,
        IPluginEventBus         eventBus,
        IUIRegistry             uiRegistry,
        string                  pluginId)
    {
        InitializeComponent();

        ViewModel = new AssemblyExplorerViewModel(
            analysisEngine, decompilerBackend, decompiler,
            hexEditor, documentHost, output, uiRegistry, pluginId);

        DataContext            = ViewModel;
        DetailPane.DataContext = ViewModel.DetailViewModel;

        // Wire tree ItemsSource
        MainTreeView.ItemsSource = ViewModel.RootNodes;

        // Wire EventBus publishing from ViewModel events
        ViewModel.AssemblyLoaded += (_, evt) => eventBus.Publish(evt);
        ViewModel.MemberSelected += (_, evt) => eventBus.Publish(evt);

        // Wire new v2.0 tree events
        MainTreeView.HighlightInHexEditorRequested += OnHighlightInHexEditor;
        MainTreeView.PinAssemblyRequested          += OnPinAssembly;
        MainTreeView.CompareWithRequested          += OnCompareWith;
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

    // ── Open Assembly dialog + panel-level Ctrl+V ─────────────────────────────

    private void OnOpenAssemblyClick(object sender, RoutedEventArgs e)
        => OpenAssemblyViaDialog();

    /// <summary>
    /// Panel-level Ctrl+V: if the clipboard holds a valid .dll/.exe path, load it
    /// directly without opening the dialog. Otherwise open the dialog pre-filled.
    /// </summary>
    private void OnPanelKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.V || Keyboard.Modifiers != ModifierKeys.Control) return;

        var path = TryGetPathFromClipboard();
        if (path is null) return;

        e.Handled = true;

        if (File.Exists(path))
        {
            // Valid existing path — load immediately, no dialog needed.
            _ = ViewModel.LoadAssemblyAsync(path);
        }
        else
        {
            // Clipboard text doesn't point to an existing file — open dialog pre-filled.
            var dialog = new OpenAssemblyDialog { Owner = Window.GetWindow(this) };
            dialog.PreFillPath(path);
            if (dialog.ShowDialog() == true)
                foreach (var p in dialog.SelectedFilePaths)
                    _ = ViewModel.LoadAssemblyAsync(p);
        }
    }

    private void OpenAssemblyViaDialog()
    {
        var dialog = new OpenAssemblyDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true) return;

        foreach (var path in dialog.SelectedFilePaths)
            _ = ViewModel.LoadAssemblyAsync(path);
    }

    /// <summary>
    /// Extracts a .dll/.exe file path from the clipboard.
    /// Returns null if no usable path is found.
    /// Priority: Explorer FileDrop > plain text.
    /// </summary>
    private static string? TryGetPathFromClipboard()
    {
        if (Clipboard.ContainsFileDropList())
        {
            var list = Clipboard.GetFileDropList();
            foreach (string? entry in list)
            {
                if (entry is null) continue;
                var ext = Path.GetExtension(entry).ToLowerInvariant();
                if (ext is ".dll" or ".exe") return entry;
            }
        }

        if (Clipboard.ContainsText())
        {
            var text = Clipboard.GetText().Trim().Trim('"');
            if (!string.IsNullOrEmpty(text)) return text;
        }

        return null;
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
        // "Open Assembly File in Hex Editor" — opens the raw .dll bytes without navigating.
        var filePath = node.OwnerFilePath;
        if (!string.IsNullOrEmpty(filePath))
            ViewModel.OpenAssemblyFileInHexEditor(filePath);
    }

    private void OnDecompile(object? sender, AssemblyNodeViewModel node)
        => ViewModel.DetailViewModel.ShowNode(node, node.OwnerFilePath ?? string.Empty);

    private void OnCopyName(object? sender, AssemblyNodeViewModel node)
        => SafeCopy(node.DisplayName);

    private void OnCopyFullName(object? sender, AssemblyNodeViewModel node)
    {
        var text = node is TypeNodeViewModel t ? t.Model.FullName : node.DisplayName;
        SafeCopy(text);
    }

    private void OnCopyOffset(object? sender, AssemblyNodeViewModel node)
        => SafeCopy(node.PeOffset > 0 ? $"0x{node.PeOffset:X}" : "0");

    private void OnCloseAssembly(object? sender, AssemblyNodeViewModel node)
        => ViewModel.CloseAssembly(node);

    // ── v2.0 tree event handlers ──────────────────────────────────────────────

    private void OnHighlightInHexEditor(object? sender, AssemblyNodeViewModel node)
        => _ = ViewModel.OpenMemberInHexEditorAsync(node);

    private void OnPinAssembly(object? sender, AssemblyNodeViewModel node)
        => ViewModel.PinAssemblyCommand.Execute(node);

    private void OnCompareWith(object? sender, AssemblyNodeViewModel node)
    {
        if (_diffPanel is null) return;
        _diffPanel.PreSelectBaseline(node.DisplayName.Split(' ')[0]); // strip version suffix
        _diffPanelShowAction?.Invoke();
    }

    // ── Diff panel reference (set by plugin entry point) ─────────────────────

    private AssemblyDiffPanel? _diffPanel;
    private Action?            _diffPanelShowAction;

    /// <summary>
    /// Called by the plugin entry point after the diff panel is registered,
    /// so "Compare with…" can pre-select the baseline and open the diff panel.
    /// </summary>
    public void SetDiffPanel(AssemblyDiffPanel diffPanel, Action showDiffPanel)
    {
        _diffPanel           = diffPanel;
        _diffPanelShowAction = showDiffPanel;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SafeCopy(string text)
    {
        try   { Clipboard.SetText(text); }
        catch { /* Clipboard unavailable — silently ignore */ }
    }
}

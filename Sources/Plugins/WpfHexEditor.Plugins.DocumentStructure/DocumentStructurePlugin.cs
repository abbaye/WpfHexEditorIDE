// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentStructure
// File: DocumentStructurePlugin.cs
// Created: 2026-04-05
// Description:
//     Plugin entry point for the Document Structure / Outline panel.
//     Wires focus tracking, LSP events, caret movement, and hex editor events
//     to maintain a live hierarchical view of the active document's structure.
//
// Architecture Notes:
//     Follows ParsedFieldsPlugin pattern: _hexEditorHandledLastSwitch for focus
//     coordination, lazy rendering via PanelShown/PanelHidden, debounced refresh.
//     Provider chain: LSP (1000) > SourceOutline (500) > Language-specific (300) > Folding (100).
// ==========================================================

using System.IO;
using WpfHexEditor.Core.DocumentStructure;
using WpfHexEditor.Core.Options;
using WpfHexEditor.Core.DocumentStructure.Providers;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.Events.IDEEvents;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.SourceAnalysis.Services;
using WpfHexEditor.Plugins.DocumentStructure.Commands;
using WpfHexEditor.Plugins.DocumentStructure.ViewModels;
using WpfHexEditor.Plugins.DocumentStructure.Views;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Focus;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Events;
using WpfHexEditor.SDK.ExtensionPoints.XamlDesigner;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.Plugins.DocumentStructure.Properties;

namespace WpfHexEditor.Plugins.DocumentStructure;

public sealed class DocumentStructurePlugin : IWpfHexEditorPlugin
{
    private const string PanelUiId = "WpfHexEditor.Plugins.DocumentStructure.Panel";

    public string  Id      => "WpfHexEditor.Plugins.DocumentStructure";
    public string  Name    => DocumentStructureResources.DocStructure_PluginName;
    public Version Version => new(0, 1, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor          = true,
        AccessFileSystem         = false,
        RegisterMenus            = true,
        WriteOutput              = true,
        RegisterTerminalCommands = true,
    };

    private IIDEHostContext?               _context;
    private DocumentStructurePanel?        _panel;
    private DocumentStructureViewModel?    _vm;
    private DocumentStructureProviderResolver? _resolver;

    private IDisposable?  _lspSymbolSub;
    private IDisposable?  _cursorMovedSub;
    private IDisposable?  _refreshSub;
    private IDisposable?  _pluginLoadedSub;

    // XAML Designer integration (optional — null when XamlDesignerPlugin is not loaded).
    private IXamlDesignerService? _designerService;

    private bool _isPanelVisible = true;
    private bool _hexEditorHandledLastSwitch;
    private bool _isNavigating;           // suppresses focus-change refresh caused by our own navigation
    private string? _lastTrackedFilePath;

    // ── Pending update when panel is hidden ──────────────────────────────────
    private (string? filePath, string? docType, string? language)? _pendingRefresh;

    // ── Temp files for virtual/decompiled documents (no real file path) ──────
    private readonly Dictionary<string, string> _virtualTempFiles = new(StringComparer.OrdinalIgnoreCase);

    // ══════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ══════════════════════════════════════════════════════════════════════════

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;

        // ── Build provider resolver ──────────────────────────────────────
        _resolver = new DocumentStructureProviderResolver();
        _resolver.Register(new LspDocumentStructureProvider(
            context.LspServers,
            () =>
            {
                var sln = context.SolutionManager?.CurrentSolution;
                return sln is not null ? System.IO.Path.GetDirectoryName(sln.FilePath) : null;
            }));
        _resolver.Register(new SourceOutlineStructureProvider(new SourceOutlineEngine()));
        _resolver.Register(new MarkdownStructureProvider());
        _resolver.Register(new JsonStructureProvider());
        _resolver.Register(new XmlStructureProvider());
        _resolver.Register(new BinaryFormatStructureProvider());
        _resolver.Register(new IniStructureProvider());
        _resolver.Register(new FoldingRegionStructureProvider());

        // ── XAML Designer integration (optional) ─────────────────────────
        var designerSvc = context.ExtensionRegistry
            .GetExtensions<IXamlDesignerService>().FirstOrDefault();
        if (designerSvc is not null)
            WireDesignerService(designerSvc);

        // Late registration: XamlDesignerPlugin may load after us.
        _pluginLoadedSub = context.IDEEvents.Subscribe<PluginLoadedEvent>(evt =>
        {
            if (evt.PluginId != "WpfHexEditor.Plugins.XamlDesigner") return;
            if (_designerService is not null) return; // already wired
            var svc = context.ExtensionRegistry
                .GetExtensions<IXamlDesignerService>().FirstOrDefault();
            if (svc is not null) WireDesignerService(svc);
        });

        // ── Create ViewModel + Panel ─────────────────────────────────────
        _vm = new DocumentStructureViewModel(_resolver)
        {
            Logger        = msg => context.Output.Write("DocStructure", msg),
            MaxDepthIndex = AppSettingsService.Instance.Current.DocumentStructure.MaxDepthIndex,
        };
        _panel = new DocumentStructurePanel { DataContext = _vm };

        // Persist max-depth changes to AppSettings.
        _vm.MaxDepthChanged += (_, idx) =>
        {
            AppSettingsService.Instance.Current.DocumentStructure.MaxDepthIndex = idx;
            AppSettingsService.Instance.Save();
        };

        // Wire navigate requests
        _vm.NavigateRequested += OnNavigateRequested;
        _panel.RefreshRequested += OnPanelRefreshRequested;

        // ── Register panel ───────────────────────────────────────────────
        context.UIRegistry.RegisterPanel(PanelUiId, _panel, Id, new PanelDescriptor
        {
            Title           = DocumentStructureResources.DocStructure_PluginName,
            DefaultDockSide = "Left",
            DefaultAutoHide = false,
            CanClose        = true,
            PreferredWidth  = 280,
            Category        = "Analysis",
        });

        // ── View menu item ───────────────────────────────────────────────
        context.UIRegistry.RegisterMenuItem($"{Id}.Menu.Show", Id, new MenuItemDescriptor
        {
            Header     = "Document _Structure",
            ParentPath = "View",
            Group      = "Analysis",
            IconGlyph  = "\uE8A1",
            Command    = new RelayCommand(_ => context.UIRegistry.ShowPanel(PanelUiId)),
        });

        // ── Panel visibility tracking (lazy rendering) ───────────────────
        _isPanelVisible = context.UIRegistry.IsPanelVisible(PanelUiId);
        context.UIRegistry.PanelShown  += OnPanelShown;
        context.UIRegistry.PanelHidden += OnPanelHidden;

        // ── Focus/editor change events ───────────────────────────────────
        context.FocusContext.FocusChanged           += OnFocusChanged;
        context.HexEditor.ActiveEditorChanged       += OnActiveEditorChanged;
        context.HexEditor.FormatDetected            += OnFormatDetected;
        context.CodeEditor.DocumentChanged          += OnCodeEditorDocumentChanged;

        // ── IDE events ───────────────────────────────────────────────────
        _lspSymbolSub  = context.IDEEvents.Subscribe<LspSymbolTableUpdatedEvent>(OnLspSymbolsUpdated);
        _cursorMovedSub = context.IDEEvents.Subscribe<CodeEditorCursorMovedEvent>(OnCursorMoved);

        // ── Plugin EventBus: manual refresh request ──────────────────────
        _refreshSub = context.EventBus.Subscribe<DocumentStructureRefreshRequestedEvent>(OnRefreshEvent);

        // ── Terminal commands ─────────────────────────────────────────────
        context.Terminal.RegisterCommand(new StructureListCommand(_vm));
        context.Terminal.RegisterCommand(new StructureNavigateCommand(_vm));

        // ── Deferred startup load ─────────────────────────────────────────
        // Defer to Loaded priority so the docking layout has finished rendering
        // before we attempt the first refresh (mirrors ParsedFieldsPlugin pattern).
        _panel?.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, (Action)(() =>
        {
            _isPanelVisible = true;

            // HexEditor takes priority
            if (_context?.HexEditor.IsActive == true)
            {
                var fp = _context.HexEditor.CurrentFilePath;
                if (!string.IsNullOrEmpty(fp)) { QueueOrRefresh(fp, "hex", null); return; }
            }

            var activeDoc = _context?.FocusContext.ActiveDocument;
            if (activeDoc is null || string.IsNullOrEmpty(activeDoc.FilePath)) return;

            var filePath = activeDoc.FilePath;
            if (!File.Exists(filePath)) return;

            QueueOrRefresh(filePath, activeDoc.DocumentType, InferLanguage(filePath));
        }));

        return Task.CompletedTask;
    }

    // ── XAML Designer wiring ──────────────────────────────────────────────────

    private void WireDesignerService(IXamlDesignerService svc)
    {
        _designerService = svc;
        _resolver?.Register(new XamlDesignerStructureProvider(svc));
        svc.SelectedElementChanged += OnDesignerSelectionChanged;
        svc.ElementTreeChanged     += OnDesignerTreeChanged;
    }

    private void OnDesignerSelectionChanged(object? sender, int uid)
        => _vm?.HighlightNodeByUid(uid);

    private void OnDesignerTreeChanged(object? sender, EventArgs e)
    {
        var filePath = _context?.FocusContext.ActiveDocument?.FilePath;
        var docType  = _context?.FocusContext.ActiveDocument?.DocumentType;
        if (!string.IsNullOrEmpty(filePath))
            QueueOrRefresh(filePath, docType, "xaml");
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        _lspSymbolSub?.Dispose();
        _cursorMovedSub?.Dispose();
        _refreshSub?.Dispose();
        _pluginLoadedSub?.Dispose();

        if (_designerService is not null)
        {
            _designerService.SelectedElementChanged -= OnDesignerSelectionChanged;
            _designerService.ElementTreeChanged     -= OnDesignerTreeChanged;
            _designerService = null;
        }

        if (_context is not null)
        {
            _context.UIRegistry.PanelShown  -= OnPanelShown;
            _context.UIRegistry.PanelHidden -= OnPanelHidden;
            _context.FocusContext.FocusChanged     -= OnFocusChanged;
            _context.HexEditor.ActiveEditorChanged -= OnActiveEditorChanged;
            _context.HexEditor.FormatDetected      -= OnFormatDetected;
            _context.CodeEditor.DocumentChanged    -= OnCodeEditorDocumentChanged;

            _context.Terminal.UnregisterCommand(new StructureListCommand(_vm!).CommandName);
            _context.Terminal.UnregisterCommand(new StructureNavigateCommand(_vm!).CommandName);
        }

        // Clean up temp files used for virtual/decompiled documents
        foreach (var f in _virtualTempFiles.Values)
            try { File.Delete(f); } catch { /* best-effort */ }

        if (_vm is not null)
            _vm.NavigateRequested -= OnNavigateRequested;
        if (_panel is not null)
            _panel.RefreshRequested -= OnPanelRefreshRequested;

        _panel   = null;
        _vm      = null;
        _context = null;
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Lazy Panel Visibility
    // ══════════════════════════════════════════════════════════════════════════

    private void OnPanelShown(object? sender, string uiId)
    {
        if (uiId != PanelUiId) return;
        _isPanelVisible = true;

        if (_pendingRefresh is var (fp, dt, lang))
        {
            _pendingRefresh = null;
            _vm?.QueueRefresh(fp, dt, lang);
        }
    }

    private void OnPanelHidden(object? sender, string uiId)
    {
        if (uiId != PanelUiId) return;
        _isPanelVisible = false;
    }

    private void QueueOrRefresh(string? filePath, string? docType, string? language)
    {
        if (_vm?.IsPinned == true) return;
        if (_isPanelVisible)
            _vm?.QueueRefresh(filePath, docType, language);
        else
            _pendingRefresh = (filePath, docType, language);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Focus / Editor Change Handlers
    // ══════════════════════════════════════════════════════════════════════════

    private void OnFocusChanged(object? sender, FocusChangedEventArgs e)
    {
        if (_isNavigating) return;   // triggered by our own ActivateAndNavigateTo — ignore

        if (e.ActiveDocument is null)
        {
            _vm?.ClearForNoDocument();
            return;
        }
        if (e.ActiveDocument.ContentId == e.PreviousDocument?.ContentId) return;

        // Skip panels with no file (Options, Settings…)
        if (string.IsNullOrEmpty(e.ActiveDocument.FilePath)) return;

        _hexEditorHandledLastSwitch = false;

        var filePath = e.ActiveDocument.FilePath;
        var docType  = e.ActiveDocument.DocumentType;
        var language = InferLanguage(filePath);

        // Defer to ContextIdle so OnActiveEditorChanged (Background priority) runs first
        _panel?.Dispatcher.InvokeAsync(() =>
        {
            if (_hexEditorHandledLastSwitch) return;
            QueueOrRefresh(filePath, docType, language);
        }, System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    /// <summary>Infers language ID from file extension (fallback when NullCodeEditorService is active).</summary>
    private static string? InferLanguage(string? filePath) =>
        Path.GetExtension(filePath)?.ToLowerInvariant() switch
        {
            ".cs"   => "csharp",
            ".vb"   => "vb",
            ".xaml" => "xaml",
            ".xml"  => "xml",
            ".json" => "json",
            ".md"   => "markdown",
            ".ini"  => "ini",
            _       => null,
        };

    private void OnActiveEditorChanged(object? sender, EventArgs e)
    {
        _hexEditorHandledLastSwitch = true;

        // HexEditor tab switched — refresh with hex context
        var filePath = _context?.HexEditor.IsActive == true ? _context.HexEditor.CurrentFilePath : null;
        if (!string.IsNullOrEmpty(filePath))
            QueueOrRefresh(filePath, "hex", null);
    }

    private void OnFormatDetected(object? sender, FormatDetectedArgs e)
    {
        // Binary format detected → refresh to pick up BinaryFormatStructureProvider
        if (!e.Success) return;
        var filePath = _context?.HexEditor.CurrentFilePath;
        if (!string.IsNullOrEmpty(filePath))
            QueueOrRefresh(filePath, "hex", null);
    }

    private void OnCodeEditorDocumentChanged(object? sender, EventArgs e)
    {
        // NullCodeEditorService: IsActive=false, CurrentFilePath=null — event never fires anyway.
        // Fallback to FocusContext which is always accurate.
        var activeDoc = _context?.FocusContext.ActiveDocument;
        if (activeDoc is null || string.IsNullOrEmpty(activeDoc.FilePath)) return;
        if (!File.Exists(activeDoc.FilePath)) return;
        QueueOrRefresh(activeDoc.FilePath, activeDoc.DocumentType, InferLanguage(activeDoc.FilePath));
    }

    /// <summary>
    /// Writes the current code editor content to a per-language temp file.
    /// Returns the temp file path, or null if content or language is unavailable.
    /// </summary>
    private string? WriteVirtualToTempFile(string? language)
    {
        if (_context is null) return null;
        var content = _context.CodeEditor.GetContent();
        if (string.IsNullOrEmpty(content)) return null;

        var ext = language switch
        {
            "csharp" => ".cs",
            "vb"     => ".vb",
            "xml"    => ".xml",
            "xaml"   => ".xaml",
            _        => null,
        };
        if (ext is null) return null;

        if (!_virtualTempFiles.TryGetValue(ext, out var tempPath))
        {
            tempPath = Path.Combine(Path.GetTempPath(), $"wpfhex_ds_{Guid.NewGuid():N}{ext}");
            _virtualTempFiles[ext] = tempPath;
        }

        try   { File.WriteAllText(tempPath, content); }
        catch { return null; }
        return tempPath;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // IDE Events
    // ══════════════════════════════════════════════════════════════════════════

    private void OnLspSymbolsUpdated(LspSymbolTableUpdatedEvent evt)
    {
        // LSP symbols rebuilt → refresh if it's for the current file
        var currentFile = _context?.CodeEditor.CurrentFilePath ?? _context?.FocusContext.ActiveDocument?.FilePath;
        if (string.Equals(evt.FilePath, currentFile, StringComparison.OrdinalIgnoreCase))
            QueueOrRefresh(currentFile, "code", _context?.CodeEditor.CurrentLanguage);
    }

    private void OnCursorMoved(CodeEditorCursorMovedEvent evt)
    {
        if (_isPanelVisible)
            _vm?.UpdateCaretHighlight(evt.Line);

        // Split-view file-switch detection: re-resolve when the active pane's file changes
        if (!string.IsNullOrEmpty(evt.FilePath) &&
            !string.Equals(evt.FilePath, _lastTrackedFilePath, StringComparison.OrdinalIgnoreCase))
        {
            _lastTrackedFilePath = evt.FilePath;
            QueueOrRefresh(evt.FilePath, "code", _context?.CodeEditor.CurrentLanguage);
        }
    }

    private void OnRefreshEvent(DocumentStructureRefreshRequestedEvent evt)
    {
        var filePath = evt.FilePath ?? _context?.FocusContext.ActiveDocument?.FilePath;
        var docType = _context?.FocusContext.ActiveDocument?.DocumentType;
        var language = _context?.CodeEditor.IsActive == true ? _context.CodeEditor.CurrentLanguage : null;
        if (!string.IsNullOrEmpty(filePath))
            QueueOrRefresh(filePath, docType, language);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Navigation
    // ══════════════════════════════════════════════════════════════════════════

    private void OnNavigateRequested(object? sender, StructureNodeVm node)
    {
        if (_context is null) return;

        // XAML Designer: navigate by element UID — activates designer tab + syncs code editor.
        if (node.Tag is int uid && _designerService?.IsDesignerActive == true)
        {
            _designerService.NavigateToElement(uid);
            return;
        }

        if (node.ByteOffset >= 0 && _context.HexEditor.IsActive)
        {
            _context.HexEditor.NavigateTo(node.ByteOffset);
            if (node.ByteLength > 0)
                _context.HexEditor.SetSelection(node.ByteOffset, node.ByteOffset + node.ByteLength - 1);
        }
        else if (node.StartLine > 0)
        {
            var filePath = _context.FocusContext.ActiveDocument?.FilePath;
            if (!string.IsNullOrEmpty(filePath))
            {
                _isNavigating = true;
                _context.DocumentHost.ActivateAndNavigateTo(filePath!, node.StartLine, node.StartColumn > 0 ? node.StartColumn : 1);
                // Reset after dispatcher flushes the focus-change event
                _panel?.Dispatcher.InvokeAsync(() => _isNavigating = false,
                    System.Windows.Threading.DispatcherPriority.ContextIdle);
            }
        }
    }

    private void OnPanelRefreshRequested(object? sender, EventArgs e)
    {
        var filePath = _context?.FocusContext.ActiveDocument?.FilePath;
        var docType = _context?.FocusContext.ActiveDocument?.DocumentType;
        var language = _context?.CodeEditor.IsActive == true ? _context.CodeEditor.CurrentLanguage : null;
        if (!string.IsNullOrEmpty(filePath))
            _vm?.QueueRefresh(filePath, docType, language);
    }
}

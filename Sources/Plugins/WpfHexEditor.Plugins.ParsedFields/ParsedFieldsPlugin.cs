// ==========================================================
// Project: WpfHexEditor.Plugins.ParsedFields
// File: ParsedFieldsPlugin.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6, Claude (Anthropic)
// Created: 2026-03-07
// Refactored: 2026-03-26
// Description:
//     Plugin entry point for the Parsed Fields panel.
//     Two coexisting paths:
//       1. HexEditor tabs → legacy ConnectParsedFieldsPanel (per-tab FormatParsingService)
//       2. Non-hex documents / Explorer previews → plugin-owned _previewService
//     Lazy rendering: only parses when panel is visible.
//
// Architecture Notes:
//     Each HexEditor tab owns its own FormatParsingService + HexEditorDataSource.
//     This plugin owns a SEPARATE _previewService for non-hex previews (Solution
//     Explorer file clicks, Assembly Explorer members, non-hex document tabs).
//     OnActiveEditorChanged disconnects the preview service so HexEditor takes over.
//     OnFocusChanged skips "hex" document types — those are handled by the legacy path.
// ==========================================================

using System.IO;
using System.Linq;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.Core.Services.FormatParsing;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.Plugins.ParsedFields.Views;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Focus;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Events;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.Plugins.ParsedFields;

/// <summary>
/// Plugin registering the Parsed Fields panel (Right dock).
/// HexEditor tabs use legacy ConnectParsedFieldsPanel; all other sources use a
/// plugin-owned preview FormatParsingService.
/// </summary>
public sealed class ParsedFieldsPlugin : IWpfHexEditorPlugin
{
    public string  Id      => "WpfHexEditor.Plugins.ParsedFields";
    public string  Name    => "Parsed Fields";
    public Version Version => new(0, 7, 1);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor  = true,
        AccessFileSystem = false,
        RegisterMenus    = true,
        WriteOutput      = false
    };

    private const string PanelUiId = "WpfHexEditor.Plugins.ParsedFields.Panel.ParsedFieldsPanel";

    private ParsedFieldsPanel? _panel;
    private IIDEHostContext?   _context;
    private IDisposable?       _templateSub;
    private IDisposable?       _grammarSub;
    private IDisposable?       _filePreviewSub;
    private IDisposable?       _assemblyMemberSub;
    private IDisposable?       _diffSideSub;

    // ── Preview service (owned by plugin — separate from HexEditor's per-tab service) ──
    private FormatParsingService? _previewService;
    private GenericFileDataSource? _previewDataSource;
    private string? _lastPreviewFilePath;
    private bool _isPreviewActive; // true when preview service owns the panel
    // _previewFormatsLoaded removed — shared catalog loaded at app startup via FormatCatalogService

    // ── Lazy update state ────────────────────────────────────────────────────
    private bool _isPanelVisible = true;
    private ParsedFieldsUpdateRequestedEvent? _pendingUpdate;
    private bool _hexEditorHandledLastSwitch; // set by OnActiveEditorChanged, cleared by OnFocusChanged

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;
        _panel   = new ParsedFieldsPanel();

        // Register the panel (Right dock).
        context.UIRegistry.RegisterPanel(PanelUiId, _panel, Id, new PanelDescriptor
        {
            Title           = "Parsed Fields",
            DefaultDockSide = "Right",
            DefaultAutoHide = false,
            CanClose        = true,
            PreferredWidth  = 340
        });

        // Register View menu item.
        context.UIRegistry.RegisterMenuItem($"{Id}.Menu.Show", Id, new MenuItemDescriptor
        {
            Header     = "_Parsed Fields",
            ParentPath = "View",
            Group      = "Analysis",
            IconGlyph  = "\uE81E",
            Command    = new RelayCommand(_ => context.UIRegistry.ShowPanel(PanelUiId))
        });

        // ── Panel visibility tracking (lazy updates) ─────────────────────
        _isPanelVisible = context.UIRegistry.IsPanelVisible(PanelUiId);
        context.UIRegistry.PanelShown  += OnPanelShown;
        context.UIRegistry.PanelHidden += OnPanelHidden;

        // ── ALWAYS wire legacy HexEditor path (handles HexEditor tabs) ───
        if (context.HexEditor.IsActive)
            context.HexEditor.ConnectParsedFieldsPanel(_panel);
        context.HexEditor.ActiveEditorChanged += OnActiveEditorChanged;
        context.HexEditor.FileOpened          += OnFileOpened;
        context.HexEditor.FormatDetected      += OnFormatDetected;

        // ── Focus change → preview for non-hex documents ─────────────────
        context.FocusContext.FocusChanged += OnFocusChanged;

        // ── Solution Explorer file selection → preview ───────────────────
        _filePreviewSub = context.EventBus.Subscribe<FilePreviewRequestedEvent>(OnFilePreviewRequested);

        // ── Assembly Explorer member selection → PE fields ───────────────
        _assemblyMemberSub = context.EventBus.Subscribe<AssemblyNavigationRequestedEvent>(OnAssemblyNavigationRequested);

        // ── Bookmark navigation ──────────────────────────────────────────
        _panel.NavigateToOffsetRequested += OnNavigateToOffsetRequested;

        // ── Template / Grammar EventBus routes ───────────────────────────
        _templateSub = context.EventBus.Subscribe<TemplateApplyRequestedEvent>(OnTemplateApplyRequested);
        _grammarSub  = context.EventBus.Subscribe<GrammarAppliedEvent>(OnGrammarApplied);

        // ── Binary Diff side focus → preview for the focused file ─────
        _diffSideSub = context.EventBus.Subscribe<DiffSideFocusChangedEvent>(OnDiffSideFocusChanged);

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        _templateSub?.Dispose();
        _grammarSub?.Dispose();
        _filePreviewSub?.Dispose();
        _assemblyMemberSub?.Dispose();
        _diffSideSub?.Dispose();

        // Cleanup preview service
        _previewService?.DisconnectPanel();
        _previewService?.Clear();
        _previewDataSource?.Dispose();

        if (_context != null)
        {
            _context.UIRegistry.PanelShown  -= OnPanelShown;
            _context.UIRegistry.PanelHidden -= OnPanelHidden;
            _context.FocusContext.FocusChanged -= OnFocusChanged;

            // Legacy HexEditor path (always wired)
            _context.HexEditor.ActiveEditorChanged -= OnActiveEditorChanged;
            _context.HexEditor.FileOpened          -= OnFileOpened;
            _context.HexEditor.FormatDetected      -= OnFormatDetected;
            _context.HexEditor.DisconnectParsedFieldsPanel();
        }

        if (_panel != null)
            _panel.NavigateToOffsetRequested -= OnNavigateToOffsetRequested;

        _panel   = null;
        _context = null;
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Lazy Update Infrastructure
    // ══════════════════════════════════════════════════════════════════════════

    private void QueueOrExecuteUpdate(ParsedFieldsUpdateRequestedEvent update)
    {
        if (_isPanelVisible)
            ExecuteUpdate(update);
        else
            _pendingUpdate = update;
    }

    private void OnPanelShown(object? sender, string uiId)
    {
        if (uiId != PanelUiId) return;
        _isPanelVisible = true;

        if (_pendingUpdate != null)
        {
            var pending = _pendingUpdate;
            _pendingUpdate = null;
            ExecuteUpdate(pending);
        }
    }

    private void OnPanelHidden(object? sender, string uiId)
    {
        if (uiId != PanelUiId) return;
        _isPanelVisible = false;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Preview Service Management
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Attach a preview data source for a non-HexEditor file.
    /// Uses the plugin-owned _previewService (never touches HexEditor's per-tab service).
    /// </summary>
    private void ActivatePreview(string? filePath)
    {
        if (_panel == null) return;
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

        // Dedup: don't re-parse the same file
        if (string.Equals(filePath, _lastPreviewFilePath, StringComparison.OrdinalIgnoreCase)
            && _previewService?.ActiveFormat != null)
            return;

        // Disconnect HexEditor's panel connection — preview takes over
        _context?.HexEditor.DisconnectParsedFieldsPanel();

        // Create preview service on first use (uses shared catalog via FormatDetectionService.EffectiveFormats)
        _previewService ??= new FormatParsingService();

        // Connect panel to preview service (steals it from HexEditor)
        if (!_isPreviewActive)
        {
            _previewService.ConnectPanel(_panel);
            _previewService.FormatDetected += OnPreviewFormatDetected;
            _isPreviewActive = true;
        }

        // Attach new data source
        _previewDataSource?.Dispose();
        _previewDataSource = new GenericFileDataSource(filePath);
        _previewService.Attach(_previewDataSource); // autoDetect=true → DetectAndParseAsync
        _lastPreviewFilePath = filePath;
    }

    /// <summary>
    /// Deactivate preview mode — HexEditor takes panel ownership back.
    /// </summary>
    private void DeactivatePreview()
    {
        if (_isPreviewActive && _previewService != null)
        {
            _previewService.FormatDetected -= OnPreviewFormatDetected;
            _previewService.DisconnectPanel();
            _previewService.Clear();
            _isPreviewActive = false;
        }

        _previewDataSource?.Dispose();
        _previewDataSource = null;
        _lastPreviewFilePath = null;
    }

    private void OnPreviewFormatDetected(object? sender, WpfHexEditor.Core.Events.FormatDetectedEventArgs e)
    {
        if (_panel is null || !e.Success) return;
        _panel.Dispatcher.InvokeAsync(() => _panel.SetEnrichedFormat(e.Format));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ExecuteUpdate Orchestrator (for non-HexEditor sources only)
    // ══════════════════════════════════════════════════════════════════════════

    private void ExecuteUpdate(ParsedFieldsUpdateRequestedEvent update)
    {
        if (_panel == null || _context == null) return;

        _panel.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                switch (update.SourceKind)
                {
                    case "document":
                    case "explorer":
                        ActivatePreview(update.FilePath);
                        break;

                    case "assembly":
                        HandleAssemblyUpdate(update);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ParsedFieldsPlugin] Update error: {ex.Message}");
            }
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void HandleAssemblyUpdate(ParsedFieldsUpdateRequestedEvent update)
    {
        // If a HexEditor is active with a PE file, navigate to the PE offset
        if (_context!.HexEditor.IsActive && update.PeOffset > 0)
        {
            _context.HexEditor.NavigateTo(update.PeOffset);
            return;
        }

        // No HexEditor open — open a preview data source for the PE file
        if (!string.IsNullOrEmpty(update.FilePath) && File.Exists(update.FilePath))
            ActivatePreview(update.FilePath);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Event Handlers — Context Change Sources
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Document tab focus changed. For non-HexEditor tabs (CodeEditor, TextEditor, etc.),
    /// activates preview mode. HexEditor tabs are handled by OnActiveEditorChanged instead.
    /// Deferred to Background priority so OnActiveEditorChanged has time to set
    /// _hexEditorHandledLastSwitch before this callback checks it.
    /// </summary>
    private void OnFocusChanged(object? sender, FocusChangedEventArgs e)
    {
        if (e.ActiveDocument == null) return;
        if (e.ActiveDocument.ContentId == e.PreviousDocument?.ContentId) return;

        var filePath = e.ActiveDocument.FilePath;
        if (string.IsNullOrEmpty(filePath)) return;

        // Clear the flag — OnActiveEditorChanged will set it back to true if it fires
        _hexEditorHandledLastSwitch = false;

        // Defer to ContextIdle priority (LOWER than Background) so OnActiveEditorChanged
        // (which fires at Background) has already run and set the flag by the time we check.
        _panel?.Dispatcher.InvokeAsync(() =>
        {
            // If HexEditor claimed this tab switch, skip — it already reconnected the panel
            if (_hexEditorHandledLastSwitch) return;

            // Non-hex document — activate preview
            QueueOrExecuteUpdate(new ParsedFieldsUpdateRequestedEvent
            {
                FilePath = filePath,
                SourceKind = "document"
            });
        }, System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    /// <summary>Solution Explorer file selected → preview format.</summary>
    private void OnFilePreviewRequested(FilePreviewRequestedEvent evt)
    {
        if (string.IsNullOrEmpty(evt.FilePath) || !File.Exists(evt.FilePath)) return;

        QueueOrExecuteUpdate(new ParsedFieldsUpdateRequestedEvent
        {
            FilePath = evt.FilePath,
            SourceKind = "explorer"
        });
    }

    /// <summary>Assembly Explorer member selected → show PE fields.</summary>
    private void OnAssemblyNavigationRequested(AssemblyNavigationRequestedEvent evt)
    {
        if (evt.PeOffset <= 0) return;

        QueueOrExecuteUpdate(new ParsedFieldsUpdateRequestedEvent
        {
            FilePath = string.IsNullOrEmpty(evt.FilePath) ? null : evt.FilePath,
            SourceKind = "assembly",
            PeOffset = evt.PeOffset
        });
    }

    /// <summary>Binary Diff viewer side focus changed → preview the focused file's format.</summary>
    private void OnDiffSideFocusChanged(DiffSideFocusChangedEvent evt)
    {
        if (string.IsNullOrEmpty(evt.FilePath)) return;

        QueueOrExecuteUpdate(new ParsedFieldsUpdateRequestedEvent
        {
            FilePath   = evt.FilePath,
            SourceKind = "document"
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Legacy HexEditor Event Handlers (ALWAYS active — handles HexEditor tabs)
    // ══════════════════════════════════════════════════════════════════════════

    private void OnNavigateToOffsetRequested(object? sender, long e)
    {
        if (_context?.HexEditor.IsActive == true)
            _context.HexEditor.NavigateTo(e);
    }

    /// <summary>
    /// HexEditor tab switched. Deactivate preview service and reconnect
    /// the panel to the new HexEditor's per-tab FormatParsingService.
    /// Only fires when the new active tab IS a HexEditor.
    /// </summary>
    private void OnActiveEditorChanged(object? sender, EventArgs e)
    {
        if (_panel is null || _context is null) return;

        // Signal that HexEditor handled this tab switch — OnFocusChanged should skip
        _hexEditorHandledLastSwitch = true;

        // Deactivate preview — HexEditor takes panel ownership
        DeactivatePreview();

        _context.HexEditor.DisconnectParsedFieldsPanel();
        if (_context.HexEditor.IsActive)
            _context.HexEditor.ConnectParsedFieldsPanel(_panel);
    }

    private void OnFileOpened(object? sender, EventArgs e)
    {
        _panel?.Clear();
    }

    private void OnFormatDetected(object? sender, FormatDetectedArgs e)
    {
        if (_panel is null) return;
        var format = e.RawFormatDefinition as FormatDefinition;
        _panel.Dispatcher.InvokeAsync(() => _panel.SetEnrichedFormat(format));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // EventBus Handlers (Template / Grammar — shared by all paths)
    // ══════════════════════════════════════════════════════════════════════════

    private void OnTemplateApplyRequested(TemplateApplyRequestedEvent evt)
    {
        if (_panel is null || _context?.HexEditor.IsActive != true) return;

        _panel.Dispatcher.InvokeAsync(() =>
        {
            _panel.Clear();
            _panel.TotalFileSize = _context.HexEditor.FileSize;

            foreach (var block in evt.Blocks)
            {
                _panel.ParsedFields.Add(new ParsedFieldViewModel
                {
                    Name           = block.Name,
                    Offset         = block.Offset,
                    Length         = block.Length,
                    ValueType      = block.TypeHint     ?? "Unknown",
                    FormattedValue = block.DisplayValue ?? string.Empty
                });
            }
            _panel.RefreshView();
        });
    }

    private void OnGrammarApplied(GrammarAppliedEvent evt)
    {
        if (_panel is null || _context?.HexEditor.IsActive != true) return;

        _panel.Dispatcher.InvokeAsync(() =>
        {
            _panel.Clear();
            _panel.TotalFileSize = _context.HexEditor.FileSize;

            foreach (var field in evt.Fields)
            {
                _panel.ParsedFields.Add(new ParsedFieldViewModel
                {
                    Name           = field.Name,
                    Offset         = field.Offset,
                    Length         = field.Length,
                    FormattedValue = field.ValueDisplay,
                    ValueType      = field.Kind.ToString(),
                    Description    = field.Description,
                    Color          = string.IsNullOrEmpty(field.Color) ? null
                                     : (field.Color.StartsWith('#') ? field.Color : "#" + field.Color),
                    IndentLevel    = field.IndentLevel,
                    GroupName      = field.GroupName,
                    IsValid        = field.IsValid,
                });
            }

            _panel.FormatInfo = new FormatInfo
            {
                IsDetected  = true,
                Name        = evt.GrammarName,
                Description = $"Parsed via UFWB grammar — {evt.Fields.Count} fields",
            };
            _panel.RefreshView();
        });
    }
}

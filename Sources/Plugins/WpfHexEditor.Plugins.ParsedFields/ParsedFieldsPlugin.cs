// ==========================================================
// Project: WpfHexEditor.Plugins.ParsedFields
// File: ParsedFieldsPlugin.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-07
// Description:
//     Plugin entry point for the Parsed Fields panel.
//     Moves the panel from direct MainWindow management into the plugin system,
//     making it independently registerable, dockable, and lifecycle-managed.
//
// Architecture Notes:
//     Pattern: Observer + Mediator
//     - Subscribes to IHexEditorService.ActiveEditorChanged to reconnect the panel
//       on tab switches (replaces MainWindow's direct Connect/Disconnect calls).
//     - Subscribes to TemplateApplyRequestedEvent via IPluginEventBus to receive
//       blocks from CustomParserTemplatePlugin (replaces MainWindow handler).
//     - The 5 bidirectional HexEditor↔ParsedFieldsPanel events (FieldSelected,
//       RefreshRequested, FormatterChanged, FieldValueEdited, FormatCandidateSelected)
//       are auto-wired via HexEditorControl's ParsedFieldsPanelProperty DP.
// ==========================================================

using WpfHexEditor.SDK.Commands;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.Plugins.ParsedFields.Views;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Events;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.Plugins.ParsedFields;

/// <summary>
/// Plugin registering the Parsed Fields panel (Right dock).
/// Manages the HexEditor ↔ ParsedFieldsPanel bidirectional connection and
/// routes <see cref="TemplateApplyRequestedEvent"/> from the CustomParser plugin.
/// </summary>
public sealed class ParsedFieldsPlugin : IWpfHexEditorPlugin
{
    public string  Id      => "WpfHexEditor.Plugins.ParsedFields";
    public string  Name    => "Parsed Fields";
    public Version Version => new(0, 5, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor  = true,
        AccessFileSystem = false,
        RegisterMenus    = true,
        WriteOutput      = false
    };

    private ParsedFieldsPanel? _panel;
    private IIDEHostContext?   _context;
    private IDisposable?       _templateSub;
    private IDisposable?       _grammarSub;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;
        _panel   = new ParsedFieldsPanel();

        // Register the panel (Right dock — same side as before).
        context.UIRegistry.RegisterPanel(
            "WpfHexEditor.Plugins.ParsedFields.Panel.ParsedFieldsPanel",
            _panel,
            Id,
            new PanelDescriptor
            {
                Title           = "Parsed Fields",
                DefaultDockSide = "Right",
                DefaultAutoHide = false,
                CanClose        = true,
                PreferredWidth  = 340
            });

        // Register View menu item so the user can show/hide this panel.
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.Show",
            Id,
            new MenuItemDescriptor
            {
                Header     = "_Parsed Fields",
                ParentPath = "View",
                Group      = "Analysis",
                IconGlyph  = "\uE81E",
                Command    = new RelayCommand(_ => context.UIRegistry.ShowPanel(
                                 "WpfHexEditor.Plugins.ParsedFields.Panel.ParsedFieldsPanel"))
            });

        // Connect to the current active editor immediately (if any file is already open).
        if (context.HexEditor.IsActive)
            context.HexEditor.ConnectParsedFieldsPanel(_panel);

        // Wire bookmark navigation: clicking a section chip scrolls the hex editor.
        _panel.NavigateToOffsetRequested += OnNavigateToOffsetRequested;

        // Reconnect when the active tab changes.
        context.HexEditor.ActiveEditorChanged += OnActiveEditorChanged;
        context.HexEditor.FileOpened          += OnFileOpened;
        context.HexEditor.FormatDetected      += OnFormatDetected;

        // Route TemplateApplyRequestedEvent to this panel (was handled by MainWindow).
        _templateSub = context.EventBus.Subscribe<TemplateApplyRequestedEvent>(OnTemplateApplyRequested);

        // Route GrammarAppliedEvent from SynalysisGrammarPlugin (issue #177).
        _grammarSub = context.EventBus.Subscribe<GrammarAppliedEvent>(OnGrammarApplied);

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        _templateSub?.Dispose();
        _grammarSub?.Dispose();

        if (_context != null)
        {
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

    // ── HexEditor event handlers ───────────────────────────────────────────

    /// <summary>
    /// Forwards a bookmark navigation request to the hex editor:
    /// scrolls the viewport to <paramref name="e"/> and places the caret there.
    /// </summary>
    private void OnNavigateToOffsetRequested(object? sender, long e)
    {
        if (_context?.HexEditor.IsActive == true)
            _context.HexEditor.NavigateTo(e);
    }

    /// <summary>
    /// Reconnects the panel to the newly active editor after a tab switch.
    /// The previous editor is disconnected automatically by DisconnectParsedFieldsPanel().
    /// </summary>
    private void OnActiveEditorChanged(object? sender, EventArgs e)
    {
        if (_panel is null || _context is null) return;
        _context.HexEditor.DisconnectParsedFieldsPanel();
        if (_context.HexEditor.IsActive)
            _context.HexEditor.ConnectParsedFieldsPanel(_panel);
        // Enriched data is re-populated by OnParsedFieldsPanelChanged inside the HexEditor DP callback
        // (calls newPanel.SetEnrichedFormat(_detectedFormat) when reconnecting to an already-detected editor).
    }

    /// <summary>Clears the panel when a new file is opened (no parsed fields yet).</summary>
    private void OnFileOpened(object? sender, EventArgs e)
    {
        _panel?.Clear();
        // Do NOT call SetEnrichedFormat(null) here.
        // AutoDetectAndApplyFormat is queued at Background priority before this deferred
        // FileOpened event; clearing here causes the enriched section to appear then vanish.
        // Safety: Clear() sets FormatInfo.IsDetected=false → parent Border collapses → any
        // stale enriched data is hidden until format detection re-runs and sets IsDetected=true.
    }

    /// <summary>
    /// Receives format detection result and pushes enriched metadata into the panel.
    /// RawFormatDefinition is cast to FormatDefinition — available to bundled plugins via SDK→HexEditor.
    /// </summary>
    private void OnFormatDetected(object? sender, FormatDetectedArgs e)
    {
        if (_panel is null) return;
        var format = e.RawFormatDefinition as FormatDefinition;
        _panel.Dispatcher.InvokeAsync(() => _panel.SetEnrichedFormat(format));
    }

    // ── EventBus handler ──────────────────────────────────────────────────

    /// <summary>
    /// Receives parsed blocks from the CustomParserTemplate plugin via EventBus
    /// and populates this panel — replaces the MainWindow.PluginSystem handler.
    /// </summary>
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

    /// <summary>
    /// Receives UFWB grammar parse results from SynalysisGrammarPlugin (issue #177)
    /// and populates this panel with the structured fields.
    /// Pattern identical to OnTemplateApplyRequested.
    /// </summary>
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

            // Show grammar name as the format info header.
            _panel.FormatInfo = new WpfHexEditor.Core.Interfaces.FormatInfo
            {
                IsDetected  = true,
                Name        = evt.GrammarName,
                Description = $"Parsed via UFWB grammar — {evt.Fields.Count} fields",
            };

            _panel.RefreshView();
        });
    }
}

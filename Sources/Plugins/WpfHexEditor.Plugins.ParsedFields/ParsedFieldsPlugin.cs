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

using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.Plugins.ParsedFields.Views;
using WpfHexEditor.SDK.Contracts;
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
    public Version Version => new(1, 0, 0);

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
                Title          = "Parsed Fields",
                DefaultDockSide = "Right",
                CanClose        = true,
                PreferredWidth  = 340
            });

        // Connect to the current active editor immediately (if any file is already open).
        if (context.HexEditor.IsActive)
            context.HexEditor.ConnectParsedFieldsPanel(_panel);

        // Reconnect when the active tab changes.
        context.HexEditor.ActiveEditorChanged += OnActiveEditorChanged;
        context.HexEditor.FileOpened          += OnFileOpened;

        // Route TemplateApplyRequestedEvent to this panel (was handled by MainWindow).
        _templateSub = context.EventBus.Subscribe<TemplateApplyRequestedEvent>(OnTemplateApplyRequested);

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        _templateSub?.Dispose();

        if (_context != null)
        {
            _context.HexEditor.ActiveEditorChanged -= OnActiveEditorChanged;
            _context.HexEditor.FileOpened          -= OnFileOpened;
            _context.HexEditor.DisconnectParsedFieldsPanel();
        }

        _panel   = null;
        _context = null;
        return Task.CompletedTask;
    }

    // ── HexEditor event handlers ───────────────────────────────────────────

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
    }

    /// <summary>Clears the panel when a new file is opened (no parsed fields yet).</summary>
    private void OnFileOpened(object? sender, EventArgs e)
        => _panel?.Clear();

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
                    DataType       = block.TypeHint     ?? "Unknown",
                    FormattedValue = block.DisplayValue ?? string.Empty
                });
            }

            _panel.RefreshView();
        });
    }
}

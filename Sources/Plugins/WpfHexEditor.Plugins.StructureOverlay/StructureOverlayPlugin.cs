// ==========================================================
// Project: WpfHexEditor.Plugins.StructureOverlay
// File: StructureOverlayPlugin.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-07
// Description:
//     Plugin entry point for the Structure Overlay panel.
//     Feeds file bytes to the panel on FileOpened, and bridges
//     OnFieldSelectedForHighlight → IHexEditorService.SetSelection
//     so that selecting a field in the tree highlights it in the HexEditor.
//
// Architecture Notes:
//     Pattern: Observer + Adapter — host events drive panel; panel events drive host.
// ==========================================================

using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.Core.Models.StructureOverlay;
using WpfHexEditor.Plugins.StructureOverlay.Views;

namespace WpfHexEditor.Plugins.StructureOverlay;

/// <summary>
/// Official plugin wrapping the Structure Overlay panel.
/// Bridges field selection in the panel to HexEditor byte highlighting via
/// <see cref="IHexEditorService.SetSelection"/>.
/// </summary>
public sealed class StructureOverlayPlugin : IWpfHexEditorPlugin
{
    private IIDEHostContext?       _context;
    private StructureOverlayPanel? _panel;

    public string  Id      => "WpfHexEditor.Plugins.StructureOverlay";
    public string  Name    => "Structure Overlay";
    public Version Version => new(0, 4, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor  = true,
        AccessFileSystem = false,
        RegisterMenus    = true,
        WriteOutput      = true
    };

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;
        _panel   = new StructureOverlayPanel();

        // Bridge: field selected in panel → highlight in HexEditor
        _panel.OnFieldSelectedForHighlight += OnFieldSelectedForHighlight;

        context.UIRegistry.RegisterPanel(
            "WpfHexEditor.Plugins.StructureOverlay.Panel.StructureOverlayPanel",
            _panel,
            Id,
            new PanelDescriptor
            {
                Title           = "Structure Overlay",
                DefaultDockSide = "Right",
                DefaultAutoHide = false,
                CanClose        = true
            });

        // Register View menu item so the user can show/hide this panel.
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.Show",
            Id,
            new MenuItemDescriptor
            {
                Header     = "_Structure Overlay",
                ParentPath = "View",
                Group      = "Analysis",
                IconGlyph  = "\uE82D",
                Command    = new RelayCommand(_ => context.UIRegistry.ShowPanel(
                                 "WpfHexEditor.Plugins.StructureOverlay.Panel.StructureOverlayPanel"))
            });

        context.HexEditor.FileOpened += OnFileOpened;

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        if (_context is not null)
            _context.HexEditor.FileOpened -= OnFileOpened;

        if (_panel is not null)
            _panel.OnFieldSelectedForHighlight -= OnFieldSelectedForHighlight;

        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------

    private void OnFileOpened(object? sender, EventArgs e)
    {
        if (_panel is null || _context is null || !_context.HexEditor.IsActive) return;

        var readLen = (int)Math.Min(_context.HexEditor.FileSize, 1_048_576);
        var data    = readLen > 0 ? _context.HexEditor.ReadBytes(0, readLen) : [];

        _panel.Dispatcher.BeginInvoke(() => _panel.UpdateFileBytes(data));
    }

    private void OnFieldSelectedForHighlight(object? sender, OverlayField field)
    {
        if (_context is null || field is null) return;
        _context.HexEditor.SetSelection(field.Offset, field.Offset + field.Length - 1);
    }
}

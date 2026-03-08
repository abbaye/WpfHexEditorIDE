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
//     Only feeds bytes when the panel is visible; cancels in-flight work on new events.
//
// Architecture Notes:
//     Pattern: Observer + Adapter — host events drive panel; panel events drive host.
//     Fixes #167 — added IsPanelVisible guard and CancellationToken to avoid wasted work.
// ==========================================================

using System.Threading.Tasks;
using System.Windows.Threading;
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
    private CancellationTokenSource? _cts;

    private const string PanelUiId = "WpfHexEditor.Plugins.StructureOverlay.Panel.StructureOverlayPanel";

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
            PanelUiId,
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
                Command    = new RelayCommand(_ => context.UIRegistry.ShowPanel(PanelUiId))
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

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------

    private async void OnFileOpened(object? sender, EventArgs e)
    {
        if (_panel is null || _context is null || !_context.HexEditor.IsActive) return;

        // Skip expensive I/O when the panel is not visible (closed or auto-hidden).
        if (!_context.UIRegistry.IsPanelVisible(PanelUiId)) return;

        // Cancel any in-flight byte transfer and start a fresh run.
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        var hexEditor = _context.HexEditor;
        var panel     = _panel;
        var readLen   = (int)Math.Min(hexEditor.FileSize, 1_048_576);

        // Off-load to thread pool; retrieve bytes on the UI thread (DependencyObject
        // affinity), then hand the buffer to the panel without blocking the render pass.
        await Task.Run(async () =>
        {
            if (ct.IsCancellationRequested || !hexEditor.IsActive) return;
            var data = readLen > 0
                ? await panel.Dispatcher.InvokeAsync(() => hexEditor.ReadBytes(0, readLen), DispatcherPriority.Background, ct)
                : [];
            if (ct.IsCancellationRequested) return;
            await panel.Dispatcher.InvokeAsync(() => panel.UpdateFileBytes(data), DispatcherPriority.Background, ct);
        }, ct);
    }

    private void OnFieldSelectedForHighlight(object? sender, OverlayField field)
    {
        if (_context is null || field is null) return;
        _context.HexEditor.SetSelection(field.Offset, field.Offset + field.Length - 1);
    }
}

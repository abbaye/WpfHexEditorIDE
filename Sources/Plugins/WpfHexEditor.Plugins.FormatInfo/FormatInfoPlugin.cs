// ==========================================================
// Project: WpfHexEditor.Plugins.FormatInfo
// File: FormatInfoPlugin.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-07
// Description:
//     Plugin entry point for the Enriched Format Info panel.
//     Subscribes to IHexEditorService.FormatDetected / FileOpened events
//     to push rich format metadata into EnrichedFormatInfoPanel.
//
// Architecture Notes:
//     FormatDetectedArgs.RawFormatDefinition carries the full Core FormatDefinition
//     object (bundled-plugin privilege). We cast it here — external/sandboxed plugins
//     must not rely on this field.
// ==========================================================

using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Plugins.FormatInfo.Properties;
using WpfHexEditor.Plugins.FormatInfo.Views;

namespace WpfHexEditor.Plugins.FormatInfo;

/// <summary>
/// Official plugin wrapping the Enriched Format Info panel.
/// Displays rich format metadata when the HexEditor detects a file format.
/// </summary>
public sealed class FormatInfoPlugin : IWpfHexEditorPlugin
{
    private IIDEHostContext?        _context;
    private EnrichedFormatInfoPanel? _panel;

    public string  Id      => "WpfHexEditor.Plugins.FormatInfo";
    public string  Name    => FormatInfoResources.FormatInfo_PluginName;
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
        _panel   = new EnrichedFormatInfoPanel();

        context.UIRegistry.RegisterPanel(
            "WpfHexEditor.Plugins.FormatInfo.Panel.EnrichedFormatInfoPanel",
            _panel,
            Id,
            new PanelDescriptor
            {
                Title           = FormatInfoResources.FormatInfo_PluginName,
                DefaultDockSide = "Right",
                DefaultAutoHide = true,
                CanClose        = true
            });

        // Register View menu item so the user can show/hide this panel.
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.Show",
            Id,
            new MenuItemDescriptor
            {
                Header     = "Format _Info",
                ParentPath = "View",
                Group      = "Analysis",
                IconGlyph  = "\uE946",
                Command    = new RelayCommand(_ => context.UIRegistry.ShowPanel(
                                 "WpfHexEditor.Plugins.FormatInfo.Panel.EnrichedFormatInfoPanel"))
            });

        context.HexEditor.FormatDetected += OnFormatDetected;
        context.HexEditor.FileOpened     += OnFileOpened;

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        if (_context is not null)
        {
            _context.HexEditor.FormatDetected -= OnFormatDetected;
            _context.HexEditor.FileOpened     -= OnFileOpened;
        }
        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------

    private void OnFormatDetected(object? sender, FormatDetectedArgs e)
    {
        if (_panel is null) return;

        // RawFormatDefinition is populated by HexEditorServiceImpl — bundled plugins may cast it.
        var format = e.RawFormatDefinition as FormatDefinition;

        _panel.Dispatcher.BeginInvoke(() => _panel.SetFormat(format));
    }

    private void OnFileOpened(object? sender, EventArgs e)
    {
        if (_panel is null) return;

        // Clear stale format info when a new file opens (before detection completes)
        _panel.Dispatcher.BeginInvoke(() => _panel.ClearFormat());
    }
}

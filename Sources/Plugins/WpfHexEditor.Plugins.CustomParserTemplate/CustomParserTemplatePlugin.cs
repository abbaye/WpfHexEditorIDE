// ==========================================================
// Project: WpfHexEditor.Plugins.CustomParserTemplate
// File: CustomParserTemplatePlugin.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-07
// Description:
//     Plugin entry point for the Custom Parser Template panel.
//     When the user clicks "Apply Template", the plugin publishes a
//     TemplateApplyRequestedEvent on IPluginEventBus. MainWindow subscribes
//     to that event and routes the parsed blocks to ParsedFieldsPanel.
//
// Architecture Notes:
//     Decoupled via IPluginEventBus — this plugin has no direct reference
//     to MainWindow or ParsedFieldsPanel (which stays in MainWindow).
//     Pattern: Mediator (EventBus) between plugin panel and MainWindow.
// ==========================================================

using System.Linq;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Events;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.Plugins.CustomParserTemplate.Views;

namespace WpfHexEditor.Plugins.CustomParserTemplate;

/// <summary>
/// Official plugin wrapping the Custom Parser Template panel.
/// Bridges template application to the MainWindow-managed ParsedFieldsPanel
/// via the <see cref="IPluginEventBus"/>.
/// </summary>
public sealed class CustomParserTemplatePlugin : IWpfHexEditorPlugin
{
    private IIDEHostContext?           _context;
    private CustomParserTemplatePanel? _panel;

    public string  Id      => "WpfHexEditor.Plugins.CustomParserTemplate";
    public string  Name    => "Custom Parser Template";
    public Version Version => new(0, 4, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor  = true,
        AccessFileSystem = true,
        RegisterMenus    = true,
        WriteOutput      = true
    };

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;
        _panel   = new CustomParserTemplatePanel();

        // Bridge: template applied in panel → publish to EventBus for MainWindow
        _panel.TemplateApplyRequested += OnTemplateApplyRequested;

        context.UIRegistry.RegisterPanel(
            "WpfHexEditor.Plugins.CustomParserTemplate.Panel.CustomParserTemplatePanel",
            _panel,
            Id,
            new PanelDescriptor
            {
                Title           = "Custom Parser Template",
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
                Header     = "_Custom Parser Template",
                ParentPath = "View",
                Group      = "FileTools",
                IconGlyph  = "\uE9A1",
                Command    = new RelayCommand(_ => context.UIRegistry.ShowPanel(
                                 "WpfHexEditor.Plugins.CustomParserTemplate.Panel.CustomParserTemplatePanel"))
            });

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        if (_panel is not null)
            _panel.TemplateApplyRequested -= OnTemplateApplyRequested;
        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------

    private void OnTemplateApplyRequested(object? sender, TemplateApplyEventArgs e)
    {
        if (_context is null || e.Template is null) return;

        // Map CustomBlock list → SDK ParsedBlockInfo (no direct Core dependency)
        var blocks = e.Template.Blocks?
            .Select(b => new ParsedBlockInfo
            {
                Name         = b.Name         ?? string.Empty,
                Offset       = b.Offset,
                Length       = b.Length,
                TypeHint     = b.ValueType,
                DisplayValue = b.Description
            })
            .ToList() ?? new List<ParsedBlockInfo>();

        _context.EventBus.Publish(new TemplateApplyRequestedEvent
        {
            TemplateName = e.Template.Name ?? string.Empty,
            Blocks       = blocks
        });
    }
}

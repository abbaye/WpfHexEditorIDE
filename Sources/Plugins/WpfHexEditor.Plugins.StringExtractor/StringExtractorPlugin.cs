// ==========================================================
// Project: WpfHexEditor.Plugins.StringExtractor
// File: StringExtractorPlugin.cs
// Description: Plugin entry point for the String Extractor panel.
//              Registers the panel and wires HexEditor events.
// Architecture Notes:
//     Standalone-safe: No nullable IDE services are required.
//     The panel works with context.HexEditor alone (always non-null).
//     Navigation callback is registered via panel.SetContext() which
//     is only called when a full context is available.
// ==========================================================

using WpfHexEditor.Plugins.StringExtractor.Properties;
using WpfHexEditor.Plugins.StringExtractor.Views;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.Plugins.StringExtractor;

/// <summary>
/// Plugin registering the String Extractor panel (Bottom dock).
/// </summary>
public sealed class StringExtractorPlugin : IWpfHexEditorPlugin
{
    public string  Id      => "WpfHexEditor.Plugins.StringExtractor";
    public string  Name    => StringExtractorResources.StringExtractor_PluginName;
    public Version Version => new(1, 0, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor  = true,
        AccessFileSystem = true,
        RegisterMenus    = true,
        WriteOutput      = true
    };

    private StringExtractorPanel? _panel;
    private IIDEHostContext?       _context;

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;
        _panel   = new StringExtractorPanel();
        _panel.SetContext(context);

        context.UIRegistry.RegisterPanel(
            "WpfHexEditor.Plugins.StringExtractor.Panel",
            _panel,
            Id,
            new PanelDescriptor
            {
                Title           = StringExtractorResources.StringExtractor_PanelTitle,
                DefaultDockSide = "Bottom",
                DefaultAutoHide = false,
                CanClose        = true,
                PreferredHeight = 250
            });

        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.Show",
            Id,
            new MenuItemDescriptor
            {
                Header     = StringExtractorResources.StringExtractor_MenuItem,
                ParentPath = "View",
                Group      = "Analysis",
                IconGlyph  = "",
                Command    = new RelayCommand(_ => context.UIRegistry.ShowPanel(
                                 "WpfHexEditor.Plugins.StringExtractor.Panel"))
            });

        context.HexEditor.FileOpened          += OnFileOpened;
        context.HexEditor.ActiveEditorChanged += OnActiveEditorChanged;

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        if (_context is not null)
        {
            _context.HexEditor.FileOpened          -= OnFileOpened;
            _context.HexEditor.ActiveEditorChanged -= OnActiveEditorChanged;
        }
        _panel   = null;
        _context = null;
        return Task.CompletedTask;
    }

    private void OnFileOpened(object? sender, EventArgs e)
        => _panel?.OnFileOpened();

    private void OnActiveEditorChanged(object? sender, EventArgs e)
        => _panel?.OnFileOpened();
}

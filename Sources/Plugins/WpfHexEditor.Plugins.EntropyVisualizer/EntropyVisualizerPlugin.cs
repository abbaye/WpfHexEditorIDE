// ==========================================================
// Project: WpfHexEditor.Plugins.EntropyVisualizer
// File: EntropyVisualizerPlugin.cs
// Description: Plugin entry point for the Entropy Visualizer panel.
//              Registers the panel and wires HexEditor file events.
// Architecture Notes:
//     Standalone-safe: No nullable IDE services are required.
//     The panel works with context.HexEditor alone (always non-null).
// ==========================================================

using WpfHexEditor.Plugins.EntropyVisualizer.Properties;
using WpfHexEditor.Plugins.EntropyVisualizer.Views;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.Plugins.EntropyVisualizer;

public sealed class EntropyVisualizerPlugin : IWpfHexEditorPlugin
{
    private const string PanelId = "WpfHexEditor.Plugins.EntropyVisualizer.Panel";

    public string  Id      => "WpfHexEditor.Plugins.EntropyVisualizer";
    public string  Name    => EntropyVisualizerResources.EntropyVisualizer_PluginName;
    public Version Version => new(1, 0, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor  = true,
        AccessFileSystem = false,
        RegisterMenus    = true,
        WriteOutput      = false
    };

    private EntropyVisualizerPanel? _panel;
    private IIDEHostContext?        _context;

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;
        _panel   = new EntropyVisualizerPanel();
        _panel.SetContext(context);

        context.UIRegistry.RegisterPanel(
            PanelId,
            _panel,
            Id,
            new PanelDescriptor
            {
                Title           = EntropyVisualizerResources.EntropyVisualizer_PanelTitle,
                DefaultDockSide = "Bottom",
                DefaultAutoHide = false,
                CanClose        = true,
                PreferredHeight = 280
            });

        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.Show",
            Id,
            new MenuItemDescriptor
            {
                Header     = EntropyVisualizerResources.EntropyVisualizer_MenuItem,
                ParentPath = "View",
                Group      = "Analysis",
                IconGlyph  = "",
                Command    = new RelayCommand(_ => context.UIRegistry.ShowPanel(PanelId))
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

    private void OnFileOpened(object? sender, EventArgs e)          => _panel?.OnFileOpened();
    private void OnActiveEditorChanged(object? sender, EventArgs e) => _panel?.OnFileOpened();
}

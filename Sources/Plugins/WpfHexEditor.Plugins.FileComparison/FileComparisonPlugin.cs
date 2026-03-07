//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.Plugins.FileComparison.Views;

namespace WpfHexEditor.Plugins.FileComparison;

/// <summary>
/// Official plugin wrapping the File Comparison panel(s).
/// </summary>
public sealed class FileComparisonPlugin : IWpfHexEditorPlugin
{
    public string Id      => "WpfHexEditor.Plugins.FileComparison";
    public string Name    => "File Comparison";
    public Version Version => new(0, 2, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor  = true,
        AccessFileSystem = true,
        RegisterMenus    = true,
        WriteOutput      = true
    };

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        context.UIRegistry.RegisterPanel(
            "WpfHexEditor.Plugins.FileComparison.Panel.FileComparisonPanel",
            new FileComparisonPanel(),
            Id,
            new PanelDescriptor
            {
                Title           = "File Comparison",
                DefaultDockSide = "Bottom",
                DefaultAutoHide = true,
                CanClose        = true
            });

        // Register View menu item so the user can show/hide this panel.
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.Show",
            Id,
            new MenuItemDescriptor
            {
                Header     = "File _Comparison",
                ParentPath = "View",
                Group      = "FileTools",
                IconGlyph  = "\uE93D",
                Command    = new RelayCommand(_ => context.UIRegistry.ShowPanel(
                                 "WpfHexEditor.Plugins.FileComparison.Panel.FileComparisonPanel"))
            });

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        // UIRegistry.UnregisterAllForPlugin is called automatically by PluginHost.
        return Task.CompletedTask;
    }
}

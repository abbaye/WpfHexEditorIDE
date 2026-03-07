//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.Plugins.FileStatistics.Views;

namespace WpfHexEditor.Plugins.FileStatistics;

/// <summary>
/// Official plugin wrapping the File Statistics panel(s).
/// </summary>
public sealed class FileStatisticsPlugin : IWpfHexEditorPlugin
{
    public string Id      => "WpfHexEditor.Plugins.FileStatistics";
    public string Name    => "File Statistics";
    public Version Version => new(1, 0, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor  = true,
        AccessFileSystem = false,
        RegisterMenus    = true,
        WriteOutput      = true
    };

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        context.UIRegistry.RegisterPanel(
            "WpfHexEditor.Plugins.FileStatistics.Panel.FileStatisticsPanel",
            new FileStatisticsPanel(),
            Id,
            new PanelDescriptor { Title = "FileStatistics", DefaultDockSide = "Right", CanClose = true });
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        // UIRegistry.UnregisterAllForPlugin is called automatically by PluginHost.
        return Task.CompletedTask;
    }
}

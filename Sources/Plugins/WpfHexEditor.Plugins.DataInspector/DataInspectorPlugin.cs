//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.Plugins.DataInspector.Views;

namespace WpfHexEditor.Plugins.DataInspector;

/// <summary>
/// Official plugin wrapping the Data Inspector panel(s).
/// </summary>
public sealed class DataInspectorPlugin : IWpfHexEditorPlugin
{
    public string Id      => "WpfHexEditor.Plugins.DataInspector";
    public string Name    => "Data Inspector";
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
            "WpfHexEditor.Plugins.DataInspector.Panel.DataInspectorPanel",
            new DataInspectorPanel(),
            Id,
            new PanelDescriptor { Title = "DataInspector", DefaultDockSide = "Right", CanClose = true });
        context.UIRegistry.RegisterPanel(
            "WpfHexEditor.Plugins.DataInspector.Panel.ByteChartPanel",
            new ByteChartPanel(),
            Id,
            new PanelDescriptor { Title = "ByteChart", DefaultDockSide = "Right", CanClose = true });
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        // UIRegistry.UnregisterAllForPlugin is called automatically by PluginHost.
        return Task.CompletedTask;
    }
}

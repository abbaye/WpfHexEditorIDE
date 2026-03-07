//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.Plugins.DataInspector.Options;
using WpfHexEditor.Plugins.DataInspector.Views;

namespace WpfHexEditor.Plugins.DataInspector;

/// <summary>
/// Official plugin wrapping the Data Inspector panel(s).
/// Implements IPluginWithOptions to expose its settings in the IDE Options panel
/// and in the Plugin Manager "Settings" tab.
/// </summary>
public sealed class DataInspectorPlugin : IWpfHexEditorPlugin, IPluginWithOptions
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

    // -- IPluginWithOptions --------------------------------------------------

    private DataInspectorOptionsPage? _optionsPage;

    /// <summary>Creates (or returns a new instance of) the options UI for this plugin.</summary>
    public FrameworkElement CreateOptionsPage()
    {
        _optionsPage = new DataInspectorOptionsPage();
        _optionsPage.Load();
        return _optionsPage;
    }

    /// <summary>Persists the current state of the options page.</summary>
    public void SaveOptions() => _optionsPage?.Save();

    /// <summary>Reloads options from disk into the cached options page (if alive).</summary>
    public void LoadOptions() => _optionsPage?.Load();
}

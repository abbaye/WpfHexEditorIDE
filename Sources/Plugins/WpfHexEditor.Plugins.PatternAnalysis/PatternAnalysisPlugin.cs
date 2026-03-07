//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.Plugins.PatternAnalysis.Views;

namespace WpfHexEditor.Plugins.PatternAnalysis;

/// <summary>
/// Official plugin wrapping the Pattern Analysis panel(s).
/// </summary>
public sealed class PatternAnalysisPlugin : IWpfHexEditorPlugin
{
    public string Id      => "WpfHexEditor.Plugins.PatternAnalysis";
    public string Name    => "Pattern Analysis";
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
            "WpfHexEditor.Plugins.PatternAnalysis.Panel.PatternAnalysisPanel",
            new PatternAnalysisPanel(),
            Id,
            new PanelDescriptor { Title = "PatternAnalysis", DefaultDockSide = "Right", CanClose = true });
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        // UIRegistry.UnregisterAllForPlugin is called automatically by PluginHost.
        return Task.CompletedTask;
    }
}

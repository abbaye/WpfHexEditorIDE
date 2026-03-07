//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.Plugins.StructureOverlay.Views;

namespace WpfHexEditor.Plugins.StructureOverlay;

/// <summary>
/// Official plugin wrapping the Structure Overlay panel(s).
/// </summary>
public sealed class StructureOverlayPlugin : IWpfHexEditorPlugin
{
    public string Id      => "WpfHexEditor.Plugins.StructureOverlay";
    public string Name    => "Structure Overlay";
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
            "WpfHexEditor.Plugins.StructureOverlay.Panel.StructureOverlayPanel",
            new StructureOverlayPanel(),
            Id,
            new PanelDescriptor { Title = "StructureOverlay", DefaultDockSide = "Right", CanClose = true });
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        // UIRegistry.UnregisterAllForPlugin is called automatically by PluginHost.
        return Task.CompletedTask;
    }
}

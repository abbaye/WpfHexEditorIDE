//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.Plugins.ArchiveStructure.Views;

namespace WpfHexEditor.Plugins.ArchiveStructure;

/// <summary>
/// Official plugin wrapping the Archive Structure panel(s).
/// </summary>
public sealed class ArchiveStructurePlugin : IWpfHexEditorPlugin
{
    public string Id      => "WpfHexEditor.Plugins.ArchiveStructure";
    public string Name    => "Archive Structure";
    public Version Version => new(1, 0, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor  = false,
        AccessFileSystem = true,
        RegisterMenus    = true,
        WriteOutput      = true
    };

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        context.UIRegistry.RegisterPanel(
            "WpfHexEditor.Plugins.ArchiveStructure.Panel.ArchiveStructurePanel",
            new ArchiveStructurePanel(),
            Id,
            new PanelDescriptor { Title = "ArchiveStructure", DefaultDockSide = "Right", CanClose = true });
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        // UIRegistry.UnregisterAllForPlugin is called automatically by PluginHost.
        return Task.CompletedTask;
    }
}

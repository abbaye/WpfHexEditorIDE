//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.Plugins.CustomParserTemplate.Views;

namespace WpfHexEditor.Plugins.CustomParserTemplate;

/// <summary>
/// Official plugin wrapping the Custom Parser Template panel(s).
/// </summary>
public sealed class CustomParserTemplatePlugin : IWpfHexEditorPlugin
{
    public string Id      => "WpfHexEditor.Plugins.CustomParserTemplate";
    public string Name    => "Custom Parser Template";
    public Version Version => new(1, 0, 0);

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
            "WpfHexEditor.Plugins.CustomParserTemplate.Panel.CustomParserTemplatePanel",
            new CustomParserTemplatePanel(),
            Id,
            new PanelDescriptor { Title = "CustomParserTemplate", DefaultDockSide = "Right", CanClose = true });
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        // UIRegistry.UnregisterAllForPlugin is called automatically by PluginHost.
        return Task.CompletedTask;
    }
}

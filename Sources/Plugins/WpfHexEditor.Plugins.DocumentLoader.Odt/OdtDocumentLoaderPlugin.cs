// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentLoader.Odt
// File: OdtDocumentLoaderPlugin.cs
// Description:
//     Plugin entry point. Registers OdtDocumentLoader as an
//     IDocumentLoader extension via IExtensionRegistry on startup.
// ==========================================================

using WpfHexEditor.Editor.Core;
using WpfHexEditor.Plugins.DocumentLoader.Odt.Parsers;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.SDK.Services;

namespace WpfHexEditor.Plugins.DocumentLoader.Odt;

/// <summary>
/// Registers <see cref="OdtDocumentLoader"/> as an <see cref="IDocumentLoader"/>
/// extension point so the <c>DocumentEditorHost</c> can auto-select it for .odt/.ott files.
/// </summary>
public sealed class OdtDocumentLoaderPlugin : IWpfHexEditorPlugin
{
    private OdtDocumentLoader? _loader;

    public string  Id      => "WpfHexEditor.Plugins.DocumentLoader.Odt";
    public string  Name    => "ODT Document Loader";
    public Version Version => new(1, 0, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessFileSystem = true,
        WriteOutput      = false,
        RegisterMenus    = false,
    };

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _loader = new OdtDocumentLoader();
        context.ExtensionRegistry.Register<IDocumentLoader>(Id, _loader);
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        _loader = null;
        return Task.CompletedTask;
    }
}

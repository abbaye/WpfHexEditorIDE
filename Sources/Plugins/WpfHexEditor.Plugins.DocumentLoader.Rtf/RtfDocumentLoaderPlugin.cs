// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentLoader.Rtf
// File: RtfDocumentLoaderPlugin.cs
// Description:
//     Plugin entry point. Registers RtfDocumentLoader as an
//     IDocumentLoader extension via IExtensionRegistry on startup.
//     Mirrors the FolderSolutionLoaderPlugin registration pattern.
// ==========================================================

using WpfHexEditor.Editor.Core;
using WpfHexEditor.Plugins.DocumentLoader.Rtf.Parsers;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.SDK.Services;

namespace WpfHexEditor.Plugins.DocumentLoader.Rtf;

/// <summary>
/// Registers <see cref="RtfDocumentLoader"/> as an <see cref="IDocumentLoader"/>
/// extension point so the <c>DocumentEditorHost</c> can auto-select it for .rtf files.
/// </summary>
public sealed class RtfDocumentLoaderPlugin : IWpfHexEditorPlugin
{
    private RtfDocumentLoader? _loader;

    public string  Id      => "WpfHexEditor.Plugins.DocumentLoader.Rtf";
    public string  Name    => "RTF Document Loader";
    public Version Version => new(1, 0, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessFileSystem = true,
        WriteOutput      = false,
        RegisterMenus    = false,
    };

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _loader = new RtfDocumentLoader();
        context.ExtensionRegistry.Register<IDocumentLoader>(Id, _loader);
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        _loader = null;
        return Task.CompletedTask;
    }
}

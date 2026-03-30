// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentLoader.Docx
// File: DocxDocumentLoaderPlugin.cs
// Description:
//     Plugin entry point. Registers DocxDocumentLoader as an
//     IDocumentLoader extension via IExtensionRegistry on startup.
// ==========================================================

using WpfHexEditor.Editor.Core;
using WpfHexEditor.Plugins.DocumentLoader.Docx.Parsers;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.Editor.DocumentEditor.Core;

namespace WpfHexEditor.Plugins.DocumentLoader.Docx;

/// <summary>
/// Registers <see cref="DocxDocumentLoader"/> as an <see cref="IDocumentLoader"/>
/// extension point so the <c>DocumentEditorHost</c> can auto-select it for .docx/.dotx files.
/// </summary>
public sealed class DocxDocumentLoaderPlugin : IWpfHexEditorPlugin
{
    private DocxDocumentLoader? _loader;

    public string  Id      => "WpfHexEditor.Plugins.DocumentLoader.Docx";
    public string  Name    => "DOCX Document Loader";
    public Version Version => new(1, 0, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessFileSystem = true,
        WriteOutput      = false,
        RegisterMenus    = false,
    };

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _loader = new DocxDocumentLoader();
        context.ExtensionRegistry.Register<IDocumentLoader>(Id, _loader);
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        _loader = null;
        return Task.CompletedTask;
    }
}

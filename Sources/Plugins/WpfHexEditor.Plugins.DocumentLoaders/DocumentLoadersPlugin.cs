// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentLoaders
// File: DocumentLoadersPlugin.cs
// Description:
//     Unified plugin entry point that registers the RTF, DOCX, and ODT
//     document loaders as IDocumentLoader extensions via IExtensionRegistry.
//     Replaces the three separate DocumentLoader.Rtf/Docx/Odt plugins.
// ==========================================================

using WpfHexEditor.Editor.DocumentEditor.Core;
using WpfHexEditor.Plugins.DocumentLoaders.Parsers.Docx;
using WpfHexEditor.Plugins.DocumentLoaders.Parsers.Odt;
using WpfHexEditor.Plugins.DocumentLoaders.Parsers.Rtf;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.Plugins.DocumentLoaders;

/// <summary>
/// Registers <see cref="RtfDocumentLoader"/>, <see cref="DocxDocumentLoader"/>,
/// and <see cref="OdtDocumentLoader"/> as <see cref="IDocumentLoader"/> extension
/// points so the <c>DocumentEditorHost</c> can auto-select them by file extension.
/// </summary>
public sealed class DocumentLoadersPlugin : IWpfHexEditorPlugin
{
    public string  Id      => "WpfHexEditor.Plugins.DocumentLoaders";
    public string  Name    => "Document Loaders (RTF / DOCX / ODT)";
    public Version Version => new(1, 0, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessFileSystem = true,
        WriteOutput      = true,
        RegisterMenus    = false,
    };

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        context.ExtensionRegistry.Register<IDocumentLoader>(Id + ".Rtf",  new RtfDocumentLoader());
        context.ExtensionRegistry.Register<IDocumentLoader>(Id + ".Docx", new DocxDocumentLoader());
        context.ExtensionRegistry.Register<IDocumentLoader>(Id + ".Odt",  new OdtDocumentLoader());
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default) => Task.CompletedTask;
}

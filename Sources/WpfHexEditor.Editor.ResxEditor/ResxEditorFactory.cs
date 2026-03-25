// ==========================================================
// Project: WpfHexEditor.Editor.ResxEditor
// File: ResxEditorFactory.cs
// Description:
//     IEditorFactory implementation for the RESX/RESW grid editor.
//     Factory Id "resx-editor" must match preferredEditor in
//     RESX.whfmt and RESW.whfmt.
//     EventBus delegates are injected into the editor instance
//     after creation so the control does not hold a direct
//     reference to the app-level IIDEEventBus.
//
//     Command registration for the 10 resx.* commands is performed
//     from MainWindow.Build.cs (which has access to RelayCommand
//     and the active-editor resolver) to keep this project free of
//     the WpfHexEditor.Commands dependency.
// Architecture:
//     IEditorFactory → IDocumentEditor → ResxEditor (UserControl)
//     ResxEditorDescriptor is file-scoped (internal to this TU).
// ==========================================================

using System.IO;
using System.Linq;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.ResxEditor.Models;
using WpfHexEditor.Editor.ResxEditor.Controls;
using WpfHexEditor.Core.Events.IDEEvents;
using ResxEditorControl = WpfHexEditor.Editor.ResxEditor.Controls.ResxEditor;

namespace WpfHexEditor.Editor.ResxEditor;

/// <summary>
/// Factory that creates <see cref="ResxEditor"/> instances.
/// </summary>
public sealed class ResxEditorFactory : IEditorFactory
{
    private static readonly IEditorDescriptor _descriptor = new ResxEditorDescriptor();

    // -----------------------------------------------------------------------
    // Optional event-bus publish delegates — set by the host after wiring
    // -----------------------------------------------------------------------

    /// <summary>
    /// Called by the host to provide EventBus publish capabilities.
    /// Signature: (ResxEntryChangedEvent e) — called on entry add/edit/delete.
    /// </summary>
    public Action<ResxEntryChangedEvent>?    PublishEntryChanged     { get; set; }

    /// <summary>
    /// Signature: (ResxLocaleDiscoveredEvent e) — called when locale siblings are discovered.
    /// </summary>
    public Action<ResxLocaleDiscoveredEvent>?  PublishLocaleDiscovered { get; set; }

    /// <summary>
    /// Signature: (ResxDesignerGeneratedEvent e) — called after Designer.cs is written.
    /// </summary>
    public Action<ResxDesignerGeneratedEvent>? PublishDesignerGenerated { get; set; }

    // -----------------------------------------------------------------------
    // IEditorFactory
    // -----------------------------------------------------------------------

    public IEditorDescriptor Descriptor => _descriptor;

    public bool CanOpen(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return ext is ".resx" or ".resw";
    }

    public IDocumentEditor Create()
    {
        var editor = new ResxEditorControl();

        // Wire EventBus publish delegates so the editor can fire IDE-wide events.
        // The editor uses simpler delegate types (no Events assembly dep); the factory
        // adapts between them and the typed IDE event records.
        if (PublishEntryChanged is not null)
            editor.OnSavePublish = fp => PublishEntryChanged(new ResxEntryChangedEvent
            {
                FilePath   = fp,
                ChangeType = ResxEntryChangeType.Edited
            });

        if (PublishLocaleDiscovered is not null)
            editor.OnLocalePublish = set => PublishLocaleDiscovered(new ResxLocaleDiscoveredEvent
            {
                BasePath = set.BasePath,
                Variants = set.Variants.Select(v => v.Path).ToArray()
            });

        if (PublishDesignerGenerated is not null)
            editor.OnDesignerPublish = (fp, dp) => PublishDesignerGenerated(new ResxDesignerGeneratedEvent
            {
                FilePath     = fp,
                DesignerPath = dp
            });

        return editor;
    }
}

// ---------------------------------------------------------------------------
// Editor descriptor (file-scoped — not exported from this assembly)
// ---------------------------------------------------------------------------

file sealed class ResxEditorDescriptor : IEditorDescriptor
{
    public string Id          => "resx-editor";
    public string DisplayName => "RESX Editor";
    public string Description => ".NET XML resource file editor (strings, images, binary blobs)";
    public IReadOnlyList<string> SupportedExtensions => [".resx", ".resw"];
}

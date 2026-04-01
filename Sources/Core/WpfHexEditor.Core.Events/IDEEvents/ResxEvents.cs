// ==========================================================
// Project: WpfHexEditor.Core.Events
// File: IDEEvents/ResxEvents.cs
// Description:
//     IDE-wide events published by the RESX editor.
//     Consumed by WpfHexEditor.Plugins.ResxLocalization
//     to keep the Locale Browser and Missing Translations
//     panels in sync without polling the file system.
// ==========================================================

namespace WpfHexEditor.Core.Events.IDEEvents;

/// <summary>Change type for a RESX entry mutation.</summary>
public enum ResxEntryChangeType { Added, Deleted, Edited }

/// <summary>
/// Published when a RESX entry is added, deleted, or edited.
/// Enables plugins to react to resource-table mutations in real time.
/// </summary>
public sealed record ResxEntryChangedEvent : IDEEventBase
{
    public string FilePath    { get; init; } = string.Empty;
    public string Key         { get; init; } = string.Empty;
    public ResxEntryChangeType ChangeType { get; init; }
}

/// <summary>
/// Published when <c>ResxLocaleDiscovery</c> finds sibling locale files
/// (e.g. Resources.fr.resx, Resources.de-DE.resx) next to the base file.
/// </summary>
public sealed record ResxLocaleDiscoveredEvent : IDEEventBase
{
    public string   BasePath { get; init; } = string.Empty;
    public string[] Variants { get; init; } = [];
}

/// <summary>
/// Published after Designer.cs is successfully generated and written to disk.
/// </summary>
public sealed record ResxDesignerGeneratedEvent : IDEEventBase
{
    public string FilePath     { get; init; } = string.Empty;
    public string DesignerPath { get; init; } = string.Empty;
}

// ==========================================================
// Project: WpfHexEditor.SDK
// File: Events/OpenAssemblyInExplorerEvent.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     EventBus message that requests the Assembly Explorer plugin to load
//     a specific PE file. Published by any plugin (e.g., SolutionExplorer)
//     that wants to trigger assembly analysis without a direct reference
//     to the AssemblyExplorer plugin.
//
// Architecture Notes:
//     Pattern: Observer / Pub-Sub via IPluginEventBus.
//     Subscriber: AssemblyExplorerPlugin.OnOpenAssemblyRequested.
//     Publisher: any plugin with a reference to WpfHexEditor.SDK.
// ==========================================================

namespace WpfHexEditor.SDK.Events;

/// <summary>
/// Requests the Assembly Explorer panel to open and analyse the specified file.
/// </summary>
public sealed class OpenAssemblyInExplorerEvent
{
    /// <summary>
    /// Absolute path to the .dll or .exe file to analyse.
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// When <see langword="true"/>, the Assembly Explorer panel is brought
    /// to the foreground after loading.  Defaults to <see langword="true"/>.
    /// </summary>
    public bool BringToFront { get; init; } = true;
}

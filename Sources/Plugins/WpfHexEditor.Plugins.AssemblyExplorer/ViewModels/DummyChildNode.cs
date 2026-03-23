// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/DummyChildNode.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Sentinel tree node inserted as the sole child of a lazy-loading parent
//     so the WPF TreeView renders an expand arrow before real children are loaded.
//     Never displayed to the user — replaced by real children on first expand.
//
// Architecture Notes:
//     Pattern: Null Object / sentinel node (virtual tree lazy-load pattern).
//     The node is identified via 'is DummyChildNode' in HasDummyChild.
// ==========================================================

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

/// <summary>
/// Sentinel placeholder child used to force the WPF TreeView expand arrow to appear
/// before the real children of a lazy-loading node are loaded.
/// </summary>
internal sealed class DummyChildNode : AssemblyNodeViewModel
{
    public override string DisplayName => string.Empty;
    public override string IconGlyph   => string.Empty;
}

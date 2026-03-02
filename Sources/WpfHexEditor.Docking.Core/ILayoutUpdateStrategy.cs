//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6, Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Docking.Core;

/// <summary>
/// Strategy hook for customizing how items are inserted into the layout.
/// Set on the dock host to intercept document/anchorable insertion from
/// <c>DocumentsSource</c> / <c>AnchorablesSource</c> and layout deserialization.
/// </summary>
public interface ILayoutUpdateStrategy
{
    /// <summary>
    /// Called before a document item is inserted into the layout.
    /// Return <c>true</c> if the strategy handled the insertion (the default logic is skipped).
    /// Return <c>false</c> to let the default logic proceed.
    /// </summary>
    /// <param name="layout">The current layout root.</param>
    /// <param name="item">The item about to be inserted.</param>
    /// <param name="target">The default target group (usually <see cref="DockLayoutRoot.MainDocumentHost"/>).</param>
    bool BeforeInsertDocument(DockLayoutRoot layout, DockItem item, DockGroupNode target);

    /// <summary>
    /// Called before an anchorable (tool) item is inserted into the layout.
    /// Return <c>true</c> if the strategy handled the insertion (the default logic is skipped).
    /// Return <c>false</c> to let the default logic proceed.
    /// </summary>
    /// <param name="layout">The current layout root.</param>
    /// <param name="item">The item about to be inserted.</param>
    /// <param name="target">The default target group.</param>
    bool BeforeInsertAnchorable(DockLayoutRoot layout, DockItem item, DockGroupNode target);

    /// <summary>
    /// Called after a layout has been deserialized and applied.
    /// Use this to perform post-load adjustments (e.g. re-position items, apply constraints).
    /// </summary>
    /// <param name="layout">The deserialized layout root.</param>
    void AfterLayoutDeserialized(DockLayoutRoot layout);
}

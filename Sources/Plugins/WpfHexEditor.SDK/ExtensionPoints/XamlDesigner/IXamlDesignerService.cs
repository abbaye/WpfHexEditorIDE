// ==========================================================
// Project: WpfHexEditor.SDK
// File: ExtensionPoints/XamlDesigner/IXamlDesignerService.cs
// Created: 2026-04-06
// Description:
//     SDK bridge between the XAML Designer plugin and any consumer plugin
//     (e.g. Document Structure) that needs access to the live element tree
//     without a direct assembly reference to WpfHexEditor.Plugins.XamlDesigner.
//
// Architecture Notes:
//     Registered by XamlDesignerPlugin via IExtensionRegistry.Register<IXamlDesignerService>.
//     Consumed by DocumentStructurePlugin via IExtensionRegistry.GetExtensions<IXamlDesignerService>.
// ==========================================================

namespace WpfHexEditor.SDK.ExtensionPoints.XamlDesigner;

/// <summary>
/// SDK bridge that exposes the active XAML Designer's element tree and selection
/// to other plugins without requiring a direct assembly reference to the designer.
/// Registered by <c>XamlDesignerPlugin</c> via <see cref="SDK.Contracts.IExtensionRegistry"/>.
/// </summary>
public interface IXamlDesignerService
{
    /// <summary>True when a .xaml file is open and rendered in the XAML Designer (not code-only).</summary>
    bool IsDesignerActive { get; }

    /// <summary>
    /// Returns the root nodes of the current rendered element tree.
    /// Returns an empty list when no designer is active or the canvas has not yet rendered.
    /// </summary>
    IReadOnlyList<XamlDesignerNode> GetElementTree();

    /// <summary>UID of the currently selected element, or -1 when nothing is selected.</summary>
    int SelectedElementUid { get; }

    /// <summary>
    /// Fires when the designer's element tree is rebuilt — on document switch or after
    /// <c>DesignCanvas.DesignRendered</c> (i.e., after a XAML source edit re-render).
    /// </summary>
    event EventHandler? ElementTreeChanged;

    /// <summary>
    /// Fires when the designer's canvas selection changes.
    /// Argument is the new UID (-1 when the selection is cleared).
    /// </summary>
    event EventHandler<int>? SelectedElementChanged;

    /// <summary>Selects the element with the given UID on the active design canvas.</summary>
    void SelectElement(int uid);

    /// <summary>
    /// Selects the element by UID in the designer canvas AND navigates the code editor
    /// to the element's source line.  Also activates (brings to front) the designer tab.
    /// No-op when the UID is invalid or the designer is not active.
    /// </summary>
    void NavigateToElement(int uid) { }
}

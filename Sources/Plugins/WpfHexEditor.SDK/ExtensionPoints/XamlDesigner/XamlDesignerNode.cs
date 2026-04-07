// ==========================================================
// Project: WpfHexEditor.SDK
// File: ExtensionPoints/XamlDesigner/XamlDesignerNode.cs
// Created: 2026-04-06
// Description:
//     Immutable node returned by IXamlDesignerService.GetElementTree().
//     Represents a single WPF UIElement from the live design canvas,
//     with its source line position and designer UID for selection sync.
// ==========================================================

namespace WpfHexEditor.SDK.ExtensionPoints.XamlDesigner;

/// <summary>
/// A single element node from the live XAML Designer canvas.
/// Returned by <see cref="IXamlDesignerService.GetElementTree()"/>.
/// </summary>
public sealed class XamlDesignerNode
{
    /// <summary>Unique integer identifier assigned by <c>XamlElementMapper</c>. Used for selection sync.</summary>
    public required int Uid { get; init; }

    /// <summary>WPF type name of the element (e.g. "Grid", "Button", "StackPanel").</summary>
    public required string TypeName { get; init; }

    /// <summary>Value of the <c>x:Name</c> attribute, or null when the element is anonymous.</summary>
    public string? Name { get; init; }

    /// <summary>1-based source line in the .xaml file. 0 when line info is unavailable.</summary>
    public int StartLine { get; init; }

    /// <summary>1-based source column. 0 when unavailable.</summary>
    public int StartColumn { get; init; }

    /// <summary>Child nodes in the visual hierarchy.</summary>
    public IReadOnlyList<XamlDesignerNode> Children { get; init; } = [];
}

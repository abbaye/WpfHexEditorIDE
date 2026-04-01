// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: DesignOperation.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Immutable record representing a single design-surface operation
//     (Move, Resize, PropertyChange) that can be undone or redone.
//     Captures before/after XAML attribute states keyed by attribute name.
//
// Architecture Notes:
//     Value Object / Record pattern — fully immutable.
//     Before/After dictionaries use XAML attribute names as keys
//     (e.g. "Width", "Height", "Margin", "Canvas.Left").
//     Null value means "remove attribute"; non-null means "set attribute".
// ==========================================================

namespace WpfHexEditor.Editor.XamlDesigner.Models;

/// <summary>
/// Identifies the category of a design-surface operation.
/// </summary>
public enum DesignOperationType
{
    /// <summary>Element was translated (Margin or Canvas.Left/Top changed).</summary>
    Move,

    /// <summary>Element dimensions were changed (Width/Height/Margin changed).</summary>
    Resize,

    /// <summary>A single property was changed via the Property Inspector.</summary>
    PropertyChange,

    /// <summary>Element was inserted from the Toolbox.</summary>
    Insert,

    /// <summary>Element was deleted.</summary>
    Delete,

    /// <summary>Multiple elements were aligned or distributed.</summary>
    Alignment,

    /// <summary>Element was rotated via the rotation handle (RenderTransform.Angle changed).</summary>
    Rotate,
}

/// <summary>
/// Immutable snapshot of a single design operation for undo/redo support.
/// </summary>
/// <param name="Type">The kind of operation performed.</param>
/// <param name="ElementUid">
/// Pre-order UID of the target element in the XAML document tree
/// (assigned during render via Tag injection).
/// </param>
/// <param name="Before">Attribute values before the operation (null value = attribute was absent).</param>
/// <param name="After">Attribute values after the operation (null value = attribute should be removed).</param>
/// <param name="Description">Human-readable label shown in the Undo history.</param>
public sealed record DesignOperation(
    DesignOperationType         Type,
    int                         ElementUid,
    IReadOnlyDictionary<string, string?> Before,
    IReadOnlyDictionary<string, string?> After,
    string                      Description = "")
{
    // ── Factory helpers ───────────────────────────────────────────────────────

    /// <summary>Creates a Move operation from margin/position snapshots.</summary>
    public static DesignOperation CreateMove(
        int uid,
        Dictionary<string, string?> before,
        Dictionary<string, string?> after)
        => new(DesignOperationType.Move, uid, before, after, "Move");

    /// <summary>Creates a Resize operation from size/margin snapshots.</summary>
    public static DesignOperation CreateResize(
        int uid,
        Dictionary<string, string?> before,
        Dictionary<string, string?> after)
        => new(DesignOperationType.Resize, uid, before, after, "Resize");

    /// <summary>Creates a single-property change operation.</summary>
    public static DesignOperation CreatePropertyChange(
        int uid,
        string attributeName,
        string? valueBefore,
        string? valueAfter)
        => new(
            DesignOperationType.PropertyChange,
            uid,
            new Dictionary<string, string?> { [attributeName] = valueBefore },
            new Dictionary<string, string?> { [attributeName] = valueAfter },
            $"Set {attributeName}");

    /// <summary>
    /// Creates a placeholder Insert operation.
    /// The actual XAML change is stored in a <c>SnapshotDesignUndoEntry</c>;
    /// Before/After dicts are empty.
    /// </summary>
    public static DesignOperation CreateInsert(int uid, string elementName)
        => new(
            DesignOperationType.Insert,
            uid,
            new Dictionary<string, string?>(),
            new Dictionary<string, string?>(),
            $"Insert {elementName}");

    /// <summary>Creates a Rotate operation capturing the angle before and after the drag.</summary>
    public static DesignOperation CreateRotate(
        int uid,
        Dictionary<string, string?> before,
        Dictionary<string, string?> after)
        => new(DesignOperationType.Rotate, uid, before, after, "Rotate");

    /// <summary>
    /// Creates a placeholder Delete operation.
    /// The actual XAML change is stored in a <c>SnapshotDesignUndoEntry</c>;
    /// Before/After dicts are empty.
    /// </summary>
    public static DesignOperation CreateDelete(int uid, string elementName)
        => new(
            DesignOperationType.Delete,
            uid,
            new Dictionary<string, string?>(),
            new Dictionary<string, string?>(),
            $"Delete {elementName}");
}

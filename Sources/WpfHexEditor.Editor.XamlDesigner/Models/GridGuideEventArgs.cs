// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: Models/GridGuideEventArgs.cs
// Author: Derek Tremblay
// Created: 2026-03-18
// Description:
//     Event argument types raised by GridGuideAdorner and forwarded through
//     DesignCanvas to XamlDesignerSplitHost for XAML patching + undo.
// ==========================================================

namespace WpfHexEditor.Editor.XamlDesigner.Models;

/// <summary>Raised when the user drags a boundary grip to resize a column or row.</summary>
public sealed class GridGuideResizedEventArgs : EventArgs
{
    public required bool   IsColumn    { get; init; }
    public required int    Index       { get; init; }
    public required string NewRawValue { get; init; }  // e.g. "150", "2.5*", "Auto"
}

/// <summary>Raised when the user clicks the "+" button to add a new column or row.</summary>
public sealed class GridGuideAddedEventArgs : EventArgs
{
    public required bool   IsColumn    { get; init; }
    public required int    InsertAfter { get; init; }  // -1 = prepend, Count-1 = append
    public required string Definition  { get; init; }  // default "*"
}

/// <summary>Raised when the user clicks the "×" button on a handle chip to remove a column or row.</summary>
public sealed class GridGuideRemovedEventArgs : EventArgs
{
    public required bool IsColumn { get; init; }
    public required int  Index    { get; init; }
}

/// <summary>Raised when the user selects a new size type via the handle chip dropdown.</summary>
public sealed class GridGuideTypeChangedEventArgs : EventArgs
{
    public required bool         IsColumn    { get; init; }
    public required int          Index       { get; init; }
    public required GridSizeType NewType     { get; init; }
    public required string       NewRawValue { get; init; }  // e.g. "*", "Auto", "100"
}

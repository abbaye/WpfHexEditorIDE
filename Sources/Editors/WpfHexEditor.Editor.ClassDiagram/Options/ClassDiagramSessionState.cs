// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Options/ClassDiagramSessionState.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-07
// Description:
//     Plain data model for the persisted Class Diagram session state.
//     Serialized to/from JSON by ClassDiagramSessionStateSerializer.
//     Stores enough context to restore the exact view the user had
//     when they last closed the diagram.
//
// Architecture Notes:
//     POCO — no WPF dependencies.  Loaded before the canvas is shown
//     so that zoom/pan can be set before first render.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Options;

/// <summary>
/// Persisted session state for a Class Diagram document.
/// </summary>
public sealed class ClassDiagramSessionState
{
    /// <summary>Absolute path of the last open C# source file.</summary>
    public string? LastFilePath { get; set; }

    /// <summary>
    /// Flat key-value view snapshot produced by <c>ClassDiagramSplitHost.GetViewSnapshot()</c>.
    /// Keys: zoom, offsetX, offsetY, selected, minimap.
    /// </summary>
    public Dictionary<string, string> ViewSnapshot { get; set; } = [];
}

// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Options/WhcdDocument.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-08
// Description:
//     Data model for the .whcd twin-file format.
//     A .whcd file is stored next to the source file (e.g. Foo.cs.whcd)
//     and captures the full visual state of a class diagram: node
//     positions, zoom/pan, minimap visibility, and selected node.
//
// Architecture Notes:
//     POCO — no WPF dependencies. Serialized to/from JSON by WhcdSerializer.
//     Indexed by node ID so it survives source edits that add/remove members
//     without moving nodes.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Options;

/// <summary>
/// Root model for a .whcd class-diagram twin file.
/// </summary>
public sealed class WhcdDocument
{
    public int          Version        { get; set; } = 1;
    /// <summary>Source file(s) this diagram was generated from.</summary>
    public List<string> SourceFiles    { get; set; } = [];
    public double       Zoom           { get; set; } = 1.0;
    public double       OffsetX        { get; set; }
    public double       OffsetY        { get; set; }
    public bool         MinimapVisible { get; set; } = true;
    public string       MinimapCorner  { get; set; } = "BottomLeft";
    public string?      SelectedNodeId { get; set; }
    /// <summary>Per-node canvas positions keyed by node ID.</summary>
    public List<WhcdNodePosition> Nodes { get; set; } = [];
}

/// <summary>Persisted canvas position for a single class-diagram node.</summary>
public sealed class WhcdNodePosition
{
    public string Id    { get; set; } = "";
    public double X     { get; set; }
    public double Y     { get; set; }
    public double Width { get; set; } = 180.0;
}

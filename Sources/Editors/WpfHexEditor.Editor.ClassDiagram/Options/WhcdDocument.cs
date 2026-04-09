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
//     positions, zoom/pan, minimap, selection, swimlanes, snap, view-mode,
//     collapsed sections, and custom node heights.
//
// Architecture Notes:
//     POCO — no WPF dependencies. Serialized to/from JSON by WhcdSerializer.
//     Indexed by node ID so it survives source edits that add/remove members
//     without moving nodes.
//     Version 2 adds: ShowSwimLanes, SnapToGrid, ViewMode, SplitLayout,
//     SplitColRatio, SplitRowRatio, CollapsedSections; WhcdNodePosition.Height.
//     Old Version=1 files deserialize safely: missing fields receive defaults.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Options;

/// <summary>
/// Root model for a .whcd class-diagram twin file.
/// </summary>
public sealed class WhcdDocument
{
    public int          Version        { get; set; } = 2;
    /// <summary>Source file(s) this diagram was generated from.</summary>
    public List<string> SourceFiles    { get; set; } = [];
    public double       Zoom           { get; set; } = 1.0;
    public double       OffsetX        { get; set; }
    public double       OffsetY        { get; set; }
    public bool         MinimapVisible { get; set; } = true;
    public string       MinimapCorner  { get; set; } = "BottomLeft";
    public string?      SelectedNodeId { get; set; }

    // ── View / UI state (Version 2) ──────────────────────────────────────────

    /// <summary>Whether the swimlane background layer is visible.</summary>
    public bool   ShowSwimLanes  { get; set; } = false;

    /// <summary>Whether snap-to-grid is active during node drag.</summary>
    public bool   SnapToGrid     { get; set; } = false;

    /// <summary>
    /// Active view mode. One of: <c>DslOnly</c>, <c>Split</c>, <c>DiagramOnly</c>.
    /// </summary>
    public string ViewMode       { get; set; } = "DiagramOnly";

    /// <summary>
    /// Active split layout. One of: <c>SplitRight</c>, <c>SplitLeft</c>,
    /// <c>SplitTop</c>, <c>SplitBottom</c>.
    /// </summary>
    public string SplitLayout    { get; set; } = "SplitRight";

    /// <summary>Code-column ratio [0.1–0.9] for horizontal splits.</summary>
    public double SplitColRatio  { get; set; } = 0.35;

    /// <summary>Code-row ratio [0.1–0.9] for vertical splits.</summary>
    public double SplitRowRatio  { get; set; } = 0.35;

    /// <summary>
    /// Flattened collapsed-section entries in the form <c>"nodeId:SectionName"</c>
    /// (e.g. <c>"MyClass:Fields"</c>). SectionName is one of:
    /// Fields, Properties, Methods, Events.
    /// </summary>
    public List<string> CollapsedSections { get; set; } = [];

    /// <summary>Per-node canvas positions keyed by node ID.</summary>
    public List<WhcdNodePosition> Nodes { get; set; } = [];
}

/// <summary>Persisted canvas position for a single class-diagram node.</summary>
public sealed class WhcdNodePosition
{
    public string Id     { get; set; } = "";
    public double X      { get; set; }
    public double Y      { get; set; }
    public double Width  { get; set; } = 180.0;
    /// <summary>
    /// Custom height in logical pixels set by the resize gripper.
    /// <c>0</c> means auto (no custom height override).
    /// </summary>
    public double Height { get; set; } = 0.0;
}

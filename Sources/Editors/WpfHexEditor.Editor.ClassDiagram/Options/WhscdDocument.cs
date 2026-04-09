// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Options/WhscdDocument.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-08
// Description:
//     Data model for the .whscd twin-file format (WpfHexEditor Solution
//     Class Diagram). A .whscd file is stored next to the solution file
//     (e.g. MySolution.whsln.whscd) and captures the full visual + filter
//     state of a solution-wide class diagram: node positions, zoom/pan,
//     minimap, project groups, project/namespace filters, swimlanes, snap,
//     view-mode, collapsed sections, and custom node heights.
//
// Architecture Notes:
//     POCO — no WPF dependencies. Serialized to/from JSON by WhscdSerializer.
//     Extends the .whcd concept for multi-project scope:
//       - ProjectGroups maps each project to a display color and collapsed state.
//       - ExcludedProjects / ExcludedNamespaces let users filter the diagram.
//       - Version 1 is the initial format; designed for future extension.
//     Reuses WhcdNodePosition from WhcdDocument (same position model).
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Options;

/// <summary>
/// Root model for a .whscd solution class-diagram twin file.
/// </summary>
public sealed class WhscdDocument
{
    public int    Version      { get; set; } = 1;
    /// <summary>Absolute path to the solution file (.whsln / .sln / .slnx) this diagram belongs to.</summary>
    public string SolutionPath { get; set; } = "";
    /// <summary>UTC timestamp when the diagram was generated.</summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    // ── Viewport ─────────────────────────────────────────────────────────────
    public double  Zoom           { get; set; } = 1.0;
    public double  OffsetX        { get; set; }
    public double  OffsetY        { get; set; }
    public bool    MinimapVisible { get; set; } = true;
    public string  MinimapCorner  { get; set; } = "BottomLeft";
    public string? SelectedNodeId { get; set; }

    // ── View / UI state ───────────────────────────────────────────────────────
    /// <summary>Whether the swimlane background layer is visible. Default ON for solution diagrams.</summary>
    public bool   ShowSwimLanes { get; set; } = true;
    /// <summary>Whether snap-to-grid is active during node drag.</summary>
    public bool   SnapToGrid    { get; set; } = false;
    /// <summary>Active view mode: <c>DslOnly</c>, <c>Split</c>, or <c>DiagramOnly</c>.</summary>
    public string ViewMode      { get; set; } = "DiagramOnly";
    /// <summary>Split direction when <see cref="ViewMode"/> is <c>Split</c>.</summary>
    public string SplitLayout   { get; set; } = "SplitRight";
    /// <summary>Code-column ratio [0.1–0.9] for horizontal splits.</summary>
    public double SplitColRatio { get; set; } = 0.35;
    /// <summary>Code-row ratio [0.1–0.9] for vertical splits.</summary>
    public double SplitRowRatio { get; set; } = 0.35;
    /// <summary>Collapsed member-section entries in the form <c>"nodeId:SectionName"</c>.</summary>
    public List<string> CollapsedSections { get; set; } = [];

    // ── Project grouping ──────────────────────────────────────────────────────
    /// <summary>Visual metadata for each project in the solution.</summary>
    public List<WhscdProjectGroup> ProjectGroups { get; set; } = [];

    // ── Filters ───────────────────────────────────────────────────────────────
    /// <summary>Project paths to hide from the diagram (matched case-insensitively).</summary>
    public List<string> ExcludedProjects   { get; set; } = [];
    /// <summary>Namespace prefixes to hide from the diagram (matched case-insensitively).</summary>
    public List<string> ExcludedNamespaces { get; set; } = [];
    /// <summary>Whether private members are shown in each node card.</summary>
    public bool ShowPrivateMembers { get; set; } = false;
    /// <summary>Whether internal (non-public) types are included in the diagram.</summary>
    public bool ShowInternalTypes  { get; set; } = true;

    // ── Node positions ────────────────────────────────────────────────────────
    /// <summary>Per-node canvas positions keyed by stable node ID.</summary>
    public List<WhcdNodePosition> Nodes { get; set; } = [];
}

/// <summary>
/// Visual metadata persisted for a single project within a solution diagram.
/// </summary>
public sealed class WhscdProjectGroup
{
    /// <summary>Display name of the project (matches <see cref="IProject.Name"/>).</summary>
    public string ProjectName { get; set; } = "";
    /// <summary>Absolute path to the project file (.whproj / .csproj).</summary>
    public string ProjectPath { get; set; } = "";
    /// <summary>Swimlane fill color as a CSS hex string (e.g. <c>#3A6EA5</c>).</summary>
    public string Color       { get; set; } = "#444444";
    /// <summary>Whether the project's swimlane is collapsed in the diagram.</summary>
    public bool   IsCollapsed { get; set; } = false;
    /// <summary>Whether the project is completely excluded from the diagram view.</summary>
    public bool   IsExcluded  { get; set; } = false;
}

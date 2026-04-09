// ==========================================================
// Project: WpfHexEditor.Plugins.ClassDiagram
// File: Options/ClassDiagramOptions.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Persisted settings for the Class Diagram plugin.
//     Controls layout defaults, snap behaviour, and C# source
//     analysis options used by RoslynClassDiagramAnalyzer.
//
// Architecture Notes:
//     Plain POCO — no WPF dependencies.
//     Serialised to/from JSON by the IDE options infrastructure.
//     All properties have safe defaults so first-run works
//     without a settings file.
// ==========================================================

using WpfHexEditor.Editor.ClassDiagram.Core.Layout;

namespace WpfHexEditor.Plugins.ClassDiagram.Options;

/// <summary>Controls which .cs files are analyzed when opening a class diagram from a single file.</summary>
public enum PartialClassScopeMode
{
    /// <summary>Automatically include all .cs files with the same base name (e.g. MainWindow.*.cs).</summary>
    AllSiblings,
    /// <summary>Analyze only the clicked file, ignoring other partial class files.</summary>
    ActiveFileOnly,
    /// <summary>Analyze all .cs files in the same directory.</summary>
    WholeDirectory,
    /// <summary>Show a prompt when more than one sibling file is found.</summary>
    AskWhenAmbiguous
}

/// <summary>
/// Persisted settings for the Class Diagram plugin.
/// </summary>
public sealed class ClassDiagramOptions
{
    // ── Layout ────────────────────────────────────────────────────────────────

    /// <summary>
    /// When true, newly generated diagrams have their nodes arranged
    /// automatically by the auto-layout engine before the canvas is shown.
    /// </summary>
    public bool AutoLayout { get; set; } = true;

    /// <summary>
    /// Layout algorithm to use when auto-layout is triggered.
    /// Default: <see cref="LayoutStrategyKind.Hierarchical"/>.
    /// </summary>
    public LayoutStrategyKind LayoutStrategy { get; set; } = LayoutStrategyKind.ForceDirected;

    /// <summary>
    /// Number of iterations for the ForceDirected layout simulation.
    /// Higher values produce better layout at the cost of more CPU time.
    /// Valid range: 100–2000.
    /// </summary>
    public int ForceDirectedIterations { get; set; } = 800;

    /// <summary>
    /// Spring rest length in logical pixels for the ForceDirected layout.
    /// Larger values spread nodes further apart.
    /// Valid range: 100–600.
    /// </summary>
    public double SpringLength { get; set; } = 320;

    /// <summary>
    /// Grid snap granularity in logical pixels used when dragging nodes on the canvas.
    /// Valid range: 1–128 px.
    /// </summary>
    public double SnapSize { get; set; } = 8.0;

    /// <summary>
    /// Default width in logical pixels for newly created class nodes.
    /// Overridden per-node once content is measured.
    /// </summary>
    public double DefaultNodeWidth { get; set; } = 180.0;

    /// <summary>
    /// Default height in logical pixels for newly created class nodes.
    /// Grows automatically with the member count after first layout.
    /// </summary>
    public double DefaultNodeHeight { get; set; } = 80.0;

    // ── Canvas defaults ───────────────────────────────────────────────────────

    /// <summary>
    /// When true the canvas dot-grid is visible when the diagram is first opened.
    /// The user can toggle it at any time via the toolbar.
    /// </summary>
    public bool ShowGridByDefault { get; set; } = true;

    /// <summary>
    /// When true snap-to-grid is active when the diagram is first opened.
    /// The user can toggle it at any time via the toolbar.
    /// </summary>
    public bool SnapEnabledByDefault { get; set; } = true;

    // ── Source analysis ───────────────────────────────────────────────────────

    /// <summary>
    /// When true the Roslyn analyzer includes private and protected members.
    /// When false only public and internal members are extracted.
    /// </summary>
    public bool IncludePrivateMembers { get; set; } = false;

    /// <summary>
    /// When true members inherited from base classes are included in each node.
    /// Currently a hint for future Roslyn-backed analysis; the regex analyser
    /// does not resolve inheritance chains.
    /// </summary>
    public bool IncludeInheritedMembers { get; set; } = false;

    /// <summary>
    /// When true nodes from the same namespace are placed in a named group lane
    /// during auto-layout.
    /// </summary>
    public bool GroupByNamespace { get; set; } = true;

    // ── Minimap ───────────────────────────────────────────────────────────────

    /// <summary>Whether the minimap is visible when the diagram is first opened.</summary>
    public bool ShowMinimapByDefault { get; set; } = true;

    // ── Outline panel ─────────────────────────────────────────────────────────

    /// <summary>When true the outline panel shows expandable member sub-items.</summary>
    public bool OutlinePanelShowMembers { get; set; } = true;

    /// <summary>When true member visibility is indicated by coloured ellipses.</summary>
    public bool OutlinePanelColorByVisibility { get; set; } = true;

    // ── Hover tooltips ────────────────────────────────────────────────────────

    /// <summary>When true hovering a class node shows a detail tooltip after a short delay.</summary>
    public bool ShowHoverTooltips { get; set; } = true;

    /// <summary>Delay in milliseconds before the hover tooltip appears. Valid range: 100–2000.</summary>
    public int TooltipDelayMs { get; set; } = 400;

    // ── Session restore ───────────────────────────────────────────────────────

    /// <summary>When true the last zoom, pan, selected node, and minimap position are restored on reopen.</summary>
    public bool RestoreLastState { get; set; } = true;

    // ── Partial class scope ───────────────────────────────────────────────────

    /// <summary>Controls partial class file discovery scope when opening a diagram from a single file.</summary>
    public PartialClassScopeMode PartialClassScope { get; set; } = PartialClassScopeMode.AllSiblings;

    // ── Solution generation ───────────────────────────────────────────────────

    /// <summary>
    /// When true the per-project swimlane layer is visible by default when a
    /// solution-wide class diagram is first opened.
    /// </summary>
    public bool SolutionShowSwimLanesByDefault { get; set; } = true;

    /// <summary>
    /// When true private members are shown in solution-wide class diagram nodes.
    /// When false only public, internal, and protected members are extracted.
    /// </summary>
    public bool SolutionIncludePrivateMembers { get; set; } = false;

    /// <summary>
    /// When true types with internal (non-public) accessibility are included
    /// in solution-wide class diagrams.
    /// </summary>
    public bool SolutionIncludeInternalTypes { get; set; } = true;

    /// <summary>
    /// When true projects whose name contains "Test", "Tests", or "Spec"
    /// (case-insensitive) are automatically excluded from the solution diagram.
    /// </summary>
    public bool SolutionExcludeTestProjects { get; set; } = false;

    /// <summary>
    /// Maximum number of source files in a solution before the generator shows
    /// a confirmation prompt. Set to 0 to disable the prompt.
    /// Valid range: 0–10000.
    /// </summary>
    public int SolutionMaxFilesPromptThreshold { get; set; } = 500;
}

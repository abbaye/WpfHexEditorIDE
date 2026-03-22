// ==========================================================
// Project: WpfHexEditor.Plugins.ClassDiagram
// File: Options/ClassDiagramOptions.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Persisted settings for the Class Diagram plugin.
//     Controls layout defaults, snap behaviour, and C# source
//     analysis options used by ClassDiagramSourceAnalyzer.
//
// Architecture Notes:
//     Plain POCO — no WPF dependencies.
//     Serialised to/from JSON by the IDE options infrastructure.
//     All properties have safe defaults so first-run works
//     without a settings file.
// ==========================================================

namespace WpfHexEditor.Plugins.ClassDiagram.Options;

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
    /// When true <see cref="ClassDiagramSourceAnalyzer"/> includes private and
    /// protected members when it parses C# source files.
    /// When false only public members are extracted.
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
}

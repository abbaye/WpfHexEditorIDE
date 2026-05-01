// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Controls/DiagramNavBar.cs
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-09
// Description:
//     VS-style navigation bar rendered above the diagram canvas.
//     Four cascading dropdowns: Project → Namespace → Class → Member.
//     Selection in any dropdown fires ZoomToNodeRequested / ScopeFilterChanged.
//     Driven externally by canvas SelectedClassChanged events.
//
// Architecture Notes:
//     Pure code-behind. No XAML. Theme tokens: CD_NavBar*.
//     _updating guard prevents re-entrant ComboBox SelectionChanged loops.
//     SetSelectedNode() reflects canvas selection into the dropdowns without
//     firing navigation events (guard is active during reflection).
// ==========================================================

using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Controls;

/// <summary>
/// Active scope filter driven by the nav bar Project / Namespace dropdowns.
/// </summary>
public sealed class ScopeFilter
{
    public static readonly ScopeFilter All = new();

    public string? ProjectName { get; init; }
    public string? Namespace   { get; init; }

    public bool IsAll => ProjectName is null && Namespace is null;
}

/// <summary>
/// VS-style navigation bar with four cascading ComboBoxes:
/// [All Projects ▾] · [All Namespaces ▾] · [— class ▾] · [— member ▾]
/// </summary>
public sealed class DiagramNavBar : Border
{
    // ── Children ─────────────────────────────────────────────────────────────
    private readonly ComboBox _cboProject;
    private readonly ComboBox _cboNamespace;
    private readonly ComboBox _cboClass;
    private readonly ComboBox _cboMember;
    private readonly TextBlock _statsLabel;

    // ── State ─────────────────────────────────────────────────────────────────
    private DiagramDocument?                      _doc;
    private IReadOnlyList<DiagramProjectGroup>    _groups = [];
    private bool                                  _updating;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when the user selects a class (or member's parent) — canvas should zoom to it.</summary>
    public event EventHandler<ClassNode?>? ZoomToNodeRequested;

    /// <summary>Fired when the user selects a member — canvas should zoom to node and highlight row.</summary>
    public event EventHandler<(ClassNode Node, ClassMember Member)>? ZoomToMemberRequested;

    /// <summary>Fired when Project or Namespace selection changes — host should filter the document.</summary>
    public event EventHandler<ScopeFilter>? ScopeFilterChanged;

    // ── Constructor ───────────────────────────────────────────────────────────

    public DiagramNavBar()
    {
        Height = 26;
        Padding = new Thickness(4, 2, 4, 2);
        this.SetResourceReference(BackgroundProperty,   "CD_NavBarBackground");
        this.SetResourceReference(BorderBrushProperty,  "CD_NavBarBorder");
        BorderThickness = new Thickness(0, 0, 0, 1);

        _cboProject   = MakeCombo(160);
        _cboNamespace = MakeCombo(200);
        _cboClass     = MakeCombo(200);
        _cboMember    = MakeCombo(220);

        _statsLabel = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontSize          = 11,
            Margin            = new Thickness(8, 0, 0, 0),
            Opacity           = 0.55
        };
        _statsLabel.SetResourceReference(TextBlock.ForegroundProperty, "CD_NavBarComboForeground");

        var sep1 = MakeSep();
        var sep2 = MakeSep();
        var sep3 = MakeSep();

        var panel = new StackPanel
        {
            Orientation       = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(_cboProject);
        panel.Children.Add(sep1);
        panel.Children.Add(_cboNamespace);
        panel.Children.Add(sep2);
        panel.Children.Add(_cboClass);
        panel.Children.Add(sep3);
        panel.Children.Add(_cboMember);
        panel.Children.Add(_statsLabel);

        Child = panel;

        _cboProject.SelectionChanged   += OnProjectChanged;
        _cboNamespace.SelectionChanged += OnNamespaceChanged;
        _cboClass.SelectionChanged     += OnClassChanged;
        _cboMember.SelectionChanged    += OnMemberChanged;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads a new document into the nav bar.
    /// Resets all dropdowns to "All / —".
    /// </summary>
    public void SetDocument(DiagramDocument doc, IReadOnlyList<DiagramProjectGroup> groups)
    {
        _doc    = doc;
        _groups = groups;
        _updating = true;
        try
        {
            RebuildProjects();
            RebuildNamespaces(null);
            RebuildClasses(null, null);
            RebuildMembers(null);
            UpdateStats();
        }
        finally { _updating = false; }
    }

    /// <summary>
    /// Reflects the canvas selection into the dropdowns without firing navigation events.
    /// Call from <c>ClassDiagramSplitHost.OnCanvasSelectedClassChanged</c>.
    /// </summary>
    public void SetSelectedNode(ClassNode? node)
    {
        if (_doc is null) return;
        _updating = true;
        try
        {
            if (node is null)
            {
                // Clear class + member dropdowns, leave project/ns intact
                if (_cboClass.Items.Count > 0)   _cboClass.SelectedIndex   = 0;
                if (_cboMember.Items.Count > 0)  _cboMember.SelectedIndex  = 0;
                return;
            }

            // Reflect project
            string? projectName = GetNodeProject(node);
            SetComboToText(_cboProject,   projectName ?? AllProjects);
            // Reflect namespace
            SetComboToText(_cboNamespace, string.IsNullOrEmpty(node.Namespace) ? AllNamespaces : node.Namespace);
            // Rebuild class list if needed and select
            RebuildClasses(projectName, string.IsNullOrEmpty(node.Namespace) ? null : node.Namespace);
            SetComboToText(_cboClass, node.Name);
            // Clear member
            RebuildMembers(node);
            if (_cboMember.Items.Count > 0) _cboMember.SelectedIndex = 0;
        }
        finally { _updating = false; }
    }

    // ── ComboBox handlers ─────────────────────────────────────────────────────

    private void OnProjectChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || _doc is null) return;
        string? proj = SelectedText(_cboProject) is AllProjects ? null : SelectedText(_cboProject);
        _updating = true;
        try
        {
            RebuildNamespaces(proj);
            RebuildClasses(proj, null);
            RebuildMembers(null);
        }
        finally { _updating = false; }
        ScopeFilterChanged?.Invoke(this, new ScopeFilter { ProjectName = proj, Namespace = null });
    }

    private void OnNamespaceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || _doc is null) return;
        string? ns   = SelectedText(_cboNamespace) is AllNamespaces ? null : SelectedText(_cboNamespace);
        string? proj = SelectedText(_cboProject)   is AllProjects   ? null : SelectedText(_cboProject);
        _updating = true;
        try
        {
            RebuildClasses(proj, ns);
            RebuildMembers(null);
        }
        finally { _updating = false; }
        ScopeFilterChanged?.Invoke(this, new ScopeFilter { ProjectName = proj, Namespace = ns });
    }

    private void OnClassChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || _doc is null) return;
        string? name = SelectedText(_cboClass);
        if (name is null or "—") return;
        var node = FindClassByName(name);
        if (node is null) return;
        _updating = true;
        try { RebuildMembers(node); }
        finally { _updating = false; }
        ZoomToNodeRequested?.Invoke(this, node);
    }

    private void OnMemberChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || _doc is null) return;
        if (_cboMember.SelectedItem is not NavMemberItem item) return;
        if (item.Member is null) return;  // "—" sentinel
        var node = FindClassByName(SelectedText(_cboClass) ?? string.Empty);
        if (node is null) return;
        // Fire only ZoomToMemberRequested — host zooms to the node as part of that handler.
        ZoomToMemberRequested?.Invoke(this, (node, item.Member));
    }

    // ── Rebuild helpers ───────────────────────────────────────────────────────

    private void RebuildProjects()
    {
        _cboProject.Items.Clear();
        _cboProject.Items.Add(AllProjects);
        foreach (var g in _groups.OrderBy(g => g.ProjectName))
            _cboProject.Items.Add(g.ProjectName);
        _cboProject.SelectedIndex = 0;
    }

    private void RebuildNamespaces(string? projectFilter)
    {
        _cboNamespace.Items.Clear();
        _cboNamespace.Items.Add(AllNamespaces);
        if (_doc is null) { _cboNamespace.SelectedIndex = 0; return; }
        var nodes = FilterByProject(_doc.Classes, projectFilter);
        foreach (var ns in nodes.Select(n => n.Namespace)
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct().OrderBy(s => s))
            _cboNamespace.Items.Add(ns);
        _cboNamespace.SelectedIndex = 0;
    }

    private void RebuildClasses(string? projectFilter, string? namespaceFilter)
    {
        _cboClass.Items.Clear();
        _cboClass.Items.Add("—");
        if (_doc is null) { _cboClass.SelectedIndex = 0; return; }
        var nodes = FilterByProject(_doc.Classes, projectFilter);
        if (!string.IsNullOrEmpty(namespaceFilter))
            nodes = nodes.Where(n => n.Namespace == namespaceFilter);
        foreach (var n in nodes.OrderBy(n => n.Name))
            _cboClass.Items.Add(n.Name);
        _cboClass.SelectedIndex = 0;
    }

    private void RebuildMembers(ClassNode? node)
    {
        _cboMember.Items.Clear();
        _cboMember.Items.Add(new NavMemberItem("—", null!));
        if (node is null) { _cboMember.SelectedIndex = 0; return; }
        foreach (var m in node.Members)
            _cboMember.Items.Add(new NavMemberItem(BuildMemberLabel(m), m));
        _cboMember.SelectedIndex = 0;
    }

    private void UpdateStats()
    {
        if (_doc is null) { _statsLabel.Text = string.Empty; return; }
        _statsLabel.Text = $"  {_doc.Classes.Count} types · {_doc.Relationships.Count} relationships";
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private const string AllProjects   = "All Projects";
    private const string AllNamespaces = "All Namespaces";

    private IEnumerable<ClassNode> FilterByProject(IReadOnlyList<ClassNode> nodes, string? project)
    {
        if (project is null) return nodes;
        var ids = _groups.FirstOrDefault(g => g.ProjectName == project)?.ClassIds;
        if (ids is null) return nodes;
        var set = new HashSet<string>(ids, StringComparer.Ordinal);
        return nodes.Where(n => set.Contains(n.Id));
    }

    private string? GetNodeProject(ClassNode node)
    {
        foreach (var g in _groups)
            if (g.ClassIds.Contains(node.Id))
                return g.ProjectName;
        return null;
    }

    private ClassNode? FindClassByName(string name)
        => _doc?.Classes.FirstOrDefault(n => n.Name == name);

    private static string? SelectedText(ComboBox cbo)
        => cbo.SelectedItem as string ?? (cbo.SelectedItem as NavMemberItem)?.Label;

    private static void SetComboToText(ComboBox cbo, string? text)
    {
        if (text is null) { if (cbo.Items.Count > 0) cbo.SelectedIndex = 0; return; }
        for (int i = 0; i < cbo.Items.Count; i++)
        {
            string? item = cbo.Items[i] as string ?? (cbo.Items[i] as NavMemberItem)?.Label;
            if (item == text) { cbo.SelectedIndex = i; return; }
        }
        if (cbo.Items.Count > 0) cbo.SelectedIndex = 0;
    }

    private static string BuildMemberLabel(ClassMember m)
    {
        string vis = m.Visibility switch
        {
            MemberVisibility.Private   => "-",
            MemberVisibility.Protected => "#",
            MemberVisibility.Internal  => "~",
            _                          => "+"
        };
        return $"{vis} {m.Name} : {m.TypeName}";
    }

    // ── Factory helpers ───────────────────────────────────────────────────────

    private ComboBox MakeCombo(double width)
    {
        var cb = new ComboBox
        {
            Width             = width,
            Height            = 20,
            FontSize          = 11,
            Margin            = new Thickness(0, 0, 2, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            IsEditable        = false
        };
        cb.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "CD_NavBarComboForeground");
        cb.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "CD_NavBarComboBackground");
        return cb;
    }

    private static TextBlock MakeSep()
        => new TextBlock
        {
            Text              = "·",
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(3, 0, 3, 0),
            Opacity           = 0.4,
            FontSize          = 11
        };

    // ── Inner type ────────────────────────────────────────────────────────────

    private sealed record NavMemberItem(string Label, ClassMember? Member)
    {
        public override string ToString() => Label;
    }
}

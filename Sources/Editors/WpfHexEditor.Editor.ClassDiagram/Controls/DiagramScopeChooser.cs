// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Controls/DiagramScopeChooser.cs
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-09
// Description:
//     Centered overlay panel shown when a large solution diagram is opened.
//     Lets the user choose which scope to display before the full render
//     begins: All types / Single Project / Namespace / Custom selection.
//     Confirms via "Open with scope →"; cancels back to caller.
//
// Architecture Notes:
//     Rendered as an overlay Border on top of the diagram host Grid.
//     ClassDiagramSplitHost adds it at Panel.ZIndex=300, removes it on confirm/cancel.
//     ScopeChosen event carries a ScopeChoiceResult that the host uses to
//     filter the DiagramDocument before calling ApplyDocumentAsync.
// ==========================================================

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Controls;

// ── Result types ─────────────────────────────────────────────────────────────

public enum ScopeMode { All, Project, Namespace, Selection }

public sealed class ScopeChoiceResult
{
    public ScopeMode                  Mode             { get; init; }
    public string?                    ProjectName      { get; init; }
    public string?                    Namespace        { get; init; }
    public IReadOnlyList<string>      SelectedProjects { get; init; } = [];
}

// ── Control ───────────────────────────────────────────────────────────────────

/// <summary>
/// Full-screen translucent overlay that helps the user choose a scope
/// before loading a large class diagram.
/// </summary>
public sealed class DiagramScopeChooser : Grid
{
    // ── Events ────────────────────────────────────────────────────────────────
    public event EventHandler<ScopeChoiceResult>? ScopeChosen;
    public event EventHandler?                    Cancelled;

    // ── State ─────────────────────────────────────────────────────────────────
    private DiagramDocument?                   _doc;
    private IReadOnlyList<DiagramProjectGroup> _groups = [];

    // ── Controls ──────────────────────────────────────────────────────────────
    private RadioButton _rbAll       = null!;
    private RadioButton _rbProject   = null!;
    private RadioButton _rbNamespace = null!;
    private RadioButton _rbSelection = null!;
    private ComboBox    _cboProject  = null!;
    private ComboBox    _cboNs       = null!;
    private ListBox     _lstProjects = null!;
    private TextBlock   _tbStats     = null!;

    // ── Constructor ───────────────────────────────────────────────────────────

    public DiagramScopeChooser()
    {
        // Dim overlay
        Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0));
        Panel.SetZIndex(this, 300);

        var card = BuildCard();
        // Center the card
        var center = new Border
        {
            Child               = card,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center
        };
        Children.Add(center);

        // Clicking outside the card = cancel
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource == this || e.OriginalSource is Grid g && g == this)
                Cancel();
        };
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetDocument(DiagramDocument doc, IReadOnlyList<DiagramProjectGroup> groups)
    {
        _doc    = doc;
        _groups = groups;

        // Stats
        _tbStats.Text = $"{doc.Classes.Count} types  ·  {doc.Relationships.Count} relationships  ·  {groups.Count} projects";

        // Project combo
        _cboProject.Items.Clear();
        foreach (var g in groups.OrderBy(g => g.ProjectName))
            _cboProject.Items.Add(g.ProjectName);
        if (_cboProject.Items.Count > 0) _cboProject.SelectedIndex = 0;

        // Namespace combo
        _cboNs.Items.Clear();
        foreach (var ns in doc.Classes
            .Select(n => n.Namespace)
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct().OrderBy(s => s))
            _cboNs.Items.Add(ns);
        if (_cboNs.Items.Count > 0) _cboNs.SelectedIndex = 0;

        // Project list (for custom selection)
        _lstProjects.Items.Clear();
        foreach (var g in groups.OrderBy(g => g.ProjectName))
        {
            int count = g.ClassIds.Count;
            bool isBig = count > 100;
            var item = new ProjectCheckItem
            {
                ProjectName = g.ProjectName,
                TypeCount   = count,
                IsChecked   = true,
                IsBig       = isBig
            };
            _lstProjects.Items.Add(item);
        }
    }

    // ── Card builder ──────────────────────────────────────────────────────────

    private FrameworkElement BuildCard()
    {
        var card = new Border
        {
            Width           = 520,
            CornerRadius    = new CornerRadius(6),
            Padding         = new Thickness(24, 20, 24, 20),
            BorderThickness = new Thickness(1)
        };
        card.SetResourceReference(Border.BackgroundProperty,   "CD_QuickJumpBackground");
        card.SetResourceReference(Border.BorderBrushProperty,  "CD_NavBarBorder");

        var stack = new StackPanel { Orientation = Orientation.Vertical };

        // Title row
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        var titleIcon = new TextBlock { Text = "🗂", FontSize = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        var titleText = new TextBlock { FontSize = 14, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        titleText.SetResourceReference(TextBlock.ForegroundProperty, "CD_NavBarComboForeground");
        titleText.Text = "Open Class Diagram";
        titleRow.Children.Add(titleIcon);
        titleRow.Children.Add(titleText);
        stack.Children.Add(titleRow);

        // Stats
        _tbStats = new TextBlock { FontSize = 11, Margin = new Thickness(0, 0, 0, 14), Opacity = 0.6 };
        _tbStats.SetResourceReference(TextBlock.ForegroundProperty, "CD_NavBarComboForeground");
        stack.Children.Add(_tbStats);

        // Separator
        stack.Children.Add(MakeSep());

        // Scope options
        stack.Children.Add(MakeLabel("Scope:", 10));
        _rbAll       = MakeRadio("All types (render everything)",   "scope");
        _rbProject   = MakeRadio("Single project",                  "scope");
        _rbNamespace = MakeRadio("Single namespace",                "scope");
        _rbSelection = MakeRadio("Custom selection (check list)",   "scope");
        _rbAll.IsChecked = true;

        stack.Children.Add(_rbAll);

        // Project radio + combo
        var projRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        _cboProject = new ComboBox { Width = 300, Height = 22, FontSize = 11, Margin = new Thickness(8, 0, 0, 0), IsEnabled = false };
        _cboProject.SetResourceReference(Control.ForegroundProperty, "CD_NavBarComboForeground");
        _cboProject.SetResourceReference(Control.BackgroundProperty, "CD_NavBarComboBackground");
        _rbProject.Checked   += (_, _) => _cboProject.IsEnabled = true;
        _rbProject.Unchecked += (_, _) => _cboProject.IsEnabled = false;
        projRow.Children.Add(_rbProject);
        projRow.Children.Add(_cboProject);
        stack.Children.Add(projRow);

        // Namespace radio + combo
        var nsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        _cboNs = new ComboBox { Width = 300, Height = 22, FontSize = 11, Margin = new Thickness(8, 0, 0, 0), IsEnabled = false };
        _cboNs.SetResourceReference(Control.ForegroundProperty, "CD_NavBarComboForeground");
        _cboNs.SetResourceReference(Control.BackgroundProperty, "CD_NavBarComboBackground");
        _rbNamespace.Checked   += (_, _) => _cboNs.IsEnabled = true;
        _rbNamespace.Unchecked += (_, _) => _cboNs.IsEnabled = false;
        nsRow.Children.Add(_rbNamespace);
        nsRow.Children.Add(_cboNs);
        stack.Children.Add(nsRow);

        stack.Children.Add(_rbSelection);

        // Project list (custom selection)
        _lstProjects = new ListBox
        {
            Height          = 140,
            Margin          = new Thickness(20, 4, 0, 8),
            IsEnabled       = false,
            SelectionMode   = SelectionMode.Multiple
        };
        _lstProjects.SetResourceReference(BackgroundProperty, "CD_NavBarComboBackground");
        _lstProjects.ItemTemplate = BuildProjectItemTemplate();
        _rbSelection.Checked   += (_, _) => _lstProjects.IsEnabled = true;
        _rbSelection.Unchecked += (_, _) => _lstProjects.IsEnabled = false;
        stack.Children.Add(_lstProjects);

        stack.Children.Add(MakeSep());

        // Buttons
        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        var btnCancel  = MakeButton("Cancel",            false);
        var btnConfirm = MakeButton("Open with scope →", true);
        btnCancel.Click  += (_, _) => Cancel();
        btnConfirm.Click += (_, _) => Confirm();
        btnRow.Children.Add(btnCancel);
        btnRow.Children.Add(btnConfirm);
        stack.Children.Add(btnRow);

        card.Child = stack;
        return card;
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private void Confirm()
    {
        ScopeChoiceResult result;
        if (_rbProject.IsChecked == true)
        {
            result = new ScopeChoiceResult
            {
                Mode        = ScopeMode.Project,
                ProjectName = _cboProject.SelectedItem as string
            };
        }
        else if (_rbNamespace.IsChecked == true)
        {
            result = new ScopeChoiceResult
            {
                Mode      = ScopeMode.Namespace,
                Namespace = _cboNs.SelectedItem as string
            };
        }
        else if (_rbSelection.IsChecked == true)
        {
            var selected = _lstProjects.Items
                .OfType<ProjectCheckItem>()
                .Where(i => i.IsChecked)
                .Select(i => i.ProjectName)
                .ToList();
            result = new ScopeChoiceResult
            {
                Mode             = ScopeMode.Selection,
                SelectedProjects = selected
            };
        }
        else
        {
            result = new ScopeChoiceResult { Mode = ScopeMode.All };
        }
        ScopeChosen?.Invoke(this, result);
    }

    private void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

    // ── Factory helpers ───────────────────────────────────────────────────────

    private RadioButton MakeRadio(string text, string group)
    {
        var rb = new RadioButton
        {
            Content     = text,
            GroupName   = group,
            FontSize    = 12,
            Margin      = new Thickness(0, 2, 0, 2)
        };
        rb.SetResourceReference(Control.ForegroundProperty, "CD_NavBarComboForeground");
        return rb;
    }

    private Button MakeButton(string text, bool isPrimary)
    {
        var btn = new Button
        {
            Content     = text,
            Padding     = new Thickness(16, 6, 16, 6),
            FontSize    = 12,
            Margin      = new Thickness(8, 0, 0, 0),
            Cursor      = Cursors.Hand
        };
        if (isPrimary)
            btn.SetResourceReference(BackgroundProperty, "CD_ClassBoxSelectedBorderBrush");
        return btn;
    }

    private static TextBlock MakeLabel(string text, double marginBottom)
    {
        var tb = new TextBlock { Text = text, FontSize = 11, Margin = new Thickness(0, 0, 0, marginBottom), FontWeight = FontWeights.SemiBold };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "CD_NavBarComboForeground");
        return tb;
    }

    private static Border MakeSep()
    {
        var sep = new Border { Height = 1, Margin = new Thickness(0, 8, 0, 8) };
        sep.SetResourceReference(BackgroundProperty, "CD_NavBarBorder");
        return sep;
    }

    private static DataTemplate BuildProjectItemTemplate()
    {
        var template = new DataTemplate(typeof(ProjectCheckItem));
        var factory  = new FrameworkElementFactory(typeof(StackPanel));
        factory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

        var cbFactory = new FrameworkElementFactory(typeof(CheckBox));
        cbFactory.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding("IsChecked") { Mode = System.Windows.Data.BindingMode.TwoWay });
        cbFactory.SetValue(CheckBox.MarginProperty, new Thickness(0, 0, 6, 0));
        cbFactory.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);

        var tbFactory = new FrameworkElementFactory(typeof(TextBlock));
        tbFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("DisplayText"));
        tbFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
        tbFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

        factory.AppendChild(cbFactory);
        factory.AppendChild(tbFactory);
        template.VisualTree = factory;
        return template;
    }

    // ── Inner types ───────────────────────────────────────────────────────────

    private sealed class ProjectCheckItem : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isChecked;

        public string ProjectName { get; set; } = string.Empty;
        public int    TypeCount   { get; set; }
        public bool   IsBig       { get; set; }

        public bool IsChecked
        {
            get => _isChecked;
            set { _isChecked = value; PropertyChanged?.Invoke(this, new(nameof(IsChecked))); }
        }

        public string DisplayText =>
            IsBig ? $"{ProjectName}  ({TypeCount} types) ⚠"
                  : $"{ProjectName}  ({TypeCount} types)";

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}

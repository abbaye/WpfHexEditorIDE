// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: XamlOutlineNode.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     ViewModel for a single node in the XAML Outline tree.
//     Wraps an XElement and exposes display-ready properties.
//
// Architecture Notes:
//     INPC — IsSelected / IsExpanded support two-way TreeView binding.
//     ElementPath uses a simple slash-delimited path for persistence.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace WpfHexEditor.Editor.XamlDesigner.ViewModels;

/// <summary>
/// View model wrapping a single XElement for display in the XAML Outline tree.
/// </summary>
public sealed class XamlOutlineNode : INotifyPropertyChanged
{
    private bool   _isSelected;
    private bool   _isExpanded;
    private bool   _isMatch    = true;   // true = visible / not dimmed by search filter
    private bool   _isEditing;
    private string _editLabel  = string.Empty;

    // ── Constructor ───────────────────────────────────────────────────────────

    public XamlOutlineNode(XElement element, string parentPath = "")
    {
        SourceElement = element;
        TagName       = element.Name.LocalName;

        XKey  = element.Attribute(XName.Get("Key",  "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value;
        XName_ = element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value
                ?? element.Attribute("Name")?.Value;

        // Build display label and semantic icon.
        DisplayLabel = BuildDisplayLabel();
        ElementIcon  = ResolveElementIcon(TagName);

        // Build path for persistence.
        ElementPath = string.IsNullOrEmpty(parentPath)
            ? TagName
            : $"{parentPath}/{TagName}";

        // Build children recursively.
        int childIndex = 0;
        foreach (var child in element.Elements())
        {
            var childPath = $"{ElementPath}[{childIndex}]";
            var childNode = new XamlOutlineNode(child, childPath) { Parent = this };
            Children.Add(childNode);
            childIndex++;
        }
    }

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>Local name of the XML element (e.g. "Grid", "Button").</summary>
    public string TagName { get; }

    /// <summary>x:Key attribute value if present; null otherwise.</summary>
    public string? XKey { get; }

    /// <summary>x:Name or Name attribute value if present; null otherwise.</summary>
    public string? XName_ { get; }

    /// <summary>Human-readable label shown in the outline tree.</summary>
    public string DisplayLabel { get; }

    /// <summary>Slash-delimited path used for persistence and cross-panel sync.</summary>
    public string ElementPath { get; }

    /// <summary>The backing XElement from the parsed document.</summary>
    public XElement SourceElement { get; }

    /// <summary>Child nodes, built lazily on construction.</summary>
    public ObservableCollection<XamlOutlineNode> Children { get; } = new();

    /// <summary>Parent node in the outline tree; null for the root node.</summary>
    public XamlOutlineNode? Parent { get; internal set; }

    /// <summary>Whether this node is selected in the tree view.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
    }

    /// <summary>Whether this node is expanded in the tree view.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set { if (_isExpanded == value) return; _isExpanded = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// True when the node matches the current search filter (or no filter is active).
    /// False dims the node in the tree without hiding it.
    /// </summary>
    public bool IsMatch
    {
        get => _isMatch;
        set { if (_isMatch == value) return; _isMatch = value; OnPropertyChanged(); OnPropertyChanged(nameof(DimOpacity)); }
    }

    /// <summary>Opacity applied to dimmed (non-matching) nodes during search — 0.3 when dimmed, 1.0 otherwise.</summary>
    public double DimOpacity => _isMatch ? 1.0 : 0.30;

    /// <summary>True when the node is being inline-renamed by the user.</summary>
    public bool IsEditing
    {
        get => _isEditing;
        set { if (_isEditing == value) return; _isEditing = value; OnPropertyChanged(); }
    }

    /// <summary>Temporary edit text during inline rename; committed via CommitRename().</summary>
    public string EditLabel
    {
        get => _editLabel;
        set { _editLabel = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Segoe MDL2 Assets glyph representing the element type (e.g. Grid, Button, TextBlock).
    /// Computed once at construction from TagName.
    /// </summary>
    public string ElementIcon { get; }

    /// <summary>True when this element has a parse error or an invalid reference (e.g. missing x:Name target).</summary>
    public bool HasError { get; set; }

    /// <summary>Error message to display in a tooltip when HasError is true.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Begins inline renaming of the x:Name for this node.
    /// Sets IsEditing = true and seeds EditLabel with the current x:Name or TagName.
    /// </summary>
    public void BeginRename()
    {
        EditLabel = XName_ ?? TagName;
        IsEditing = true;
    }

    /// <summary>
    /// Commits the pending inline rename and exits edit mode.
    /// Returns the new name, or null if the name was unchanged or invalid.
    /// </summary>
    public string? CommitRename()
    {
        IsEditing = false;
        var newName = EditLabel.Trim();
        return string.IsNullOrEmpty(newName) || newName == (XName_ ?? TagName)
            ? null
            : newName;
    }

    // ── INPC ──────────────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ── Private helpers ───────────────────────────────────────────────────────

    private string BuildDisplayLabel()
    {
        var label = TagName;

        if (!string.IsNullOrEmpty(XName_))
            label += $" [{XName_}]";
        else if (!string.IsNullOrEmpty(XKey))
            label += $" [{XKey}]";

        return label;
    }

    /// <summary>
    /// Maps common WPF element names to Segoe MDL2 Assets glyphs.
    /// Falls back to a generic element glyph for unknown types.
    /// </summary>
    private static string ResolveElementIcon(string tagName) => tagName switch
    {
        "Grid"               => "\uE80A",
        "StackPanel"         => "\uE8FD",
        "DockPanel"          => "\uE8FD",
        "WrapPanel"          => "\uE8FD",
        "Canvas"             => "\uE771",
        "Border"             => "\uE81E",
        "Viewbox"            => "\uE8B9",
        "ScrollViewer"       => "\uE8CB",
        "TabControl"         => "\uE8A5",
        "TabItem"            => "\uE8A5",
        "Button"             => "\uE815",
        "ToggleButton"       => "\uE815",
        "CheckBox"           => "\uE739",
        "RadioButton"        => "\uE739",
        "ComboBox"           => "\uEDC5",
        "ListBox"            => "\uE8A5",
        "ListView"           => "\uE8A5",
        "TreeView"           => "\uE8A5",
        "DataGrid"           => "\uE8A5",
        "TextBox"            => "\uE8D2",
        "TextBlock"          => "\uE8D2",
        "RichTextBox"        => "\uE8D2",
        "Label"              => "\uE8D2",
        "PasswordBox"        => "\uE72E",
        "Slider"             => "\uE790",
        "ProgressBar"        => "\uE9D9",
        "Image"              => "\uEB9F",
        "MediaElement"       => "\uE8B2",
        "Rectangle"          => "\uE81E",
        "Ellipse"            => "\uE91F",
        "Line"               => "\uE745",
        "Path"               => "\uE745",
        "Polygon"            => "\uE745",
        "Polyline"           => "\uE745",
        "Expander"           => "\uE8B4",
        "GroupBox"           => "\uE810",
        "UserControl"        => "\uE9E9",
        "Window"             => "\uE737",
        "Page"               => "\uE7C3",
        "NavigationWindow"   => "\uE737",
        "Frame"              => "\uE737",
        "Menu"               => "\uE700",
        "MenuItem"           => "\uE700",
        "ContextMenu"        => "\uE700",
        "ToolBar"            => "\uE700",
        "StatusBar"          => "\uE700",
        "Separator"          => "\uE745",
        "Popup"              => "\uE8A4",
        "ToolTip"            => "\uE946",
        _                    => "\uE9E9"
    };
}

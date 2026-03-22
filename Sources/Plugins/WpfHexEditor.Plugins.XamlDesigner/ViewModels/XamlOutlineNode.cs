// ==========================================================
// Project: WpfHexEditor.Plugins.XamlDesigner
// File: XamlOutlineNode.cs
// Author: Derek Tremblay
// Created: 2026-03-16
//          2026-03-22 — Moved to plugin project (WpfHexEditor.Plugins.XamlDesigner.ViewModels).
// Description:
//     ViewModel for a single node in the XAML Outline tree.
//     Wraps an XElement and exposes display-ready properties.
//
// Architecture: Plugin-owned. INPC — IsSelected / IsExpanded support two-way TreeView binding.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace WpfHexEditor.Plugins.XamlDesigner.ViewModels;

/// <summary>
/// View model wrapping a single XElement for display in the XAML Outline tree.
/// </summary>
public sealed class XamlOutlineNode : INotifyPropertyChanged
{
    private bool   _isSelected;
    private bool   _isExpanded;
    private bool   _isMatch    = true;
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

        DisplayLabel = BuildDisplayLabel();
        ElementIcon  = ResolveElementIcon(TagName);

        ElementPath = string.IsNullOrEmpty(parentPath)
            ? TagName
            : $"{parentPath}/{TagName}";

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

    public string TagName { get; }
    public string? XKey { get; }
    public string? XName_ { get; }
    public string DisplayLabel { get; }
    public string ElementPath { get; }
    public XElement SourceElement { get; }
    public ObservableCollection<XamlOutlineNode> Children { get; } = new();
    public XamlOutlineNode? Parent { get; internal set; }

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set { if (_isExpanded == value) return; _isExpanded = value; OnPropertyChanged(); }
    }

    public bool IsMatch
    {
        get => _isMatch;
        set { if (_isMatch == value) return; _isMatch = value; OnPropertyChanged(); OnPropertyChanged(nameof(DimOpacity)); }
    }

    public double DimOpacity => _isMatch ? 1.0 : 0.30;

    public bool IsEditing
    {
        get => _isEditing;
        set { if (_isEditing == value) return; _isEditing = value; OnPropertyChanged(); }
    }

    public string EditLabel
    {
        get => _editLabel;
        set { _editLabel = value; OnPropertyChanged(); }
    }

    public string ElementIcon { get; }

    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }

    public void BeginRename()
    {
        EditLabel = XName_ ?? TagName;
        IsEditing = true;
    }

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

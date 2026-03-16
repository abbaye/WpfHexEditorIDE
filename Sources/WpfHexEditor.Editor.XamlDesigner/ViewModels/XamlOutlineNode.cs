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
    private bool _isSelected;
    private bool _isExpanded;

    // ── Constructor ───────────────────────────────────────────────────────────

    public XamlOutlineNode(XElement element, string parentPath = "")
    {
        SourceElement = element;
        TagName       = element.Name.LocalName;

        XKey  = element.Attribute(XName.Get("Key",  "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value;
        XName_ = element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value
                ?? element.Attribute("Name")?.Value;

        // Build display label.
        DisplayLabel = BuildDisplayLabel();

        // Build path for persistence.
        ElementPath = string.IsNullOrEmpty(parentPath)
            ? TagName
            : $"{parentPath}/{TagName}";

        // Build children recursively.
        int childIndex = 0;
        foreach (var child in element.Elements())
        {
            var childPath = $"{ElementPath}[{childIndex}]";
            Children.Add(new XamlOutlineNode(child, childPath));
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
}

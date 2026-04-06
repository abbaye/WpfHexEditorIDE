// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: ViewModels/ClassPropertiesPanelViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     ViewModel for the Class Properties panel (F4-like).
//     Exposes the currently selected class node or member for
//     property editing. TypeInfo provides a header display string.
//
// Architecture Notes:
//     Pattern: ViewModel (MVVM).
//     SelectedObject is typed as object to accept either ClassNode
//     or ClassMember without coupling to WPF DataTemplateSelector.
//     TypeInfo is derived from the SelectedObject runtime type.
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Editor.ClassDiagram.ViewModels;

/// <summary>
/// ViewModel for the diagram properties panel (F4).
/// </summary>
public sealed class ClassPropertiesPanelViewModel : ViewModelBase
{
    private object? _selectedObject;
    private string _typeInfo = "No selection";

    // Currently inspected node and member
    private ClassNode? _selectedNode;
    private ClassMember? _selectedMember;

    // ---------------------------------------------------------------------------
    // Properties
    // ---------------------------------------------------------------------------

    /// <summary>
    /// The object currently being inspected â€” either a <see cref="ClassNode"/>
    /// or a <see cref="ClassMember"/>.
    /// </summary>
    public object? SelectedObject
    {
        get => _selectedObject;
        private set { if (_selectedObject == value) return; _selectedObject = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Human-readable header shown above the properties grid.
    /// E.g. "Class: MyClass" or "Method: Execute : void"
    /// </summary>
    public string TypeInfo
    {
        get => _typeInfo;
        private set { if (_typeInfo == value) return; _typeInfo = value; OnPropertyChanged(); }
    }

    /// <summary>The selected class node (null if a member or nothing is selected).</summary>
    public ClassNode? SelectedNode => _selectedNode;

    /// <summary>The selected member (null if a node or nothing is selected).</summary>
    public ClassMember? SelectedMember => _selectedMember;

    // ---------------------------------------------------------------------------
    // Convenience editable properties for the node
    // ---------------------------------------------------------------------------

    public string NodeName
    {
        get => _selectedNode?.Name ?? string.Empty;
    }

    public ClassKind NodeKind
    {
        get => _selectedNode?.Kind ?? ClassKind.Class;
        set
        {
            if (_selectedNode is null || _selectedNode.Kind == value) return;
            _selectedNode.Kind = value;
            OnPropertyChanged();
            RefreshTypeInfo();
        }
    }

    public bool NodeIsAbstract
    {
        get => _selectedNode?.IsAbstract ?? false;
        set
        {
            if (_selectedNode is null || _selectedNode.IsAbstract == value) return;
            _selectedNode.IsAbstract = value;
            OnPropertyChanged();
        }
    }

    // ---------------------------------------------------------------------------
    // API
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Updates the selection to the given node and optional member.
    /// Pass null for both to clear the selection.
    /// </summary>
    public void SetSelection(ClassNode? node, ClassMember? member = null)
    {
        _selectedNode = node;
        _selectedMember = member;

        SelectedObject = member is not null ? (object)member : node;
        RefreshTypeInfo();

        OnPropertyChanged(nameof(SelectedNode));
        OnPropertyChanged(nameof(SelectedMember));
        OnPropertyChanged(nameof(NodeName));
        OnPropertyChanged(nameof(NodeKind));
        OnPropertyChanged(nameof(NodeIsAbstract));
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    private void RefreshTypeInfo()
    {
        if (_selectedMember is not null && _selectedNode is not null)
        {
            TypeInfo = $"{_selectedMember.Kind}: {_selectedMember.DisplayLabel} (in {_selectedNode.Name})";
        }
        else if (_selectedNode is not null)
        {
            string kindLabel = _selectedNode.Kind switch
            {
                ClassKind.Interface => "Interface",
                ClassKind.Enum      => "Enum",
                ClassKind.Struct    => "Struct",
                ClassKind.Abstract  => "Abstract Class",
                _                   => "Class"
            };
            TypeInfo = $"{kindLabel}: {_selectedNode.Name} ({_selectedNode.Members.Count} members)";
        }
        else
        {
            TypeInfo = "No selection";
        }
    }

    // ---------------------------------------------------------------------------
    // INotifyPropertyChanged
    // ---------------------------------------------------------------------------


}

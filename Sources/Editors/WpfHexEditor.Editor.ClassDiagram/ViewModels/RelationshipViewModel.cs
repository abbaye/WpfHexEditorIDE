// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: ViewModels/RelationshipViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Observable wrapper around ClassRelationship for WPF data binding.
//     Exposes all relationship properties and selection state.
//
// Architecture Notes:
//     Pattern: ViewModel wrapper (Adapter).
//     ClassRelationship is a record; Kind and Label updates
//     create new records via 'with'. The parent document list
//     is not directly mutated here â€” the host replaces the entry.
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Editor.ClassDiagram.ViewModels;

/// <summary>
/// Observable wrapper around a <see cref="ClassRelationship"/> record.
/// </summary>
public sealed class RelationshipViewModel : ViewModelBase
{
    private ClassRelationship _relationship;
    private bool _isSelected;

    public RelationshipViewModel(ClassRelationship relationship)
    {
        _relationship = relationship ?? throw new ArgumentNullException(nameof(relationship));
    }

    /// <summary>Underlying domain relationship record.</summary>
    public ClassRelationship Relationship => _relationship;

    // ---------------------------------------------------------------------------
    // Mirrored properties
    // ---------------------------------------------------------------------------

    public string SourceId => _relationship.SourceId;
    public string TargetId => _relationship.TargetId;

    public RelationshipKind Kind
    {
        get => _relationship.Kind;
        set
        {
            if (_relationship.Kind == value) return;
            _relationship = _relationship with { Kind = value };
            OnPropertyChanged();
            OnPropertyChanged(nameof(KindLabel));
            OnPropertyChanged(nameof(ArrowDescription));
        }
    }

    public string? Label
    {
        get => _relationship.Label;
        set
        {
            if (_relationship.Label == value) return;
            _relationship = _relationship with { Label = value };
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayText));
        }
    }

    /// <summary>Human-readable kind label (e.g. "Inheritance").</summary>
    public string KindLabel => _relationship.Kind.ToString();

    /// <summary>Arrow notation used in DSL/display.</summary>
    public string ArrowDescription => _relationship.Kind switch
    {
        RelationshipKind.Inheritance => "<|--",
        RelationshipKind.Association => "-->",
        RelationshipKind.Dependency  => "..>",
        RelationshipKind.Aggregation => "o--",
        RelationshipKind.Composition => "*--",
        _                            => "-->"
    };

    /// <summary>One-line summary for the relationships panel list.</summary>
    public string DisplayText =>
        string.IsNullOrEmpty(_relationship.Label)
            ? $"{SourceId} {ArrowDescription} {TargetId}"
            : $"{SourceId} {ArrowDescription} {TargetId} : {_relationship.Label}";

    // ---------------------------------------------------------------------------
    // Interaction state
    // ---------------------------------------------------------------------------

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
    }

    // ---------------------------------------------------------------------------
    // INotifyPropertyChanged
    // ---------------------------------------------------------------------------


}

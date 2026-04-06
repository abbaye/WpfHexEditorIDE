// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: ViewModels/RelationshipsPanelViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     ViewModel for the Relationships panel. Lists all directed
//     relationships in the diagram document with selection state.
//
// Architecture Notes:
//     Pattern: ViewModel (MVVM).
//     Rebuilds the observable collection on SetDocument to stay in
//     sync with document changes (parse results). Selection raises
//     SelectedRelationshipChanged for host synchronisation.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Editor.ClassDiagram.ViewModels;

/// <summary>
/// ViewModel for the diagram relationships dockable panel.
/// </summary>
public sealed class RelationshipsPanelViewModel : ViewModelBase
{
    private readonly ObservableCollection<RelationshipViewModel> _relationships = [];
    private RelationshipViewModel? _selectedRelationship;

    // ---------------------------------------------------------------------------
    // Collections
    // ---------------------------------------------------------------------------

    public ObservableCollection<RelationshipViewModel> Relationships => _relationships;

    // ---------------------------------------------------------------------------
    // Properties
    // ---------------------------------------------------------------------------

    public RelationshipViewModel? SelectedRelationship
    {
        get => _selectedRelationship;
        set
        {
            if (_selectedRelationship == value) return;

            // Deselect previous
            if (_selectedRelationship is not null)
                _selectedRelationship.IsSelected = false;

            _selectedRelationship = value;

            if (_selectedRelationship is not null)
                _selectedRelationship.IsSelected = true;

            OnPropertyChanged();
            SelectedRelationshipChanged?.Invoke(this, value?.Relationship);
        }
    }

    // ---------------------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------------------

    public event EventHandler<ClassRelationship?>? SelectedRelationshipChanged;

    // ---------------------------------------------------------------------------
    // Document binding
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Rebuilds the relationships list from the given <paramref name="doc"/>.
    /// </summary>
    public void SetDocument(DiagramDocument doc)
    {
        _relationships.Clear();
        foreach (ClassRelationship rel in doc.Relationships)
            _relationships.Add(new RelationshipViewModel(rel));

        // Clear selection if the previously selected relationship no longer exists
        SelectedRelationship = null;
    }

    // ---------------------------------------------------------------------------
    // INotifyPropertyChanged
    // ---------------------------------------------------------------------------


}

// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: ViewModels/ClassMemberViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Observable wrapper around ClassMember for WPF data binding.
//     Adds IsEditing and EditText for in-place editing support
//     in the class box member list.
//
// Architecture Notes:
//     Pattern: ViewModel wrapper (Adapter).
//     ClassMember is a record (immutable); mutations create a new
//     record via 'with'. The host (ClassNodeViewModel) replaces
//     the member in the parent ClassNode on commit.
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Editor.ClassDiagram.ViewModels;

/// <summary>
/// Observable wrapper around a <see cref="ClassMember"/> record.
/// </summary>
public sealed class ClassMemberViewModel : ViewModelBase
{
    private ClassMember _member;
    private bool _isEditing;
    private string _editText = string.Empty;

    public ClassMemberViewModel(ClassMember member)
    {
        _member = member ?? throw new ArgumentNullException(nameof(member));
        _editText = member.DisplayLabel;
    }

    /// <summary>Underlying domain member record.</summary>
    public ClassMember Member => _member;

    // ---------------------------------------------------------------------------
    // Mirrored member properties
    // ---------------------------------------------------------------------------

    public string Name => _member.Name;
    public string TypeName => _member.TypeName;
    public MemberKind Kind => _member.Kind;
    public MemberVisibility Visibility => _member.Visibility;
    public bool IsStatic => _member.IsStatic;
    public bool IsAbstract => _member.IsAbstract;
    public string DisplayLabel => _member.DisplayLabel;

    /// <summary>Icon character (Segoe MDL2 Assets) representing the member kind.</summary>
    public string KindIcon => _member.Kind switch
    {
        MemberKind.Property => "\uE10C",  // Link
        MemberKind.Method   => "\uE8F4",  // Code
        MemberKind.Event    => "\uECAD",  // LightningBolt
        _                   => "\uE192"   // Field / List
    };

    // ---------------------------------------------------------------------------
    // Editing state
    // ---------------------------------------------------------------------------

    /// <summary>True when this member row is in inline-edit mode.</summary>
    public bool IsEditing
    {
        get => _isEditing;
        set { if (_isEditing == value) return; _isEditing = value; OnPropertyChanged(); }
    }

    /// <summary>Buffer for in-progress inline editing. Committed via <see cref="CommitEdit"/>.</summary>
    public string EditText
    {
        get => _editText;
        set { if (_editText == value) return; _editText = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Starts inline editing, populating <see cref="EditText"/> with the current label.
    /// </summary>
    public void BeginEdit()
    {
        EditText = _member.DisplayLabel;
        IsEditing = true;
    }

    /// <summary>
    /// Cancels inline editing without modifying the underlying member.
    /// </summary>
    public void CancelEdit()
    {
        EditText = _member.DisplayLabel;
        IsEditing = false;
    }

    /// <summary>
    /// Replaces the underlying member with an updated version and ends editing.
    /// The caller is responsible for updating the parent ClassNode members list.
    /// </summary>
    /// <returns>The updated <see cref="ClassMember"/> record.</returns>
    public ClassMember CommitEdit()
    {
        // Parse DisplayLabel format "Name : Type" â€” simple split on " : "
        string text = EditText.Trim();
        string name = text;
        string typeName = _member.TypeName;

        int colonIdx = text.IndexOf(" : ", StringComparison.Ordinal);
        if (colonIdx >= 0)
        {
            name = text[..colonIdx].Trim();
            typeName = text[(colonIdx + 3)..].Trim();
        }

        _member = _member with { Name = name, TypeName = typeName };
        IsEditing = false;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(TypeName));
        OnPropertyChanged(nameof(DisplayLabel));
        return _member;
    }

    // ---------------------------------------------------------------------------
    // INotifyPropertyChanged
    // ---------------------------------------------------------------------------


}

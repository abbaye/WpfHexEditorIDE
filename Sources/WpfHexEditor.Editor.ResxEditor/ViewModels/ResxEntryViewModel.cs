// ==========================================================
// Project: WpfHexEditor.Editor.ResxEditor
// File: ViewModels/ResxEntryViewModel.cs
// Description:
//     Observable wrapper for a ResxEntry.  Exposes bindable
//     Name/Value/Comment properties, type-badge metadata, and
//     dirty/validation state.  The "Silent" setters bypass
//     the change-tracking pipeline for undo/redo replay.
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Editor.ResxEditor.Models;

namespace WpfHexEditor.Editor.ResxEditor.ViewModels;

/// <summary>Observable wrapper for a single RESX data entry.</summary>
public sealed class ResxEntryViewModel : INotifyPropertyChanged
{
    // -- Backing fields -----------------------------------------------------

    private string  _name;
    private string  _value;
    private string  _comment;
    private bool    _isDirty;
    private bool    _hasValidationError;
    private string? _validationMessage;

    // -- Constructor --------------------------------------------------------

    public ResxEntryViewModel(ResxEntry entry)
    {
        SourceEntry = entry;
        _name       = entry.Name;
        _value      = entry.Value;
        _comment    = entry.Comment;
        TypeName    = entry.TypeName;
        MimeType    = entry.MimeType;
        Space       = entry.Space;
    }

    // -- Source snapshot (immutable, updated on save) -----------------------

    public ResxEntry SourceEntry { get; private set; }

    // -- Preserved XML attributes -------------------------------------------

    public string? TypeName { get; private set; }
    public string? MimeType { get; private set; }
    public string? Space    { get; private set; }

    // -- Editable properties ------------------------------------------------

    public string Name
    {
        get => _name;
        set { if (_name == value) return; _name = value; MarkDirty(); OnPropertyChanged(); }
    }

    public string Value
    {
        get => _value;
        set { if (_value == value) return; _value = value; MarkDirty(); OnPropertyChanged(); }
    }

    public string Comment
    {
        get => _comment;
        set { if (_comment == value) return; _comment = value; MarkDirty(); OnPropertyChanged(); }
    }

    // -- State flags --------------------------------------------------------

    public bool IsDirty
    {
        get => _isDirty;
        private set { if (_isDirty == value) return; _isDirty = value; OnPropertyChanged(); }
    }

    public bool HasValidationError
    {
        get => _hasValidationError;
        set { if (_hasValidationError == value) return; _hasValidationError = value; OnPropertyChanged(); }
    }

    public string? ValidationMessage
    {
        get => _validationMessage;
        set { if (_validationMessage == value) return; _validationMessage = value; OnPropertyChanged(); }
    }

    // -- Type badge ---------------------------------------------------------

    public ResxEntryType EntryType => SourceEntry.EntryType;

    public string TypeBadgeLabel => EntryType switch
    {
        ResxEntryType.String  => "STRING",
        ResxEntryType.Image   => "IMAGE",
        ResxEntryType.Binary  => "BINARY",
        ResxEntryType.FileRef => "FILEREF",
        _                     => "OTHER"
    };

    /// <summary>Resource key for the DynamicResource badge brush (resolved at render time).</summary>
    public string TypeBadgeBrushKey => EntryType switch
    {
        ResxEntryType.String  => "RES_StringTypeBadgeBrush",
        ResxEntryType.Image   => "RES_ImageTypeBadgeBrush",
        ResxEntryType.Binary  => "RES_BinaryTypeBadgeBrush",
        ResxEntryType.FileRef => "RES_FileRefTypeBadgeBrush",
        _                     => "RES_ForegroundBrush"
    };

    // -- Silent setters for undo/redo (bypass dirty tracking) ---------------

    public void SetNameSilent(string value)
    {
        _name = value;
        OnPropertyChanged(nameof(Name));
    }

    public void SetValueSilent(string value)
    {
        _value = value;
        OnPropertyChanged(nameof(Value));
    }

    public void SetCommentSilent(string value)
    {
        _comment = value;
        OnPropertyChanged(nameof(Comment));
    }

    // -- Snapshot helpers ---------------------------------------------------

    /// <summary>Returns the current state as an immutable <see cref="ResxEntry"/>.</summary>
    public ResxEntry ToEntry()
        => new(_name, _value, _comment, TypeName, MimeType, Space);

    /// <summary>Called after a successful save — resets dirty flag and updates source snapshot.</summary>
    public void MarkSaved()
    {
        SourceEntry = ToEntry();
        IsDirty     = false;
    }

    // -- INotifyPropertyChanged ---------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void MarkDirty() => IsDirty = true;
}

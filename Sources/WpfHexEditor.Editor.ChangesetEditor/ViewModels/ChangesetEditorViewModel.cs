// Apache 2.0 - 2026
// Contributors: Claude Sonnet 4.6

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Editor.ChangesetEditor.ViewModels;

// -- Row view-models ---------------------------------------------------------

public sealed class ModifiedEntryVm : INotifyPropertyChanged
{
    private string _offset = string.Empty;
    private string _values = string.Empty;

    public string Offset
    {
        get => _offset;
        set { _offset = value; OnPropertyChanged(); }
    }
    public string Values
    {
        get => _values;
        set { _values = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class InsertedEntryVm
{
    public string Offset { get; set; } = string.Empty;
    public string Bytes  { get; set; } = string.Empty;
}

public sealed class DeletedRangeVm
{
    public string Start { get; set; } = string.Empty;
    public long   Count { get; set; }
}

// -- Main ViewModel -----------------------------------------------------------

/// <summary>
/// ViewModel for <see cref="Controls.ChangesetEditorControl"/>.
/// Loads a <see cref="ChangesetDto"/> and exposes three observable collections
/// (Modified / Inserted / Deleted) for virtualized DataGrids.
/// </summary>
public sealed class ChangesetEditorViewModel : INotifyPropertyChanged
{
    // -- Collections ------------------------------------------------------

    public ObservableCollection<ModifiedEntryVm> ModifiedEntries { get; } = [];
    public ObservableCollection<InsertedEntryVm> InsertedEntries { get; } = [];
    public ObservableCollection<DeletedRangeVm>  DeletedRanges   { get; } = [];

    // -- Status ------------------------------------------------------------

    private string _sourceFile = string.Empty;
    public string SourceFile
    {
        get => _sourceFile;
        private set { _sourceFile = value; OnPropertyChanged(); }
    }

    private string _sourceHash = string.Empty;
    public string SourceHash
    {
        get => _sourceHash;
        private set { _sourceHash = value; OnPropertyChanged(); }
    }

    // -- Load --------------------------------------------------------------

    public void Load(ChangesetDto dto)
    {
        SourceFile = dto.SourceFile ?? string.Empty;
        SourceHash = dto.SourceHash ?? string.Empty;

        ModifiedEntries.Clear();
        foreach (var m in dto.Edits.Modified)
            ModifiedEntries.Add(new ModifiedEntryVm { Offset = m.Offset ?? string.Empty, Values = m.Values ?? string.Empty });

        InsertedEntries.Clear();
        foreach (var ins in dto.Edits.Inserted)
            InsertedEntries.Add(new InsertedEntryVm { Offset = ins.Offset ?? string.Empty, Bytes = ins.Bytes ?? string.Empty });

        DeletedRanges.Clear();
        foreach (var del in dto.Edits.Deleted)
            DeletedRanges.Add(new DeletedRangeVm { Start = del.Start ?? string.Empty, Count = del.Count });
    }

    public void Clear()
    {
        SourceFile = string.Empty;
        SourceHash = string.Empty;
        ModifiedEntries.Clear();
        InsertedEntries.Clear();
        DeletedRanges.Clear();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

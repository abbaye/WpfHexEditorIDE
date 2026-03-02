//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Core.CharacterTable;

namespace WpfHexEditor.Editor.TblEditor.ViewModels;

/// <summary>
/// ViewModel wrapping a single <see cref="Dte"/> entry for display and editing.
/// </summary>
public sealed class TblEntryViewModel : INotifyPropertyChanged
{
    private string _entry;
    private string? _value;
    private DteType _type;
    private string? _comment;
    private bool _hasConflict;
    private bool _isValid = true;
    private string? _validationError;

    public TblEntryViewModel(Dte dte)
    {
        ArgumentNullException.ThrowIfNull(dte);
        _entry   = dte.Entry;
        _value   = dte.Value;
        _type    = dte.Type;
        _comment = dte.Comment;
    }

    // ── Core fields ────────────────────────────────────────────────────────

    /// <summary>
    /// Hex byte sequence (uppercase, e.g. "41" or "8283").
    /// </summary>
    public string Entry
    {
        get => _entry;
        set { if (_entry != value) { _entry = value; OnPropertyChanged(); OnPropertyChanged(nameof(ByteLength)); } }
    }

    /// <summary>
    /// Decoded character(s), e.g. "A" or "の".
    /// </summary>
    public string? Value
    {
        get => _value;
        set { if (_value != value) { _value = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// DTE type (auto-detected by <see cref="Dte"/>).
    /// </summary>
    public DteType Type
    {
        get => _type;
        set { if (_type != value) { _type = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Optional comment (not saved to .tbl, used in TBLX format).
    /// </summary>
    public string? Comment
    {
        get => _comment;
        set { if (_comment != value) { _comment = value; OnPropertyChanged(); } }
    }

    // ── Computed ───────────────────────────────────────────────────────────

    /// <summary>
    /// Number of bytes this entry encodes (Entry.Length / 2).
    /// </summary>
    public int ByteLength => Entry.Length / 2;

    /// <summary>
    /// Short UI label matching the badge displayed in the Type column
    /// (e.g. "ASC", "DTE", "MTE", "JPN", "EOL", "EOB", "INV").
    /// Used by the text search filter so searching "MTE" shows only MTE entries.
    /// </summary>
    public string TypeLabel => Type switch
    {
        DteType.Ascii                 => "ASC",
        DteType.DualTitleEncoding     => "DTE",
        DteType.MultipleTitleEncoding => "MTE",
        DteType.Japonais              => "JPN",
        DteType.EndLine               => "EOL",
        DteType.EndBlock              => "EOB",
        DteType.Invalid               => "INV",
        _                             => Type.ToString(),
    };

    // ── State ──────────────────────────────────────────────────────────────

    /// <summary>
    /// True when this entry has a prefix conflict with another entry.
    /// </summary>
    public bool HasConflict
    {
        get => _hasConflict;
        set { if (_hasConflict != value) { _hasConflict = value; OnPropertyChanged(); } }
    }

    public bool IsValid
    {
        get => _isValid;
        set { if (_isValid != value) { _isValid = value; OnPropertyChanged(); } }
    }

    public string? ValidationError
    {
        get => _validationError;
        set { if (_validationError != value) { _validationError = value; OnPropertyChanged(); } }
    }

    // ── Conversion ─────────────────────────────────────────────────────────

    /// <summary>
    /// Converts back to a <see cref="Dte"/> for persistence.
    /// </summary>
    public Dte ToDto()
    {
        var dte = new Dte(Entry, Value ?? string.Empty);
        if (!string.IsNullOrEmpty(Comment)) dte.Comment = Comment;
        return dte;
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

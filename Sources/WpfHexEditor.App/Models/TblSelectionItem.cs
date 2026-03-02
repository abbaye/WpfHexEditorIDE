//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Core;
using WpfHexEditor.Core.CharacterTable;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.App.Models;

/// <summary>
/// Discriminates the kind of entry in the TBL toolbar ComboBox.
/// </summary>
public enum TblSelectionKind
{
    /// <summary>A section header (non-selectable).</summary>
    Header,
    /// <summary>A visual separator (non-selectable).</summary>
    Separator,
    /// <summary>A built-in TBL (Default/ASCII/EBCDIC).</summary>
    BuiltIn,
    /// <summary>A standard encoding (UTF-8, Latin-1…).</summary>
    Encoding,
    /// <summary>A project-local .tbl or .tblx file.</summary>
    ProjectFile,
}

/// <summary>
/// Data model for one row in the TBL selection ComboBox in the main toolbar.
/// </summary>
public sealed class TblSelectionItem
{
    public string                     DisplayName  { get; init; } = "";
    public TblSelectionKind           Kind         { get; init; }

    /// <summary>Non-null only when <see cref="Kind"/> == <see cref="TblSelectionKind.BuiltIn"/>.
    /// <see langword="null"/> means "Default (ASCII)" → calls <c>CloseTBL()</c>.</summary>
    public DefaultCharacterTableType? BuiltInType  { get; init; }

    /// <summary>Non-null only when <see cref="Kind"/> == <see cref="TblSelectionKind.Encoding"/>.</summary>
    public CharacterTableType?        EncodingType { get; init; }

    /// <summary>Non-null only when <see cref="Kind"/> == <see cref="TblSelectionKind.ProjectFile"/>.</summary>
    public IProjectItem?              ProjectItem  { get; init; }

    /// <summary>True for rows the user can actually select (not headers or separators).</summary>
    public bool IsSelectable => Kind is TblSelectionKind.BuiltIn
                                     or TblSelectionKind.Encoding
                                     or TblSelectionKind.ProjectFile;

    /// <summary>True for section header rows (bold label, non-interactive).</summary>
    public bool IsHeader    => Kind == TblSelectionKind.Header;

    /// <summary>True for visual separator rows.</summary>
    public bool IsSeparator => Kind == TblSelectionKind.Separator;

    // ── Convenience factories ─────────────────────────────────────────────

    public static TblSelectionItem MakeHeader(string text)
        => new() { DisplayName = text, Kind = TblSelectionKind.Header };

    public static TblSelectionItem MakeSeparator()
        => new() { Kind = TblSelectionKind.Separator };

    public static TblSelectionItem MakeBuiltIn(string name, DefaultCharacterTableType? type)
        => new() { DisplayName = name, Kind = TblSelectionKind.BuiltIn, BuiltInType = type };

    public static TblSelectionItem MakeEncoding(string name, CharacterTableType enc)
        => new() { DisplayName = name, Kind = TblSelectionKind.Encoding, EncodingType = enc };

    public static TblSelectionItem MakeProjectFile(IProjectItem item)
        => new() { DisplayName = item.Name, Kind = TblSelectionKind.ProjectFile, ProjectItem = item };
}

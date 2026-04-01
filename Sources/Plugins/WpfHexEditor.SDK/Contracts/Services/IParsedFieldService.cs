//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.SDK.Contracts.Services;

/// <summary>
/// A single parsed field entry from the active format definition.
/// </summary>
public sealed class ParsedFieldEntry
{
    /// <summary>Field name as defined in the format specification.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Data type description (e.g. "UInt32", "String", "Bytes[4]").</summary>
    public string DataType { get; init; } = string.Empty;

    /// <summary>Byte offset from the start of the file.</summary>
    public long Offset { get; init; }

    /// <summary>Field length in bytes.</summary>
    public int Length { get; init; }

    /// <summary>Decoded value as a display string.</summary>
    public string ValueDisplay { get; init; } = string.Empty;
}

/// <summary>
/// Provides access to the parsed fields for the currently active document.
/// Requires <c>AccessHexEditor</c> permission.
/// </summary>
public interface IParsedFieldService
{
    /// <summary>Gets whether parsed field data is available for the active document.</summary>
    bool HasParsedFields { get; }

    /// <summary>Gets all parsed field entries for the active document.</summary>
    IReadOnlyList<ParsedFieldEntry> GetParsedFields();

    /// <summary>Gets the parsed field at the specified byte offset, or null if none.</summary>
    ParsedFieldEntry? GetFieldAtOffset(long offset);

    /// <summary>
    /// Raised when the parsed fields are refreshed (new file, new format applied).
    /// Raised on the UI thread.
    /// </summary>
    event EventHandler ParsedFieldsChanged;
}

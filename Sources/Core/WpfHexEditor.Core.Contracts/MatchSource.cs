//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Contracts;

/// <summary>
/// Describes how a <see cref="FormatMatchResult"/> was produced.
/// </summary>
[Flags]
public enum MatchSource
{
    /// <summary>No match source — indicates an unresolved result.</summary>
    None = 0,

    /// <summary>Match was derived from the file extension (e.g. ".zip").</summary>
    Extension = 1 << 0,

    /// <summary>Match was confirmed by magic-byte signature scoring.</summary>
    MagicBytes = 1 << 1,

    /// <summary>Match was derived from a MIME type string.</summary>
    MimeType = 1 << 2,

    /// <summary>Match was produced by combining Extension + MagicBytes (highest confidence).</summary>
    Combined = Extension | MagicBytes,
}

//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7 (1M context)
//////////////////////////////////////////////
// Project: WpfHexEditor.Core.Definitions
// File: Models/WhfmtJsonOptions.cs
// Description: Single source of truth for the JSONC parser options used to
//              read .whfmt files (comments + trailing commas tolerated).
//              Consolidates 7 in-assembly duplicates discovered during the
//              P11 /simplify pass.
//////////////////////////////////////////////

using System.Text.Json;

namespace WpfHexEditor.Core.Definitions.Models;

/// <summary>
/// Shared <see cref="JsonDocumentOptions"/> for parsing .whfmt content as JSONC.
/// .whfmt files routinely carry <c>// line</c> and <c>/* block */</c> comments
/// plus trailing commas — every JSON read site in <c>Core.Definitions</c> needs
/// the same configuration.
/// </summary>
internal static class WhfmtJsonOptions
{
    /// <summary><see cref="JsonDocumentOptions"/> for reading .whfmt JSONC content.</summary>
    public static readonly JsonDocumentOptions Jsonc = new()
    {
        CommentHandling     = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}

//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Project: WpfHexEditor.Core
// File: FormatLoadFailure.cs
// Description: Represents a single .whfmt file that failed to load during catalog initialization.
// Architecture Notes:
//     Immutable record — captured in FormatCatalogService (IDE pipeline) and
//     HexEditor.FormatDetection (standalone pipeline). Surfaced via OutputLogger
//     (IDE) or StatusBar (standalone).
//////////////////////////////////////////////

namespace WpfHexEditor.Core.FormatDetection;

/// <summary>
/// A lightweight descriptor for a whfmt file that failed to load.
/// </summary>
/// <param name="Source">Resource key or file path of the failing whfmt.</param>
/// <param name="Reason">Short error message (ex.Message or "IsValid() = false").</param>
public sealed record FormatLoadFailure(string Source, string Reason);

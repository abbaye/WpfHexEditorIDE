//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Lightweight hint produced by scanning the active solution for ROM-like files.
/// Passed to <see cref="ConvertTblDialog"/> (ProjectSystem) so the Game Metadata
/// section can be pre-filled automatically.
/// </summary>
/// <param name="Platform">Detected platform string, e.g. "SNES", "NES", "GBA".</param>
/// <param name="Region">Detected region if available, e.g. "Japan", "USA".</param>
/// <param name="GameTitle">Detected or derived game title (may be empty).</param>
/// <param name="SourceFileName">
///   File name of the ROM / binary that provided this hint (displayed in the UI).
/// </param>
public sealed record GameRomHint(
    string  Platform,
    string  Region,
    string  GameTitle,
    string  SourceFileName)
{
    /// <summary>
    /// Human-readable label used by UI selectors (ComboBox display).
    /// </summary>
    public override string ToString()
    {
        var title    = string.IsNullOrWhiteSpace(GameTitle) ? SourceFileName : GameTitle;
        var platform = string.IsNullOrWhiteSpace(Platform)  ? string.Empty   : $" [{Platform}]";
        var region   = string.IsNullOrWhiteSpace(Region)    ? string.Empty   : $" – {Region}";
        return $"{title}{platform}{region}";
    }
}

//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Terminal;

public enum TerminalOutputKind { Standard, Error, Warning, Info }

/// <summary>A single line of terminal output, with kind and timestamp.</summary>
public sealed class TerminalOutputLine(string text, TerminalOutputKind kind)
{
    public string Text { get; } = text;
    public TerminalOutputKind Kind { get; } = kind;
    public DateTime Timestamp { get; } = DateTime.Now;
}

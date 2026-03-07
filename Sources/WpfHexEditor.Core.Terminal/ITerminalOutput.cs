//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal;

/// <summary>
/// Output sink for terminal command results.
/// </summary>
public interface ITerminalOutput
{
    void Write(string text);
    void WriteLine(string text = "");
    void WriteError(string text);
    void WriteWarning(string text);
    void Clear();
}

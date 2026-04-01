//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.SDK.Contracts.Services;

/// <summary>
/// Provides access to the IDE OutputPanel for plugin log messages.
/// Requires <c>WriteOutput</c> permission.
/// </summary>
public interface IOutputService
{
    /// <summary>Writes an informational message to the OutputPanel.</summary>
    /// <param name="message">Message text.</param>
    void Info(string message);

    /// <summary>Writes a warning message to the OutputPanel.</summary>
    void Warning(string message);

    /// <summary>Writes an error message to the OutputPanel (displayed in red).</summary>
    void Error(string message);

    /// <summary>Writes a debug-level message (only visible when debug output is enabled).</summary>
    void Debug(string message);

    /// <summary>
    /// Writes a message with explicit category label (e.g. plugin name prefix).
    /// </summary>
    /// <param name="category">Category prefix shown in the output line (e.g. "MyPlugin").</param>
    /// <param name="message">Message text.</param>
    void Write(string category, string message);

    /// <summary>Clears all messages from the OutputPanel.</summary>
    void Clear();

    /// <summary>Returns the last <paramref name="count"/> log lines, newest last.</summary>
    IReadOnlyList<string> GetRecentLines(int count);
}

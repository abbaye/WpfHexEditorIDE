//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.SDK.Contracts.Services;

/// <summary>
/// Diagnostic severity level for error panel entries.
/// </summary>
public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>
/// Provides access to the IDE ErrorPanel for plugin diagnostics.
/// Requires <c>WriteErrorPanel</c> permission.
/// </summary>
public interface IErrorPanelService
{
    /// <summary>
    /// Posts a diagnostic entry to the ErrorPanel.
    /// </summary>
    /// <param name="severity">Entry severity.</param>
    /// <param name="message">Human-readable message.</param>
    /// <param name="source">Source identifier (e.g. plugin name, file name).</param>
    /// <param name="line">Source line number (-1 if not applicable).</param>
    /// <param name="column">Source column number (-1 if not applicable).</param>
    void PostDiagnostic(DiagnosticSeverity severity, string message, string source = "", int line = -1, int column = -1);

    /// <summary>Clears all diagnostic entries previously posted by this plugin.</summary>
    void ClearPluginDiagnostics(string pluginId);

    /// <summary>Returns the last <paramref name="count"/> error/warning messages (as formatted strings).</summary>
    IReadOnlyList<string> GetRecentErrors(int count);
}

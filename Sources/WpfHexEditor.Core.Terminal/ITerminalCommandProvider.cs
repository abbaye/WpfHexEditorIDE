//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Terminal;

/// <summary>
/// Contract for a terminal command that can be registered and executed.
/// </summary>
public interface ITerminalCommandProvider
{
    /// <summary>Primary command name (lowercase, no spaces).</summary>
    string CommandName { get; }

    /// <summary>Short description shown in help output.</summary>
    string Description { get; }

    /// <summary>Usage syntax shown in help output.</summary>
    string Usage { get; }

    /// <summary>
    /// Executes the command asynchronously.
    /// </summary>
    /// <param name="args">Arguments after the command name.</param>
    /// <param name="output">Output sink for writing results.</param>
    /// <param name="context">IDE host context for cross-service access.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Exit code: 0 = success, non-zero = error.</returns>
    Task<int> ExecuteAsync(
        string[] args,
        ITerminalOutput output,
        ITerminalContext context,
        CancellationToken ct = default);
}

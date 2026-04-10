// ==========================================================
// Project: WpfHexEditor.Terminal.Abstractions
// File: ITerminalContext.cs
// Description:
//     Execution context passed to every terminal command.
//     Zero-dependency contract — IDE-specific members are accessed
//     via the HostServices property (cast to IIDEHostContext at runtime).
// ==========================================================

namespace WpfHexEditor.SDK.Contracts.Terminal;

/// <summary>
/// Execution context passed to every terminal command.
/// Provides access to the current session state and optional host services.
/// </summary>
public interface ITerminalContext
{
    /// <summary>Current working directory for file-system commands.</summary>
    string WorkingDirectory { get; }

    /// <summary>
    /// Host services object. In the IDE this is <c>IIDEHostContext</c>.
    /// Access IDE members via the <c>IDE</c> extension property or cast directly.
    /// </summary>
    object? HostServices { get; }
}

/// <summary>
/// Extension methods for <see cref="ITerminalContext"/> to access IDE host services.
/// </summary>
public static class TerminalContextExtensions
{
    /// <summary>
    /// Returns <see cref="ITerminalContext.HostServices"/> as <c>dynamic</c>.
    /// BuiltIn commands use this to access IDE services without a compile-time SDK dependency.
    /// </summary>
    public static dynamic IDE(this ITerminalContext context)
        => context.HostServices ?? throw new InvalidOperationException(
            "No IDE host available in this terminal context.");
}

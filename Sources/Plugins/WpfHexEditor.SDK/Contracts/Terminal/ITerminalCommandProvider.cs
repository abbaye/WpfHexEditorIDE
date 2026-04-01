// ==========================================================
// Project: WpfHexEditor.SDK
// File: Contracts/Terminal/ITerminalCommandProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description:
//     SDK contract for a terminal command that plugins can register
//     and the HxTerminal can dispatch to.
//     Moved from WpfHexEditor.Core.Terminal to the SDK so that
//     plugin authors can implement commands without a Core.Terminal dependency.
//
// Architecture Notes:
//     WpfHexEditor.Core.Terminal re-exports this type via a global using alias
//     so all existing built-in commands compile without any changes.
//     Registration: context.Terminal.RegisterCommand(new MyCommand()) in InitializeAsync.
//     Shutdown:     context.Terminal.UnregisterCommand("my-cmd")  in ShutdownAsync.
// ==========================================================

namespace WpfHexEditor.SDK.Contracts.Terminal;

/// <summary>
/// Contract for a terminal command that can be registered with
/// <see cref="WpfHexEditor.SDK.Contracts.Services.ITerminalService.RegisterCommand"/>
/// and dispatched by the HxTerminal engine.
/// </summary>
public interface ITerminalCommandProvider
{
    /// <summary>Primary command name (lowercase, no spaces).</summary>
    string CommandName { get; }

    /// <summary>Short description shown in <c>help</c> output.</summary>
    string Description { get; }

    /// <summary>Usage syntax shown in <c>help</c> output.</summary>
    string Usage { get; }

    /// <summary>
    /// Origin of the command, used to group entries in <c>help</c> output.
    /// <list type="bullet">
    ///   <item><c>null</c> — Built-in (registered by the core terminal engine).</item>
    ///   <item><c>"Plugin"</c> — Contributed by a plugin via <see cref="WpfHexEditor.SDK.Contracts.Services.ITerminalService.RegisterCommand"/>.</item>
    ///   <item><c>"Script"</c> — Registered by a user script at runtime.</item>
    ///   <item>Any other string — Custom source label defined by the registrant.</item>
    /// </list>
    /// </summary>
    string? Source => null;

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

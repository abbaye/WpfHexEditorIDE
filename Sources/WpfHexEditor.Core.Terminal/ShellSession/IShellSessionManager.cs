// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: IShellSessionManager.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-08
// Description:
//     Contract for managing the lifecycle of multiple terminal sessions.
//     Provides create, close, switch, and enumeration operations.
//
// Architecture Notes:
//     Pattern: Repository + Observer.
//     Feature #92: Multi-tab terminal sessions.
//     Implemented by ShellSessionManager; consumed by TerminalPanelViewModel
//     and TerminalServiceImpl (SDK bridge).
//
// ==========================================================

namespace WpfHexEditor.Core.Terminal.ShellSession;

/// <summary>
/// Manages the lifecycle of <see cref="ShellSession"/> instances (create, close, switch, enumerate).
/// </summary>
public interface IShellSessionManager : IDisposable
{
    // -- Factory ------------------------------------------------------------------

    /// <summary>
    /// Creates a new session of the specified shell type and makes it active.
    /// Fires <see cref="SessionCreated"/> and <see cref="ActiveSessionChanged"/>.
    /// </summary>
    /// <param name="shellType">The shell to run in the new session.</param>
    /// <param name="title">Optional display title; a default is generated if null.</param>
    ShellSession CreateSession(TerminalShellType shellType, string? title = null);

    // -- Lifecycle ----------------------------------------------------------------

    /// <summary>
    /// Closes and disposes a session by id.
    /// If the closed session was active, the preceding session (or next if none) becomes active.
    /// Fires <see cref="SessionClosed"/> and possibly <see cref="ActiveSessionChanged"/>.
    /// The last session cannot be closed.
    /// </summary>
    void CloseSession(Guid sessionId);

    // -- Active session -----------------------------------------------------------

    /// <summary>Currently active session (never null when at least one session exists).</summary>
    ShellSession? ActiveSession { get; }

    /// <summary>
    /// Switches the active session to <paramref name="sessionId"/>.
    /// Fires <see cref="ActiveSessionChanged"/>.
    /// </summary>
    void SetActiveSession(Guid sessionId);

    // -- Enumeration --------------------------------------------------------------

    /// <summary>Read-only snapshot of all open sessions in creation order.</summary>
    IReadOnlyList<ShellSession> Sessions { get; }

    // -- Events -------------------------------------------------------------------

    /// <summary>Raised after a new session is created.</summary>
    event EventHandler<ShellSession> SessionCreated;

    /// <summary>Raised after a session is closed. Argument is the closed session id.</summary>
    event EventHandler<Guid> SessionClosed;

    /// <summary>Raised after the active session changes.</summary>
    event EventHandler<ShellSession> ActiveSessionChanged;
}

// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: ShellSession.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-08
// Description:
//     Core domain model for a single terminal session tab.
//     Encapsulates process lifecycle, per-session history, working directory,
//     and shell type. Output line storage is intentionally left to the
//     WPF layer (ShellSessionViewModel) to avoid a UI dependency here.
//
// Architecture Notes:
//     Pattern: Domain Model — no WPF / ObservableCollection dependency.
//     Feature #92: Multi-tab terminal with separate shell sessions.
//     The companion ShellSessionViewModel (WpfHexEditor.Terminal) owns
//     the ObservableCollection<TerminalOutputLine> for UI data binding.
//
// ==========================================================

using System.Diagnostics;
using System.IO;

namespace WpfHexEditor.Core.Terminal.ShellSession;

/// <summary>
/// Represents one terminal session: its shell type, process, working directory,
/// and per-session command history. Created and managed by <see cref="ShellSessionManager"/>.
/// </summary>
public sealed class ShellSession : IDisposable
{
    private bool _disposed;

    // -- Identity -----------------------------------------------------------------

    /// <summary>Unique identifier for this session.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Display name shown on the session tab header.</summary>
    public string TabTitle { get; set; }

    /// <summary>Shell type running in this session.</summary>
    public TerminalShellType ShellType { get; }

    /// <summary>UTC time at which this session was created.</summary>
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    // -- State --------------------------------------------------------------------

    /// <summary>Current working directory (used by file-system commands and prompt display).</summary>
    public string WorkingDirectory { get; set; }
        = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // -- External process (null for HxTerminal) -----------------------------------

    /// <summary>External shell process handle. Null when ShellType is HxTerminal.</summary>
    public Process? ShellProcess { get; set; }

    /// <summary>Stdin writer for the external shell process.</summary>
    public StreamWriter? ShellInput { get; set; }

    /// <summary>Returns true when an external shell process is alive.</summary>
    public bool IsExternalShellRunning => ShellProcess is { HasExited: false };

    // -- Per-session history ------------------------------------------------------

    /// <summary>Command history specific to this session (not shared across tabs).</summary>
    public CommandHistory History { get; } = new();

    // -- Constructor --------------------------------------------------------------

    /// <summary>Initialises a new session with the specified shell type and tab title.</summary>
    public ShellSession(TerminalShellType shellType, string tabTitle)
    {
        ShellType = shellType;
        TabTitle  = tabTitle;
    }

    // -- IDisposable --------------------------------------------------------------

    /// <summary>
    /// Kills the external shell process (if alive) and releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        ShellInput?.Dispose();
        ShellInput = null;

        if (ShellProcess is not null)
        {
            if (!ShellProcess.HasExited)
            {
                try { ShellProcess.Kill(entireProcessTree: true); }
                catch { /* best-effort */ }
            }

            ShellProcess.Dispose();
            ShellProcess = null;
        }
    }
}

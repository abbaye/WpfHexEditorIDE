// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: ShellSessionManager.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-08
// Description:
//     Concrete implementation of IShellSessionManager.
//     Manages creation, closure, and active-session switching for
//     multi-tab terminal sessions.
//
// Architecture Notes:
//     Pattern: Repository + Observer.
//     Feature #92: Multi-tab terminal sessions.
//     Thread-safe via a single _lock guard on the session list.
//     Events are fired outside the lock to prevent deadlocks.
//
// ==========================================================

namespace WpfHexEditor.Core.Terminal.ShellSession;

/// <summary>
/// Thread-safe manager for multiple <see cref="ShellSession"/> instances.
/// </summary>
public sealed class ShellSessionManager : IShellSessionManager
{
    private readonly List<ShellSession> _sessions = [];
    private readonly object _lock = new();
    private readonly Dictionary<TerminalShellType, int> _counters = [];

    private ShellSession? _activeSession;
    private bool _disposed;

    // -- IShellSessionManager: Events ---------------------------------------------

    public event EventHandler<ShellSession>? SessionCreated;
    public event EventHandler<Guid>? SessionClosed;
    public event EventHandler<ShellSession>? ActiveSessionChanged;

    // -- IShellSessionManager: Properties -----------------------------------------

    public ShellSession? ActiveSession
    {
        get { lock (_lock) return _activeSession; }
    }

    public IReadOnlyList<ShellSession> Sessions
    {
        get { lock (_lock) return _sessions.ToList(); }
    }

    // -- IShellSessionManager: Factory --------------------------------------------

    /// <inheritdoc/>
    public ShellSession CreateSession(TerminalShellType shellType, string? title = null)
    {
        string tabTitle = title ?? BuildDefaultTitle(shellType);
        var session = new ShellSession(shellType, tabTitle);

        lock (_lock)
        {
            _sessions.Add(session);
            _activeSession = session;
        }

        SessionCreated?.Invoke(this, session);
        ActiveSessionChanged?.Invoke(this, session);

        return session;
    }

    // -- IShellSessionManager: Lifecycle ------------------------------------------

    /// <inheritdoc/>
    public void CloseSession(Guid sessionId)
    {
        ShellSession? toClose = null;
        ShellSession? newActive = null;
        bool activeChanged = false;

        lock (_lock)
        {
            var idx = _sessions.FindIndex(s => s.Id == sessionId);
            if (idx < 0) return;

            // Prevent closing the last session.
            if (_sessions.Count <= 1) return;

            toClose = _sessions[idx];
            _sessions.RemoveAt(idx);

            if (_activeSession?.Id == sessionId)
            {
                // Switch to the preceding session, or the new last one.
                newActive = idx > 0 ? _sessions[idx - 1] : _sessions[0];
                _activeSession = newActive;
                activeChanged = true;
            }
        }

        if (toClose is null) return;

        toClose.Dispose();
        SessionClosed?.Invoke(this, sessionId);

        if (activeChanged && newActive is not null)
            ActiveSessionChanged?.Invoke(this, newActive);
    }

    // -- IShellSessionManager: Active session --------------------------------------

    /// <inheritdoc/>
    public void SetActiveSession(Guid sessionId)
    {
        ShellSession? found;
        lock (_lock)
        {
            found = _sessions.Find(s => s.Id == sessionId);
            if (found is null || found == _activeSession) return;
            _activeSession = found;
        }

        ActiveSessionChanged?.Invoke(this, found);
    }

    // -- Private helpers ----------------------------------------------------------

    private string BuildDefaultTitle(TerminalShellType shellType)
    {
        lock (_lock)
        {
            _counters.TryGetValue(shellType, out var n);
            _counters[shellType] = ++n;
            return shellType switch
            {
                TerminalShellType.PowerShell => $"PowerShell {n}",
                TerminalShellType.Bash       => $"Bash {n}",
                TerminalShellType.Cmd        => $"CMD {n}",
                _                            => $"HxTerminal {n}"
            };
        }
    }

    // -- IDisposable --------------------------------------------------------------

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        List<ShellSession> snapshot;
        lock (_lock)
        {
            snapshot = [.._sessions];
            _sessions.Clear();
            _activeSession = null;
        }

        foreach (var s in snapshot)
            s.Dispose();
    }
}

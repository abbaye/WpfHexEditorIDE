// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: ViewModels/DebugSessionManagerViewModel.cs
// Description:
//     Tracks multiple concurrent debug sessions for display in
//     DebugConsolePanel tabs. Each session exposes an ID, language,
//     and output log, allowing the user to switch between them.
//
// Architecture Notes:
//     One entry per active debug session, keyed by session ID.
//     Sessions added on DebugSessionStartedEvent, removed on DebugSessionEndedEvent.
//     NotifyPropertyChanged via ViewModelBase.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfHexEditor.Plugins.Debugger.ViewModels;

/// <summary>
/// Represents one active debug session entry in the session tab list.
/// </summary>
public sealed class DebugSessionEntry : INotifyPropertyChanged
{
    private bool   _isActive;
    private string _output = string.Empty;

    public string SessionId   { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string LanguageId  { get; init; } = string.Empty;

    /// <summary>True when this is the currently selected session in the UI.</summary>
    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; OnPropertyChanged(); }
    }

    /// <summary>Accumulated stdout/stderr from the debuggee.</summary>
    public string Output
    {
        get => _output;
        set { _output = value; OnPropertyChanged(); }
    }

    /// <summary>Append a line to <see cref="Output"/> (call on UI thread).</summary>
    public void AppendOutput(string text) => Output += text;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// ViewModel that owns all active <see cref="DebugSessionEntry"/> objects.
/// Bound to the session tab strip in <c>DebugConsolePanel</c>.
/// </summary>
public sealed class DebugSessionManagerViewModel : INotifyPropertyChanged
{
    private DebugSessionEntry? _activeSession;

    /// <summary>All currently running debug sessions.</summary>
    public ObservableCollection<DebugSessionEntry> Sessions { get; } = [];

    /// <summary>The session whose output is displayed in the console pane.</summary>
    public DebugSessionEntry? ActiveSession
    {
        get => _activeSession;
        set
        {
            if (_activeSession == value) return;
            if (_activeSession is not null) _activeSession.IsActive = false;
            _activeSession = value;
            if (_activeSession is not null) _activeSession.IsActive = true;
            OnPropertyChanged();
        }
    }

    /// <summary>Add a new session and make it the active one.</summary>
    public void AddSession(string sessionId, string displayName, string languageId)
    {
        var entry = new DebugSessionEntry
        {
            SessionId   = sessionId,
            DisplayName = displayName,
            LanguageId  = languageId,
        };
        Sessions.Add(entry);
        ActiveSession = entry;
    }

    /// <summary>Append output text to the given session (no-op if session not found).</summary>
    public void AppendOutput(string sessionId, string text)
    {
        var session = Sessions.FirstOrDefault(s => s.SessionId == sessionId);
        session?.AppendOutput(text);
    }

    /// <summary>Remove the session and activate the most recent remaining one.</summary>
    public void RemoveSession(string sessionId)
    {
        var entry = Sessions.FirstOrDefault(s => s.SessionId == sessionId);
        if (entry is null) return;
        Sessions.Remove(entry);
        if (ActiveSession == entry)
            ActiveSession = Sessions.LastOrDefault();
    }

    /// <summary>True when at least one session is active.</summary>
    public bool HasSessions => Sessions.Count > 0;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

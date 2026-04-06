// ==========================================================
// Project: WpfHexEditor.PluginDev
// File: Panels/PluginDevLogViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     ViewModel for the PluginDevLogPanel.
//     Manages the observable log entry collection, level-filtering,
//     and log export. Thread-safe via Dispatcher marshalling.
//
// Architecture Notes:
//     Pattern: ViewModel (MVVM) + Observer (listens to IProgress<string>).
//     Log entries are appended on the UI thread via Dispatcher.Invoke.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.PluginDev.Panels;

/// <summary>
/// Log entry severity levels.
/// </summary>
public enum LogLevel { Debug, Info, Warning, Error }

/// <summary>
/// A single log entry in the PluginDevLogPanel.
/// </summary>
public sealed record LogEntry(
    DateTime  Timestamp,
    LogLevel  Level,
    string    Source,
    string    Message);

/// <summary>
/// ViewModel for the Plugin Developer Log panel.
/// </summary>
public sealed class PluginDevLogViewModel : ViewModelBase, IProgress<string>
{
    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------

    private readonly Dispatcher              _dispatcher;
    private readonly List<LogEntry>          _allEntries = [];
    private          LogLevel?               _filterLevel;
    private          string                  _filterSource = string.Empty;
    private          bool                    _autoScroll   = true;

    // -----------------------------------------------------------------------
    // Construction
    // -----------------------------------------------------------------------

    public PluginDevLogViewModel()
    {
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    // -----------------------------------------------------------------------
    // Observable collection
    // -----------------------------------------------------------------------

    /// <summary>Filtered entries bound to the ListView.</summary>
    public ObservableCollection<LogEntry> Entries { get; } = [];

    // -----------------------------------------------------------------------
    // Filter properties
    // -----------------------------------------------------------------------

    public bool ShowDebug   { get => GetShow(LogLevel.Debug);   set => SetShow(LogLevel.Debug,   value); }
    public bool ShowInfo    { get => GetShow(LogLevel.Info);    set => SetShow(LogLevel.Info,    value); }
    public bool ShowWarning { get => GetShow(LogLevel.Warning); set => SetShow(LogLevel.Warning, value); }
    public bool ShowError   { get => GetShow(LogLevel.Error);   set => SetShow(LogLevel.Error,   value); }

    private readonly HashSet<LogLevel> _hiddenLevels = [];
    private bool GetShow(LogLevel l)    => !_hiddenLevels.Contains(l);
    private void SetShow(LogLevel l, bool v)
    {
        if (v) _hiddenLevels.Remove(l); else _hiddenLevels.Add(l);
        OnPropertyChanged(l.ToString());
        RefreshFilter();
    }

    public string FilterSource
    {
        get => _filterSource;
        set { _filterSource = value; OnPropertyChanged(); RefreshFilter(); }
    }

    public bool AutoScroll
    {
        get => _autoScroll;
        set { _autoScroll = value; OnPropertyChanged(); }
    }

    // -----------------------------------------------------------------------
    // IProgress<string>
    // -----------------------------------------------------------------------

    /// <summary>
    /// Parses a log line prefixed with [Level] Source: Message and appends it.
    /// If no prefix is detected the entry is added as Info from "Build".
    /// </summary>
    void IProgress<string>.Report(string value)
    {
        var entry = ParseLogLine(value);
        _dispatcher.Invoke(() => AppendEntry(entry));
    }

    // -----------------------------------------------------------------------
    // Public commands
    // -----------------------------------------------------------------------

    /// <summary>Adds an entry directly (e.g. from a system event).</summary>
    public void Add(LogLevel level, string source, string message)
    {
        var entry = new LogEntry(DateTime.Now, level, source, message);
        if (_dispatcher.CheckAccess())
            AppendEntry(entry);
        else
            _dispatcher.Invoke(() => AppendEntry(entry));
    }

    /// <summary>Clears all log entries.</summary>
    public void Clear()
    {
        _allEntries.Clear();
        Entries.Clear();
    }

    /// <summary>Exports all entries to a string.</summary>
    public string Export()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var e in _allEntries)
            sb.AppendLine($"{e.Timestamp:HH:mm:ss.fff} [{e.Level}] {e.Source}: {e.Message}");
        return sb.ToString();
    }

    // -----------------------------------------------------------------------
    // INotifyPropertyChanged
    // -----------------------------------------------------------------------


    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private void AppendEntry(LogEntry entry)
    {
        _allEntries.Add(entry);

        if (!_hiddenLevels.Contains(entry.Level) &&
            (string.IsNullOrEmpty(_filterSource) ||
             entry.Source.Contains(_filterSource, StringComparison.OrdinalIgnoreCase)))
        {
            Entries.Add(entry);
        }
    }

    private void RefreshFilter()
    {
        Entries.Clear();
        foreach (var e in _allEntries)
        {
            if (!_hiddenLevels.Contains(e.Level) &&
                (string.IsNullOrEmpty(_filterSource) ||
                 e.Source.Contains(_filterSource, StringComparison.OrdinalIgnoreCase)))
                Entries.Add(e);
        }
    }

    private static LogEntry ParseLogLine(string raw)
    {
        // Detect [Level] prefix.
        LogLevel level  = LogLevel.Info;
        string   source = "Build";
        string   msg    = raw.Trim();

        if (msg.StartsWith("[Error]",   StringComparison.OrdinalIgnoreCase)) { level = LogLevel.Error;   msg = msg[7..].Trim(); }
        else if (msg.StartsWith("[Warning]", StringComparison.OrdinalIgnoreCase)) { level = LogLevel.Warning; msg = msg[9..].Trim(); }
        else if (msg.StartsWith("[Debug]",   StringComparison.OrdinalIgnoreCase)) { level = LogLevel.Debug;   msg = msg[7..].Trim(); }
        else if (msg.StartsWith("[Info]",    StringComparison.OrdinalIgnoreCase)) { level = LogLevel.Info;    msg = msg[6..].Trim(); }

        // Detect [DEV] prefix from PluginDevSandbox.
        if (msg.StartsWith("[DEV]", StringComparison.OrdinalIgnoreCase)) { source = "DevSandbox"; msg = msg[5..].Trim(); }

        return new LogEntry(DateTime.Now, level, source, msg);
    }
}

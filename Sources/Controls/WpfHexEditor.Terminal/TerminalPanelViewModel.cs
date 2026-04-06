// ==========================================================
// Project: WpfHexEditor.Terminal
// File: TerminalPanelViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-07
// Description:
//     Session orchestrator for the multi-tab Terminal panel.
//     Manages the collection of ShellSessionViewModel instances (one per tab),
//     the active session, macro recording state, and global commands
//     (add/close tab, save output, copy-all, macro start/stop/replay/save).
//     All per-session state (OutputLines, CommandInput, history, process I/O) is
//     delegated to the active ShellSessionViewModel.
//
// Architecture Notes:
//     Pattern: MVVM ViewModel (Orchestrator) + Proxy (delegates properties to ActiveSession).
//     Feature #92: Multi-tab shell sessions + macro recording / replay.
//     Built-in commands (including RecordMacroCommand, ReplayHistoryCommand) are
//     registered once on the shared TerminalCommandRegistry; all sessions share
//     the same command set but have independent histories and processes.
//     The TerminalPanel code-behind subscribes to OutputLines.CollectionChanged;
//     when ActiveSession changes it must re-subscribe to the new OutputLines.
//
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using WpfHexEditor.Core.Terminal;
using WpfHexEditor.Core.Terminal.BuiltInCommands;
using WpfHexEditor.Core.Terminal.Macros;
using WpfHexEditor.Core.Terminal.Scripting;
using WpfHexEditor.Core.Terminal.ShellSession;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Terminal;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Terminal;

/// <summary>
/// Orchestrates multiple <see cref="ShellSessionViewModel"/> tabs for the Terminal panel.
/// Exposes the session collection, active-session switching, macro recording controls,
/// and per-session property proxies consumed by the Terminal XAML.
/// </summary>
public sealed class TerminalPanelViewModel : ViewModelBase, IDisposable
{
    // -- Shared services ----------------------------------------------------------

    private readonly TerminalCommandRegistry _registry = new();
    private readonly ITerminalMacroService   _macroService;
    private readonly ShellSessionManager     _sessionManager = new();
    private readonly IIDEHostContext         _ideHostContext;

    // -- Session collection (bound to TabControl) ---------------------------------

    public ObservableCollection<ShellSessionViewModel> Sessions { get; } = [];

    private ShellSessionViewModel? _activeSession;

    /// <summary>
    /// Currently selected session tab. Changing this raises PropertyChanged for all
    /// proxied per-session properties so the XAML bindings stay in sync.
    /// </summary>
    public ShellSessionViewModel? ActiveSession
    {
        get => _activeSession;
        set
        {
            if (_activeSession == value) return;
            _activeSession = value;
            OnPropertyChanged();
            // Notify all proxied properties so XAML bindings refresh.
            OnPropertyChanged(nameof(OutputLines));
            OnPropertyChanged(nameof(CommandInput));
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(WorkingDirectory));
            OnPropertyChanged(nameof(WorkingDirectoryLabel));
            OnPropertyChanged(nameof(CurrentModeLabel));
            OnPropertyChanged(nameof(IsExternalShellMode));
            OnPropertyChanged(nameof(IsAutoScrollEnabled));
            OnPropertyChanged(nameof(IsWordWrap));
            OnPropertyChanged(nameof(OutputFontSize));
            OnPropertyChanged(nameof(ShowTimestamps));
            OnPropertyChanged(nameof(IsOutputPaused));
            OnPropertyChanged(nameof(IsFindVisible));
            OnPropertyChanged(nameof(CopyOnSelect));
            OnPropertyChanged(nameof(FindText));
            OnPropertyChanged(nameof(FindStatusLabel));
            OnPropertyChanged(nameof(OutputLineCountLabel));
            OnPropertyChanged(nameof(AvailableEncodings));
            OnPropertyChanged(nameof(SelectedEncoding));
            OnPropertyChanged(nameof(HasActiveSession));
            // Refresh per-session command proxies so bound toolbar buttons re-target the new session.
            OnPropertyChanged(nameof(RunCommand));
            OnPropertyChanged(nameof(CancelCommand));
            OnPropertyChanged(nameof(ClearOutputCommand));
            OnPropertyChanged(nameof(ToggleWordWrapCommand));
            OnPropertyChanged(nameof(IncreaseFontCommand));
            OnPropertyChanged(nameof(DecreaseFontCommand));
            OnPropertyChanged(nameof(ToggleTimestampsCommand));
            OnPropertyChanged(nameof(TogglePauseCommand));
            OnPropertyChanged(nameof(ToggleFindCommand));
        }
    }

    public bool HasActiveSession => _activeSession is not null;

    // -- Proxied per-session properties (bound by XAML) ---------------------------

    // The XAML binds to these so it does not need to know about ActiveSession.
    // The code-behind re-subscribes to OutputLines.CollectionChanged on session switch.

    public ObservableCollection<TerminalOutputLine> OutputLines
        => _activeSession?.OutputLines ?? [];

    public string CommandInput
    {
        get => _activeSession?.CommandInput ?? string.Empty;
        set { if (_activeSession is not null) _activeSession.CommandInput = value; }
    }

    public bool IsRunning => _activeSession?.IsRunning ?? false;

    public string WorkingDirectory => _activeSession?.WorkingDirectory ?? string.Empty;
    public string WorkingDirectoryLabel => _activeSession?.WorkingDirectoryLabel ?? string.Empty;
    public string CurrentModeLabel => _activeSession?.CurrentModeLabel ?? "HxTerminal";
    public bool IsExternalShellMode => _activeSession?.IsExternalShellMode ?? false;

    public bool IsAutoScrollEnabled
    {
        get => _activeSession?.IsAutoScrollEnabled ?? true;
        set { if (_activeSession is not null) _activeSession.IsAutoScrollEnabled = value; }
    }

    public bool IsWordWrap
    {
        get => _activeSession?.IsWordWrap ?? true;
        set { if (_activeSession is not null) _activeSession.IsWordWrap = value; }
    }

    public double OutputFontSize
    {
        get => _activeSession?.OutputFontSize ?? 12;
        set { if (_activeSession is not null) _activeSession.OutputFontSize = value; }
    }

    public bool ShowTimestamps
    {
        get => _activeSession?.ShowTimestamps ?? false;
        set { if (_activeSession is not null) _activeSession.ShowTimestamps = value; }
    }

    public bool IsOutputPaused
    {
        get => _activeSession?.IsOutputPaused ?? false;
        set { if (_activeSession is not null) _activeSession.IsOutputPaused = value; }
    }

    public bool IsFindVisible
    {
        get => _activeSession?.IsFindVisible ?? false;
        set { if (_activeSession is not null) _activeSession.IsFindVisible = value; }
    }

    public bool CopyOnSelect
    {
        get => _activeSession?.CopyOnSelect ?? false;
        set { if (_activeSession is not null) _activeSession.CopyOnSelect = value; }
    }

    public string FindText
    {
        get => _activeSession?.FindText ?? string.Empty;
        set { if (_activeSession is not null) _activeSession.FindText = value; }
    }

    public string FindStatusLabel
    {
        get => _activeSession?.FindStatusLabel ?? string.Empty;
        set { if (_activeSession is not null) _activeSession.FindStatusLabel = value; }
    }

    public string OutputLineCountLabel => _activeSession?.OutputLineCountLabel ?? "0 lines";

    public ObservableCollection<string> AvailableEncodings
        => _activeSession?.AvailableEncodings ?? [];

    public string SelectedEncoding
    {
        get => _activeSession?.SelectedEncoding ?? "UTF-8";
        set { if (_activeSession is not null) _activeSession.SelectedEncoding = value; }
    }

    // -- Macro state --------------------------------------------------------------

    private bool _isRecording;
    public bool IsRecording
    {
        get => _isRecording;
        private set { _isRecording = value; OnPropertyChanged(); }
    }

    private MacroSession? _lastRecordedMacro;

    // -- Global commands (tab management + macros) --------------------------------

    public ICommand AddHxTerminalCommand  { get; }
    public ICommand AddPowerShellCommand  { get; }
    public ICommand AddBashCommand        { get; }
    public ICommand AddCmdCommand         { get; }
    public ICommand CloseSessionCommand   { get; }
    public ICommand SaveOutputCommand     { get; }
    public ICommand CopyAllCommand        { get; }

    // Macro toolbar
    public ICommand StartRecordingCommand { get; }
    public ICommand StopRecordingCommand  { get; }
    public ICommand ReplayMacroCommand    { get; }
    public ICommand SaveMacroCommand      { get; }

    // Per-session commands (delegated to ActiveSession) ---------------------------

    public ICommand RunCommand             => _activeSession?.RunCommand    ?? NullCommand;
    public ICommand CancelCommand          => _activeSession?.CancelCommand ?? NullCommand;
    public ICommand ClearOutputCommand     => _activeSession?.ClearOutputCommand  ?? NullCommand;
    public ICommand ToggleWordWrapCommand  => _activeSession?.ToggleWordWrapCommand ?? NullCommand;
    public ICommand IncreaseFontCommand    => _activeSession?.IncreaseFontCommand   ?? NullCommand;
    public ICommand DecreaseFontCommand    => _activeSession?.DecreaseFontCommand   ?? NullCommand;
    public ICommand ToggleTimestampsCommand => _activeSession?.ToggleTimestampsCommand ?? NullCommand;
    public ICommand TogglePauseCommand     => _activeSession?.TogglePauseCommand  ?? NullCommand;
    public ICommand ToggleFindCommand      => _activeSession?.ToggleFindCommand   ?? NullCommand;

    // Switch-mode command: creates a new session of the selected shell type.
    // In a multi-tab design, existing sessions cannot change their shell type
    // (the process is already running), so the correct behavior is to open a new tab.
    public ICommand SwitchModeCommand => new RelayCommand(p =>
    {
        var shellType = p as string switch
        {
            "PowerShell" => TerminalShellType.PowerShell,
            "Bash"       => TerminalShellType.Bash,
            "CMD"        => TerminalShellType.Cmd,
            _            => TerminalShellType.HxTerminal
        };
        CreateSession(shellType);
    });

    // -- Constructor --------------------------------------------------------------

    public TerminalPanelViewModel(IIDEHostContext hostContext)
    {
        _ideHostContext = hostContext ?? throw new ArgumentNullException(nameof(hostContext));

        _macroService = new TerminalMacroService(_registry);
        _macroService.RecordingStateChanged += (_, recording) => IsRecording = recording;

        var scriptEngine = new HxScriptEngine(_registry);
        RegisterBuiltIns(scriptEngine);

        AddHxTerminalCommand  = new RelayCommand(_ => CreateSession(TerminalShellType.HxTerminal));
        AddPowerShellCommand  = new RelayCommand(_ => CreateSession(TerminalShellType.PowerShell));
        AddBashCommand        = new RelayCommand(_ => CreateSession(TerminalShellType.Bash));
        AddCmdCommand         = new RelayCommand(_ => CreateSession(TerminalShellType.Cmd));
        CloseSessionCommand   = new RelayCommand(
            p => CloseSession(p is ShellSessionViewModel vm ? vm.Session.Id : _activeSession?.Session.Id ?? Guid.Empty),
            _ => Sessions.Count > 1);
        SaveOutputCommand     = new RelayCommand(_ => SaveOutput());
        CopyAllCommand        = new RelayCommand(_ => CopyAll());

        StartRecordingCommand = new RelayCommand(_ => _macroService.StartRecording(),  _ => !IsRecording);
        StopRecordingCommand  = new RelayCommand(_ => _lastRecordedMacro = _macroService.StopRecording(), _ => IsRecording);
        ReplayMacroCommand    = new RelayCommand(_ => _ = ReplayLastMacroAsync(),
            _ => _lastRecordedMacro is { IsEmpty: false } && !IsRecording);
        SaveMacroCommand      = new RelayCommand(_ => SaveMacroAsHxScript(),
            _ => _lastRecordedMacro is { IsEmpty: false });

        // Create the initial HxTerminal session.
        CreateSession(TerminalShellType.HxTerminal);
    }

    // -- Public API (SDK bridge) --------------------------------------------------

    /// <summary>Returns the active session's ITerminalOutput sink (for TerminalServiceImpl).</summary>
    public ITerminalOutput? GetActiveOutput() => _activeSession;

    /// <summary>Exposes the session manager so TerminalServiceImpl can route OpenSession calls.</summary>
    public IShellSessionManager SessionManager => _sessionManager;

    /// <summary>
    /// Exposes the shared command registry so MainWindow can wire it to
    /// <see cref="WpfHexEditor.App.Services.TerminalServiceImpl.SetRegistry"/>,
    /// enabling plugins to register custom HxTerminal commands.
    /// </summary>
    public TerminalCommandRegistry CommandRegistry => _registry;

    /// <summary>Opens a new session of the specified shell type (called by TerminalServiceImpl).</summary>
    public void OpenSession(TerminalShellType shellType) => CreateSession(shellType);

    /// <summary>Closes the active session if more than one exists.</summary>
    public void CloseActiveSession()
    {
        if (_activeSession is not null && Sessions.Count > 1)
            CloseSession(_activeSession.Session.Id);
    }

    // -- History navigation (called by code-behind keyboard handler) --------------

    public void NavigateHistoryUp()   => _activeSession?.NavigateHistoryUp();
    public void NavigateHistoryDown() => _activeSession?.NavigateHistoryDown();
    public void CycleCompletion()     => _activeSession?.CycleCompletion();

    // -- Session management -------------------------------------------------------

    private void CreateSession(TerminalShellType shellType)
    {
        var coreSession = _sessionManager.CreateSession(shellType);
        var vm = new ShellSessionViewModel(coreSession, _registry, _ideHostContext, _macroService);

        // Forward per-session property changes up so XAML bindings on this VM update.
        vm.PropertyChanged += OnActiveSessionPropertyChanged;

        Sessions.Add(vm);
        ActiveSession = vm;
    }

    private void CloseSession(Guid sessionId)
    {
        if (Sessions.Count <= 1) return;

        var vm = Sessions.FirstOrDefault(s => s.Session.Id == sessionId);
        if (vm is null) return;

        var idx = Sessions.IndexOf(vm);
        vm.PropertyChanged -= OnActiveSessionPropertyChanged;
        Sessions.Remove(vm);

        if (ActiveSession == vm)
            ActiveSession = Sessions.Count > 0 ? Sessions[Math.Max(0, idx - 1)] : null;

        _sessionManager.CloseSession(sessionId);
        vm.Dispose();
    }

    private void OnActiveSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Only propagate from the currently active session.
        if (sender != _activeSession) return;
        OnPropertyChanged(e.PropertyName);
    }

    // -- Macro helpers ------------------------------------------------------------

    private async Task ReplayLastMacroAsync()
    {
        if (_lastRecordedMacro is null || _activeSession is null) return;

        await _macroService.ReplayAsync(
            _lastRecordedMacro,
            _activeSession,
            _activeSession,
            CancellationToken.None).ConfigureAwait(false);
    }

    private void SaveMacroAsHxScript()
    {
        if (_lastRecordedMacro is null || _lastRecordedMacro.IsEmpty) return;

        var dlg = new SaveFileDialog
        {
            Filter      = "HxScript|*.hxscript|All Files|*.*",
            FileName    = "macro",
            DefaultExt  = "hxscript",
            FilterIndex = 1
        };

        if (dlg.ShowDialog() != true) return;

        var source = _macroService.ExportToHxScript(_lastRecordedMacro);
        File.WriteAllText(dlg.FileName, source, System.Text.Encoding.UTF8);
    }

    // -- Output helpers (save / copy all) -----------------------------------------

    private void SaveOutput()
    {
        if (_activeSession is null) return;

        var dlg = new SaveFileDialog
        {
            Filter      = "Plain Text|*.txt|HTML|*.html|RTF / Word|*.rtf|ANSI Text|*.ansi|Markdown|*.md|Excel / LibreOffice Calc|*.xml|All Files|*.*",
            FileName    = "terminal-output",
            DefaultExt  = "txt",
            FilterIndex = 1
        };

        if (dlg.ShowDialog() != true) return;

        var lines = (IReadOnlyList<TerminalOutputLine>)_activeSession.OutputLines;
        var ts    = _activeSession.ShowTimestamps;

        var content = dlg.FilterIndex switch
        {
            2 => TerminalExportService.ToHtml         (lines, ts),
            3 => TerminalExportService.ToRtf          (lines, ts),
            4 => TerminalExportService.ToAnsi         (lines, ts),
            5 => TerminalExportService.ToMarkdown     (lines, ts),
            6 => TerminalExportService.ToSpreadsheetMl(lines, ts),
            _ => TerminalExportService.ToPlainText    (lines, ts)
        };

        File.WriteAllText(dlg.FileName, content, System.Text.Encoding.UTF8);
    }

    private void CopyAll()
    {
        if (_activeSession is null) return;

        var ts = _activeSession.ShowTimestamps;
        var text = string.Join(
            Environment.NewLine,
            _activeSession.OutputLines.Select(l =>
                ts ? $"[{l.Timestamp:HH:mm:ss}] {l.Text}" : l.Text));

        if (!string.IsNullOrEmpty(text))
            Clipboard.SetText(text);
    }

    // -- Built-in command registration -------------------------------------------

    private void RegisterBuiltIns(HxScriptEngine engine)
    {
        _registry.Register(new HelpCommand(_registry));
        _registry.Register(new ClearCommand());
        _registry.Register(new EchoCommand());
        _registry.Register(new VersionCommand());
        _registry.Register(new ExitCommand());
        _registry.Register(new PluginListCommand());
        _registry.Register(new PluginReloadCommand());
        _registry.Register(new StatusCommand());
        _registry.Register(new SendOutputCommand());
        _registry.Register(new SendErrorCommand());
        _registry.Register(new RunScriptCommand(engine));
        _registry.Register(new OpenFileCommand());
        _registry.Register(new ListOpenFilesCommand());
        _registry.Register(new ReadHexCommand());
        _registry.Register(new SearchCommand());
        _registry.Register(new SaveFileCommand());
        _registry.Register(new SaveAsCommand());
        _registry.Register(new CloseFileCommand());
        _registry.Register(new OpenFolderCommand());
        _registry.Register(new OpenProjectCommand());
        _registry.Register(new CloseProjectCommand());
        _registry.Register(new OpenSolutionCommand());
        _registry.Register(new CloseSolutionCommand());
        _registry.Register(new ReloadSolutionCommand());
        _registry.Register(new ListFilesCommand());
        _registry.Register(new SelectFileCommand());
        _registry.Register(new CopyFileCommand());
        _registry.Register(new DeleteFileCommand());
        _registry.Register(new OpenPanelCommand());
        _registry.Register(new ClosePanelCommand());
        _registry.Register(new TogglePanelCommand());
        _registry.Register(new FocusPanelCommand());
        _registry.Register(new ClearPanelCommand());
        _registry.Register(new AppendPanelCommand());
        _registry.Register(new ShowLogsCommand());
        _registry.Register(new ShowErrorsCommand());
        _registry.Register(new WriteHexCommand());
        _registry.Register(new RunPluginCommand());

        // Feature #92 â€” macro commands
        _registry.Register(new RecordMacroCommand(_macroService));
        _registry.Register(new ReplayHistoryCommand(
            _macroService,
            () => _activeSession?.Session.History.GetAll() ?? []));

        // Build system commands
        _registry.Register(new BuildCommand());
        _registry.Register(new RebuildCommand());
        _registry.Register(new CleanCommand());
        _registry.Register(new BuildDirtyCommand());
        _registry.Register(new BuildCancelCommand());
        _registry.Register(new BuildStatusCommand());

        // Unit test commands
        _registry.Register(new TestRunCommand());
        _registry.Register(new TestRunProjectCommand());
        _registry.Register(new TestRunFilterCommand());
        _registry.Register(new TestStatusCommand());

        // C# scripting
        _registry.Register(new RunCsharpCommand());

        // Diff commands
        _registry.Register(new DiffCommand());
        _registry.Register(new DiffOpenCommand());

        // Debugger commands
        _registry.Register(new DebugStatusCommand());
        _registry.Register(new DebugContinueCommand());
        _registry.Register(new DebugBreakpointCommand());
    }

    // -- INotifyPropertyChanged ---------------------------------------------------



    // -- IDisposable --------------------------------------------------------------

    public void Dispose()
    {
        foreach (var vm in Sessions)
        {
            vm.PropertyChanged -= OnActiveSessionPropertyChanged;
            vm.Dispose();
        }

        Sessions.Clear();
        _sessionManager.Dispose();
    }

    // -- Helpers ------------------------------------------------------------------

    private static readonly ICommand NullCommand =
        new RelayCommand(_ => { }, _ => false);

    private sealed class RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add    => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
        public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => execute(parameter);
    }
}

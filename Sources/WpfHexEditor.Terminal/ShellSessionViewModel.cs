// ==========================================================
// Project: WpfHexEditor.Terminal
// File: ShellSessionViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-08
// Description:
//     ViewModel for a single terminal session tab.
//     Wraps ShellSession (Core model), owns the ObservableCollection of output
//     lines for WPF binding, and drives external shell process I/O.
//     Implements ITerminalContext and ITerminalOutput so it can be passed
//     directly to built-in command providers.
//
// Architecture Notes:
//     Pattern: MVVM ViewModel + Adapter (ITerminalContext / ITerminalOutput).
//     Feature #92: Multi-tab shell sessions.
//     Each tab has its own history, working directory, and process state.
//     Shared resources (TerminalCommandRegistry, IIDEHostContext) are injected.
//
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using WpfHexEditor.Core.Terminal;
using WpfHexEditor.Core.Terminal.Macros;
using WpfHexEditor.Core.Terminal.ShellSession;
using WpfHexEditor.Core.Events.IDEEvents;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Focus;

namespace WpfHexEditor.Terminal;

/// <summary>
/// ViewModel for one terminal session tab.
/// Manages command dispatch, external shell process I/O, output rendering,
/// and per-tab UX state (auto-scroll, pause, find, history navigation).
/// </summary>
public sealed class ShellSessionViewModel : INotifyPropertyChanged, IDisposable,
                                            ITerminalContext, ITerminalOutput
{
    // -- Core model ---------------------------------------------------------------

    public ShellSession Session { get; }

    // -- Shared services (injected) -----------------------------------------------

    private readonly TerminalCommandRegistry _registry;
    private readonly IIDEHostContext _ideHostContext;
    private readonly ITerminalMacroService _macroService;

    // -- Process state (external shells) -----------------------------------------

    private CancellationTokenSource? _cts;
    private StreamWriter? _shellInput;

    // -- Pause buffer -------------------------------------------------------------

    private readonly Queue<TerminalOutputLine> _pauseBuffer = new();

    // -- Tab completion -----------------------------------------------------------

    private string _completionPrefix = string.Empty;
    private List<string> _completions = [];
    private int _completionIndex = -1;

    // -- Output (bound to TerminalPanel RichTextBox) ------------------------------

    private const int MaxOutputLines = 5000;

    public ObservableCollection<TerminalOutputLine> OutputLines { get; } = [];

    // -- ITerminalContext implementation ------------------------------------------

    public IIDEHostContext IDE => _ideHostContext;
    public IDocument? ActiveDocument => _ideHostContext.FocusContext.ActiveDocument;
    public IPanel? ActivePanel => _ideHostContext.FocusContext.ActivePanel;

    private string _workingDirectory;
    public string WorkingDirectory
    {
        get => _workingDirectory;
        set
        {
            _workingDirectory = value;
            Session.WorkingDirectory = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(WorkingDirectoryLabel));
        }
    }

    public string WorkingDirectoryLabel
    {
        get
        {
            var dir = _workingDirectory;
            if (dir.Length <= 38) return dir;
            var parts = dir.TrimEnd('\\', '/').Split(['\\', '/']);
            return parts.Length >= 3
                ? parts[0] + "\\…\\" + string.Join('\\', parts[^2..])
                : "…" + dir[^36..];
        }
    }

    // -- Tab header state ---------------------------------------------------------

    public string TabTitle => Session.TabTitle;

    public string ShellIcon => Session.ShellType switch
    {
        TerminalShellType.PowerShell => "\uE756",  // PowerShell glyph
        TerminalShellType.Bash       => "\uE756",  // Terminal glyph
        TerminalShellType.Cmd        => "\uE756",  // Terminal glyph
        _                            => "\uE8A7"   // Code glyph for HxTerminal
    };

    // -- Per-tab UX state ---------------------------------------------------------

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        private set { _isRunning = value; OnPropertyChanged(); }
    }

    private bool _isAutoScrollEnabled = true;
    public bool IsAutoScrollEnabled
    {
        get => _isAutoScrollEnabled;
        set { _isAutoScrollEnabled = value; OnPropertyChanged(); }
    }

    private bool _isWordWrap = true;
    public bool IsWordWrap
    {
        get => _isWordWrap;
        set { _isWordWrap = value; OnPropertyChanged(); }
    }

    private double _outputFontSize = 12;
    public double OutputFontSize
    {
        get => _outputFontSize;
        set { _outputFontSize = Math.Clamp(value, 8, 28); OnPropertyChanged(); }
    }

    private bool _showTimestamps;
    public bool ShowTimestamps
    {
        get => _showTimestamps;
        set { _showTimestamps = value; OnPropertyChanged(); }
    }

    private bool _isOutputPaused;
    public bool IsOutputPaused
    {
        get => _isOutputPaused;
        set { _isOutputPaused = value; OnPropertyChanged(); }
    }

    private bool _isFindVisible;
    public bool IsFindVisible
    {
        get => _isFindVisible;
        set { _isFindVisible = value; OnPropertyChanged(); }
    }

    private bool _copyOnSelect;
    public bool CopyOnSelect
    {
        get => _copyOnSelect;
        set { _copyOnSelect = value; OnPropertyChanged(); }
    }

    private string _findText = string.Empty;
    public string FindText
    {
        get => _findText;
        set { _findText = value; OnPropertyChanged(); }
    }

    private string _findStatusLabel = string.Empty;
    public string FindStatusLabel
    {
        get => _findStatusLabel;
        set { _findStatusLabel = value; OnPropertyChanged(); }
    }

    // -- Command input ------------------------------------------------------------

    private string _commandInput = string.Empty;
    public string CommandInput
    {
        get => _commandInput;
        set { _commandInput = value; OnPropertyChanged(); ResetCompletion(); }
    }

    /// <summary>Shows the number of output lines in the input row status area.</summary>
    public string OutputLineCountLabel => $"{OutputLines.Count} lines";

    // -- Shell mode label ---------------------------------------------------------

    public string CurrentModeLabel => Session.ShellType switch
    {
        TerminalShellType.PowerShell => "PowerShell",
        TerminalShellType.Bash       => "Bash",
        TerminalShellType.Cmd        => "CMD",
        _                            => "HxTerminal"
    };

    public bool IsExternalShellMode => Session.ShellType != TerminalShellType.HxTerminal;

    // -- Encoding (external shells) -----------------------------------------------

    public ObservableCollection<string> AvailableEncodings { get; } = ["UTF-8", "Windows-1252", "ASCII"];

    private string _selectedEncoding = "UTF-8";
    public string SelectedEncoding
    {
        get => _selectedEncoding;
        set { _selectedEncoding = value; OnPropertyChanged(); }
    }

    // -- Commands -----------------------------------------------------------------

    public ICommand RunCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ClearOutputCommand { get; }
    public ICommand ToggleWordWrapCommand { get; }
    public ICommand IncreaseFontCommand { get; }
    public ICommand DecreaseFontCommand { get; }
    public ICommand ToggleTimestampsCommand { get; }
    public ICommand TogglePauseCommand { get; }
    public ICommand ToggleFindCommand { get; }

    // -- Constructor --------------------------------------------------------------

    public ShellSessionViewModel(
        ShellSession session,
        TerminalCommandRegistry registry,
        IIDEHostContext ideHostContext,
        ITerminalMacroService macroService)
    {
        Session          = session          ?? throw new ArgumentNullException(nameof(session));
        _registry        = registry        ?? throw new ArgumentNullException(nameof(registry));
        _ideHostContext  = ideHostContext  ?? throw new ArgumentNullException(nameof(ideHostContext));
        _macroService    = macroService    ?? throw new ArgumentNullException(nameof(macroService));

        _workingDirectory = session.WorkingDirectory;

        RunCommand             = new RelayCommand(_ => _ = ExecuteInputAsync(), _ => !IsRunning);
        CancelCommand          = new RelayCommand(_ => CancelCurrentOperation(), _ => IsRunning || Session.IsExternalShellRunning);
        ClearOutputCommand     = new RelayCommand(_ => ClearOutput());
        ToggleWordWrapCommand  = new RelayCommand(_ => IsWordWrap = !IsWordWrap);
        IncreaseFontCommand    = new RelayCommand(_ => OutputFontSize++);
        DecreaseFontCommand    = new RelayCommand(_ => OutputFontSize--);
        ToggleTimestampsCommand = new RelayCommand(_ => ShowTimestamps = !ShowTimestamps);
        TogglePauseCommand     = new RelayCommand(_ => TogglePause());
        ToggleFindCommand      = new RelayCommand(_ => IsFindVisible = !IsFindVisible);

        // Start external shell immediately if not HxTerminal.
        if (session.ShellType != TerminalShellType.HxTerminal)
            _ = StartExternalShellAsync();
    }

    // -- Public API ---------------------------------------------------------------

    public void NavigateHistoryUp()
    {
        var entry = Session.History.NavigatePrevious();
        if (entry is not null) CommandInput = entry;
    }

    public void NavigateHistoryDown()
    {
        var entry = Session.History.NavigateNext();
        if (entry is not null) CommandInput = entry;
    }

    public void CycleCompletion()
    {
        if (Session.ShellType != TerminalShellType.HxTerminal) return;

        var input = CommandInput;
        if (input != _completionPrefix || _completions.Count == 0)
        {
            _completionPrefix = input;
            _completions = [.. _registry.GetCompletions(input)];
            _completionIndex = -1;
        }

        if (_completions.Count == 0) return;

        _completionIndex = (_completionIndex + 1) % _completions.Count;
        _commandInput = _completions[_completionIndex];
        OnPropertyChanged(nameof(CommandInput));
    }

    // -- ITerminalOutput ----------------------------------------------------------

    public void Write(string text)       => AppendLine(text, TerminalOutputKind.Standard);
    public void WriteLine(string text = "") => AppendLine(text, TerminalOutputKind.Standard);
    public void WriteError(string text)  => AppendLine(text, TerminalOutputKind.Error);
    public void WriteWarning(string text)=> AppendLine(text, TerminalOutputKind.Warning);
    public void WriteInfo(string text)   => AppendLine(text, TerminalOutputKind.Info);
    public void Clear() => Application.Current?.Dispatcher.InvokeAsync(ClearOutput);

    // -- External shell process ---------------------------------------------------

    private async Task StartExternalShellAsync()
    {
        var (exe, args) = ResolveShell(Session.ShellType);
        if (exe is null)
        {
            WriteError($"Shell not found for {Session.ShellType}. Falling back to HxTerminal input.");
            return;
        }

        var encoding = ParseEncoding(SelectedEncoding);
        var psi = new ProcessStartInfo(exe)
        {
            Arguments              = args,
            UseShellExecute        = false,
            CreateNoWindow         = true,
            RedirectStandardInput  = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            StandardOutputEncoding = encoding,
            StandardErrorEncoding  = encoding,
        };

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.Exited += OnProcessExited;

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            WriteError($"Failed to start {Session.ShellType}: {ex.Message}");
            process.Dispose();
            return;
        }

        Session.ShellProcess = process;
        Session.ShellInput   = process.StandardInput;
        _shellInput          = process.StandardInput;

        _ = PipeReaderAsync(process.StandardOutput, TerminalOutputKind.Standard);
        _ = PipeReaderAsync(process.StandardError,  TerminalOutputKind.Error);

        WriteInfo($"[{Session.ShellType} started: {exe}]");
        await Task.CompletedTask;
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        CleanupProcess();
        AppendLine($"[{Session.ShellType} process exited]", TerminalOutputKind.Warning);
    }

    private void CleanupProcess()
    {
        if (Session.ShellProcess is not null)
        {
            Session.ShellProcess.Exited -= OnProcessExited;
            Session.ShellProcess.Dispose();
            Session.ShellProcess = null;
        }
        _shellInput?.Dispose();
        _shellInput = null;
        Session.ShellInput = null;
    }

    private async Task PipeReaderAsync(StreamReader reader, TerminalOutputKind kind)
    {
        try
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
                AppendLine(line, kind);
        }
        catch { /* stream closed when process exits */ }
    }

    // -- Command execution --------------------------------------------------------

    private async Task ExecuteInputAsync()
    {
        var input = CommandInput.Trim();
        if (string.IsNullOrEmpty(input)) return;

        Session.History.Push(input);
        CommandInput = string.Empty;
        AppendLine($"> {input}", TerminalOutputKind.Info);

        // Publish IDE event so plugins can observe terminal commands.
        _ideHostContext.IDEEvents.Publish(new TerminalCommandExecutedEvent
        {
            Source    = "TerminalPanel",
            Command   = input,
            ShellType = Session.ShellType.ToString(),
        });

        // External shell: forward to process stdin.
        if (Session.ShellType != TerminalShellType.HxTerminal && _shellInput is not null)
        {
            try
            {
                await _shellInput.WriteLineAsync(input).ConfigureAwait(false);
                await _shellInput.FlushAsync().ConfigureAwait(false);
            }
            catch (Exception ex) { WriteError($"Shell I/O error: {ex.Message}"); }
            return;
        }

        // HxTerminal: built-in registry dispatch.
        var parts = Tokenize(input);
        if (parts.Length == 0) return;

        _cts = new CancellationTokenSource();
        IsRunning = true;

        try
        {
            var cmd = _registry.FindCommand(parts[0]);
            if (cmd is null)
            {
                WriteError($"Unknown command: {parts[0]}. Type 'help' for a list of commands.");
                return;
            }

            // Notify macro recorder before execution.
            _registry.RaiseCommandExecuted(input);

            await cmd.ExecuteAsync(parts[1..], this, this, _cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            AppendLine("[Cancelled]", TerminalOutputKind.Warning);
        }
        catch (Exception ex)
        {
            WriteError($"Error: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void CancelCurrentOperation()
    {
        if (Session.IsExternalShellRunning)
        {
            try { Session.ShellProcess!.Kill(entireProcessTree: false); } catch { /* ignore */ }
        }
        else
        {
            _cts?.Cancel();
        }
    }

    // -- UX helpers ---------------------------------------------------------------

    private void TogglePause()
    {
        IsOutputPaused = !IsOutputPaused;

        if (!IsOutputPaused)
        {
            while (_pauseBuffer.TryDequeue(out var buffered))
                AddLineAndTrim(buffered);
        }
    }

    private void ClearOutput()
    {
        OutputLines.Clear();
        OnPropertyChanged(nameof(OutputLineCountLabel));
    }

    private void AppendLine(string text, TerminalOutputKind kind)
    {
        var line = new TerminalOutputLine(text, kind);

        if (IsOutputPaused)
        {
            _pauseBuffer.Enqueue(line);
            return;
        }

        if (Application.Current?.Dispatcher.CheckAccess() == true)
            AddLineAndTrim(line);
        else
            Application.Current?.Dispatcher.InvokeAsync(() => AddLineAndTrim(line));
    }

    private void AddLineAndTrim(TerminalOutputLine line)
    {
        while (OutputLines.Count >= MaxOutputLines)
            OutputLines.RemoveAt(0);

        OutputLines.Add(line);
        OnPropertyChanged(nameof(OutputLineCountLabel));
    }

    private void ResetCompletion()
    {
        _completions = [];
        _completionIndex = -1;
        _completionPrefix = string.Empty;
    }

    // -- Static helpers -----------------------------------------------------------

    private static (string? exe, string args) ResolveShell(TerminalShellType shellType) =>
        shellType switch
        {
            TerminalShellType.PowerShell =>
                (SearchInPath("pwsh.exe") ?? SearchInPath("powershell.exe"), "-NoLogo -NoExit"),
            TerminalShellType.Bash =>
                (SearchInPath("bash.exe") ?? SearchInPath("wsl.exe"), string.Empty),
            TerminalShellType.Cmd =>
                (SearchInPath("cmd.exe") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"), "/k"),
            _ => (null, string.Empty)
        };

    private static string? SearchInPath(string fileName)
    {
        if (File.Exists(fileName)) return Path.GetFullPath(fileName);
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        foreach (var dir in paths)
        {
            var full = Path.Combine(dir.Trim(), fileName);
            if (File.Exists(full)) return full;
        }
        return null;
    }

    private static Encoding ParseEncoding(string name) => name switch
    {
        "Windows-1252" => Encoding.GetEncoding(1252),
        "ASCII"        => Encoding.ASCII,
        _              => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
    };

    private static string[] Tokenize(string input)
    {
        var args = new List<string>();
        bool inQuote = false;
        var current = new StringBuilder();
        foreach (var ch in input)
        {
            if (ch == '"') { inQuote = !inQuote; continue; }
            if (ch == ' ' && !inQuote)
            {
                if (current.Length > 0) { args.Add(current.ToString()); current.Clear(); }
                continue;
            }
            current.Append(ch);
        }
        if (current.Length > 0) args.Add(current.ToString());
        return [.. args];
    }

    // -- INotifyPropertyChanged ---------------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // -- IDisposable --------------------------------------------------------------

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        CleanupProcess();
        Session.Dispose();
    }

    // -- RelayCommand (local to this VM) ------------------------------------------

    private sealed class RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => execute(parameter);
    }
}

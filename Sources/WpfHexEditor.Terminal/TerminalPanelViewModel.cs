//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;
using WpfHexEditor.Core.Terminal;
using WpfHexEditor.Core.Terminal.BuiltInCommands;
using WpfHexEditor.Core.Terminal.Scripting;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Focus;

namespace WpfHexEditor.Terminal;

/// <summary>
/// ViewModel for the Terminal dockable panel.
/// Manages command input, output lines, history navigation, and execution lifecycle.
/// </summary>
public sealed class TerminalPanelViewModel : INotifyPropertyChanged, IDisposable, ITerminalContext, ITerminalOutput
{
    // -- Core services ------------------------------------------------------------

    private readonly TerminalCommandRegistry _registry = new();
    private readonly CommandHistory _history = new();
    private CancellationTokenSource? _cts;

    // -- ITerminalContext ----------------------------------------------------------

    public IIDEHostContext IDE { get; }
    public IDocument? ActiveDocument => IDE.FocusContext.ActiveDocument;
    public IPanel? ActivePanel => IDE.FocusContext.ActivePanel;
    public string WorkingDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // -- Observable state ---------------------------------------------------------

    public ObservableCollection<TerminalOutputLine> OutputLines { get; } = [];

    private string _commandInput = string.Empty;
    public string CommandInput
    {
        get => _commandInput;
        set { _commandInput = value; OnPropertyChanged(); }
    }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        private set { _isRunning = value; OnPropertyChanged(); }
    }

    // -- Commands -----------------------------------------------------------------

    public ICommand RunCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ClearOutputCommand { get; }

    // -- Constructor --------------------------------------------------------------

    public TerminalPanelViewModel(IIDEHostContext hostContext)
    {
        IDE = hostContext ?? throw new ArgumentNullException(nameof(hostContext));

        var engine = new HxScriptEngine(_registry);

        RunCommand         = new RelayCommand(_ => _ = ExecuteInputAsync(), _ => !IsRunning);
        CancelCommand      = new RelayCommand(_ => _cts?.Cancel(), _ => IsRunning);
        ClearOutputCommand = new RelayCommand(_ => OutputLines.Clear());

        RegisterBuiltIns(engine);
        _ = _history.LoadAsync();
    }

    // -- Public API: register plugin commands -------------------------------------

    public void RegisterCommand(ITerminalCommandProvider command) => _registry.Register(command);
    public void UnregisterCommand(string commandName) => _registry.Unregister(commandName);

    // -- Keyboard navigation -------------------------------------------------------

    public void NavigateHistoryUp()
    {
        var entry = _history.NavigatePrevious();
        if (entry is not null) CommandInput = entry;
    }

    public void NavigateHistoryDown()
    {
        var entry = _history.NavigateNext();
        if (entry is not null) CommandInput = entry;
    }

    // -- ITerminalOutput -----------------------------------------------------------

    public void Write(string text) => AppendLine(text, TerminalOutputKind.Standard);
    public void WriteLine(string text = "") => AppendLine(text, TerminalOutputKind.Standard);
    public void WriteError(string text)   => AppendLine(text, TerminalOutputKind.Error);
    public void WriteWarning(string text) => AppendLine(text, TerminalOutputKind.Warning);
    public void Clear() => System.Windows.Application.Current?.Dispatcher.InvokeAsync(OutputLines.Clear);

    // -- Private helpers -----------------------------------------------------------

    private async Task ExecuteInputAsync()
    {
        var input = CommandInput.Trim();
        if (string.IsNullOrEmpty(input)) return;

        _history.Push(input);
        CommandInput = string.Empty;
        AppendLine($"> {input}", TerminalOutputKind.Info);

        var parts = TokenizeInput(input);
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

    private void AppendLine(string text, TerminalOutputKind kind)
    {
        var line = new TerminalOutputLine(text, kind);
        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
            OutputLines.Add(line);
        else
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => OutputLines.Add(line));
    }

    private static string[] TokenizeInput(string input)
    {
        var args = new List<string>();
        bool inQuote = false;
        var current = new System.Text.StringBuilder();
        foreach (var ch in input)
        {
            if (ch == '"') { inQuote = !inQuote; continue; }
            if (ch == ' ' && !inQuote) { if (current.Length > 0) { args.Add(current.ToString()); current.Clear(); } continue; }
            current.Append(ch);
        }
        if (current.Length > 0) args.Add(current.ToString());
        return [.. args];
    }

    private void RegisterBuiltIns(HxScriptEngine engine)
    {
        _registry.Register(new HelpCommand(_registry));
        _registry.Register(new ClearCommand());
        _registry.Register(new EchoCommand());
        _registry.Register(new VersionCommand());
        _registry.Register(new ExitCommand());
        _registry.Register(new PluginListCommand());
        _registry.Register(new StatusCommand());
        _registry.Register(new HistoryCommand(_history));
        _registry.Register(new SendOutputCommand());
        _registry.Register(new SendErrorCommand());
        _registry.Register(new RunScriptCommand(engine));
        _registry.Register(new OpenFileCommand());
        _registry.Register(new ListOpenFilesCommand());
        _registry.Register(new ReadHexCommand());
        _registry.Register(new SearchCommand());

        // File management
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

        // Panel management
        _registry.Register(new OpenPanelCommand());
        _registry.Register(new ClosePanelCommand());
        _registry.Register(new TogglePanelCommand());
        _registry.Register(new FocusPanelCommand());
        _registry.Register(new ClearPanelCommand());
        _registry.Register(new AppendPanelCommand());

        // Output / errors
        _registry.Register(new ShowLogsCommand());
        _registry.Register(new ShowErrorsCommand());

        // Hex editing
        _registry.Register(new WriteHexCommand());

        // Plugins
        _registry.Register(new RunPluginCommand());
    }

    // -- INotifyPropertyChanged ----------------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // -- IDisposable ---------------------------------------------------------------

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _ = _history.SaveAsync();
    }

    // -- RelayCommand --------------------------------------------------------------

    private sealed class RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => execute(parameter);
    }
}

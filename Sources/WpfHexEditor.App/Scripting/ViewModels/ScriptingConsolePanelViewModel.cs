// Project      : WpfHexEditor.App
// File         : Scripting/ViewModels/ScriptingConsolePanelViewModel.cs
// Description  : VM for the Scripting Console panel.
// Architecture : Delegates execution to IScriptingService; history delegated to
//                CommandHistory (Core.Terminal); output capped at MaxOutputLines.

using System.Collections.ObjectModel;
using WpfHexEditor.Core.Terminal;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Scripting.ViewModels;

public sealed class ScriptingConsolePanelViewModel : ViewModelBase
{
    private const int MaxOutputLines = 2000;

    private readonly IScriptingService? _scripting;
    private readonly CommandHistory     _history = new();
    private string                      _code    = string.Empty;
    private bool                        _isBusy;
    private CancellationTokenSource?    _cts;
    private readonly object             _ctsLock = new();

    public ObservableCollection<OutputEntry> Output { get; } = [];

    public string Code
    {
        get => _code;
        set { _code = value; OnPropertyChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set { _isBusy = value; OnPropertyChanged(); }
    }

    public ScriptingConsolePanelViewModel(IScriptingService? scripting)
    {
        _scripting = scripting;
    }

    public async Task RunAsync()
    {
        var code = _code.Trim();
        if (string.IsNullOrEmpty(code) || _scripting is null) return;

        _history.Push(code);

        CancellationTokenSource cts;
        lock (_ctsLock)
        {
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            cts  = _cts;
        }

        IsBusy = true;
        try
        {
            var result = await _scripting.RunAsync(code, cts.Token).ConfigureAwait(true);

            if (!string.IsNullOrEmpty(result.Output))
                Append(result.Output, isError: false);

            foreach (var diag in result.Diagnostics)
                Append($"{(diag.IsWarning ? "warn" : "error")} ({diag.Line},{diag.Column}): {diag.Message}", isError: !diag.IsWarning);

            if (!result.Success && !result.HasErrors)
                Append("Script failed (no diagnostics returned).", isError: true);

            if (result.Success)
                Append($"Done ({result.Duration.TotalMilliseconds:F0} ms)", isError: false);
        }
        catch (OperationCanceledException)
        {
            Append("Cancelled.", isError: false);
        }
        catch (Exception ex)
        {
            Append($"Exception: {ex.Message}", isError: true);
        }
        finally
        {
            lock (_ctsLock) { _cts = null; }
            cts.Dispose();
            IsBusy = false;
        }
    }

    public void Cancel()
    {
        CancellationTokenSource? cts;
        lock (_ctsLock) { cts = _cts; }
        cts?.Cancel();
    }

    public void ClearOutput() => Output.Clear();

    public string? HistoryUp()    => _history.NavigatePrevious();
    public string? HistoryDown()  => _history.NavigateNext();

    private void Append(string text, bool isError)
    {
        if (Output.Count >= MaxOutputLines)
            Output.RemoveAt(0);
        Output.Add(new OutputEntry(text, isError));
    }
}

public sealed record OutputEntry(string Text, bool IsError);

// ==========================================================
// Project: WpfHexEditor.Plugins.ScriptRunner
// File: ViewModels/ScriptRunnerViewModel.cs
// Description:
//     ViewModel for the ScriptRunner panel.
//     Owns the Run/Cancel/Clear commands and exposes bound properties
//     (Code, Output, IsRunning, StatusText, ScriptHistory).
// Architecture:
//     IScriptingService is injected at construction; the VM is pure INPC
//     with no direct WPF dependencies (testable without UI).
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.Plugins.ScriptRunner.Options;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.Plugins.ScriptRunner.Properties;

namespace WpfHexEditor.Plugins.ScriptRunner.ViewModels;

/// <summary>
/// View-model for the ScriptRunner dockable panel.
/// </summary>
public sealed class ScriptRunnerViewModel : ViewModelBase
{
    private readonly IScriptingService? _scripting;

    private string  _code       = DefaultCode;
    private string  _output     = string.Empty;
    private string  _statusText = ScriptRunnerResources.ScriptRunner_Ready;
    private bool    _isRunning;

    private CancellationTokenSource? _runCts;

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand RunCommand    { get; }
    public ICommand CancelCommand { get; }
    public ICommand ClearCommand  { get; }

    // ── History (last 20 entries) ─────────────────────────────────────────────

    public ObservableCollection<string> History { get; } = [];

    // ── Properties ────────────────────────────────────────────────────────────

    public string Code
    {
        get => _code;
        set { _code = value; OnPropertyChanged(); }
    }

    public string Output
    {
        get => _output;
        set { _output = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            _isRunning = value;
            OnPropertyChanged();
            // SDK RelayCommand re-queries via CommandManager.RequerySuggested
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    public ScriptRunnerViewModel(IScriptingService? scripting)
    {
        _scripting = scripting;

        RunCommand    = new RelayCommand(_ => { _ = RunAsync(); },    _ => !IsRunning && _scripting is not null);
        CancelCommand = new RelayCommand(_ => Cancel(),              _ => IsRunning);
        ClearCommand  = new RelayCommand(_ => ClearOutput());
    }

    // ── Command handlers ──────────────────────────────────────────────────────

    private async Task RunAsync()
    {
        if (_scripting is null || IsRunning) return;

        var code = Code.Trim();
        if (string.IsNullOrEmpty(code)) return;

        IsRunning  = true;
        StatusText = ScriptRunnerResources.ScriptRunner_Running;
        if (ScriptRunnerOptions.Instance.AutoClearOnNewSession)
            Output = string.Empty;

        _runCts = new CancellationTokenSource();
        try
        {
            var result = await _scripting.RunAsync(code, _runCts.Token).ConfigureAwait(false);

            // Update on UI thread via property (WPF data-binding handles marshal)
            var sb = new System.Text.StringBuilder();

            if (!string.IsNullOrEmpty(result.Output))
                sb.AppendLine(result.Output);

            foreach (var diag in result.Diagnostics)
            {
                var prefix = diag.IsWarning ? "âš  Warning" : "âœ— Error";
                sb.AppendLine($"{prefix} ({diag.Line},{diag.Column}): {diag.Message}");
            }

            Output     = sb.ToString().TrimEnd();
            StatusText = result.Success
                ? $"Done â€” {result.Duration.TotalMilliseconds:F0} ms"
                : $"Failed â€” {result.Diagnostics.Count(d => !d.IsWarning)} error(s)";

            AddToHistory(code);
        }
        catch (OperationCanceledException)
        {
            StatusText = ScriptRunnerResources.ScriptRunner_Cancelled;
        }
        catch (Exception ex)
        {
            Output     = $"âœ— Unexpected error: {ex.Message}";
            StatusText = ScriptRunnerResources.ScriptRunner_Error;
        }
        finally
        {
            _runCts?.Dispose();
            _runCts    = null;
            IsRunning  = false;
        }
    }

    private void Cancel()
    {
        _runCts?.Cancel();
        StatusText = ScriptRunnerResources.ScriptRunner_Cancelling;
    }

    private void ClearOutput()
    {
        Output     = string.Empty;
        StatusText = ScriptRunnerResources.ScriptRunner_Ready;
    }

    private void AddToHistory(string code)
    {
        int maxHistory = ScriptRunnerOptions.Instance.MaxHistoryEntries;
        if (History.Contains(code)) History.Remove(code);
        History.Insert(0, code);
        while (History.Count > maxHistory)
            History.RemoveAt(History.Count - 1);
    }

    // ── INPC ──────────────────────────────────────────────────────────────────


    // ── Defaults ──────────────────────────────────────────────────────────────

    private const string DefaultCode =
        """
        // C# scripting â€” IDE services are available directly:
        //   HexEditor, Documents, Output
        //   Use Print("...") to write to this output pane.

        Print("Hello from Script Runner!");
        """;
}

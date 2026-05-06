// ==========================================================
// Project: WpfHexEditor.Plugins.XamlDesigner
// File: ViewModels/CodeGenPanelViewModel.cs
// Author: Derek Tremblay
// Created: 2026-05-05
// Description:
//     ViewModel for the Code Generation dockable panel.
//     Exposes the current XamlCodeModel state (named elements, event sinks)
//     as observable collections for the panel's ListViews.
//     Bridges the ICodeBehindGeneratorService (SDK) event to UI-thread updates.
//
// Architecture Notes:
//     ViewModelBase — INotifyPropertyChanged pattern (project convention).
//     Subscribes to ICodeBehindGeneratorService.CodeBehindRegenerated.
//     Commands delegate to the service.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.ExtensionPoints.XamlDesigner;

namespace WpfHexEditor.Plugins.XamlDesigner.ViewModels;

/// <summary>
/// Row item representing a named element in the CodeGen panel list.
/// </summary>
public sealed class CodeGenNamedElementItem
{
    public string WpfTypeName { get; init; } = "";
    public string Name        { get; init; } = "";
    public int    SourceLine  { get; init; }
    public string LineLabel   => SourceLine > 0 ? $"line {SourceLine}" : "";
}

/// <summary>
/// Row item representing an event sink in the CodeGen panel list.
/// </summary>
public sealed class CodeGenEventSinkItem
{
    public string? ElementName        { get; init; }
    public string  EventAttributeName { get; init; } = "";
    public string  HandlerName        { get; init; } = "";
    public int     SourceLine         { get; init; }
    public string  Label => ElementName is { Length: > 0 }
        ? $"{ElementName}.{EventAttributeName} → {HandlerName}"
        : $"{EventAttributeName} → {HandlerName}";
}

/// <summary>
/// ViewModel for the Code Generation panel.
/// </summary>
public sealed class CodeGenPanelViewModel : ViewModelBase
{
    // ── State ─────────────────────────────────────────────────────────────────

    private ICodeBehindGeneratorService? _service;
    private bool   _isEnabled       = true;
    private bool   _hasXClass       = false;
    private string _statusText      = "No XAML document active.";
    private string _lastGenTime     = "";
    private string _errorText       = "";
    private bool   _hasError        = false;

    // ── Observable collections ─────────────────────────────────────────────────

    public ObservableCollection<CodeGenNamedElementItem> NamedElements { get; } = [];
    public ObservableCollection<CodeGenEventSinkItem>    EventSinks    { get; } = [];

    // ── Properties ────────────────────────────────────────────────────────────

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            if (_service is not null)
                _service.IsEnabled = value;
            OnPropertyChanged();
        }
    }

    public bool HasXClass
    {
        get => _hasXClass;
        private set { if (_hasXClass != value) { _hasXClass = value; OnPropertyChanged(); } }
    }

    public string StatusText
    {
        get => _statusText;
        private set { if (_statusText != value) { _statusText = value; OnPropertyChanged(); } }
    }

    public string LastGenTime
    {
        get => _lastGenTime;
        private set { if (_lastGenTime != value) { _lastGenTime = value; OnPropertyChanged(); } }
    }

    public string ErrorText
    {
        get => _errorText;
        private set { if (_errorText != value) { _errorText = value; OnPropertyChanged(); } }
    }

    public bool HasError
    {
        get => _hasError;
        private set { if (_hasError != value) { _hasError = value; OnPropertyChanged(); } }
    }

    public string NamedElementsCountLabel
        => NamedElements.Count == 1 ? "1 named element" : $"{NamedElements.Count} named elements";

    public string EventSinksCountLabel
        => EventSinks.Count == 1 ? "1 event sink" : $"{EventSinks.Count} event sinks";

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand ForceRegenerateCommand { get; }
    public ICommand GeneratePreviewCommand { get; }

    /// <summary>Fires when user requests to preview the generated C# (panel shows a popup).</summary>
    public event EventHandler<string>? PreviewRequested;

    // ── Constructor ───────────────────────────────────────────────────────────

    public CodeGenPanelViewModel()
    {
        ForceRegenerateCommand = new RelayCommand(
            async _ =>
            {
                if (_service is not null)
                    await _service.ForceRegenerateAsync().ConfigureAwait(false);
            },
            _ => _service is not null && _hasXClass);

        GeneratePreviewCommand = new RelayCommand(
            async _ =>
            {
                if (_service is null) return;
                var current = _service.CurrentSummary;
                if (current is null) return;
                // We need the raw XAML for the preview — request it via the service.
                string preview = await _service.GeneratePreviewAsync("", default).ConfigureAwait(false);
                PreviewRequested?.Invoke(this, preview);
            },
            _ => _service is not null && _hasXClass);
    }

    // ── Wiring ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Wires the panel to the active document's code generation service.
    /// Call from the plugin on each document focus switch.
    /// </summary>
    public void Attach(ICodeBehindGeneratorService service)
    {
        Detach();
        _service = service;
        _isEnabled = service.IsEnabled;
        OnPropertyChanged(nameof(IsEnabled));

        service.CodeBehindRegenerated -= OnRegenerated;
        service.CodeBehindRegenerated += OnRegenerated;

        // Immediately refresh from existing model.
        if (service.CurrentSummary is { } summary)
            UpdateFromSummary(summary, success: true);
        else
            Reset("Waiting for first scan...");
    }

    /// <summary>Detaches from the current document. Call on document close or focus loss.</summary>
    public void Detach()
    {
        if (_service is not null)
        {
            _service.CodeBehindRegenerated -= OnRegenerated;
            _service = null;
        }
        Reset("No XAML document active.");
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void OnRegenerated(object? sender, CodeBehindRegenEventArgs e)
    {
        // Already on UI thread (SyncService marshals).
        if (e.Success)
            UpdateFromSummary(e.Summary, success: true);
        else
            UpdateError(e.Error ?? "Unknown error.");
    }

    private void UpdateFromSummary(CodeBehindSummary summary, bool success)
    {
        HasXClass = summary.IsCodeGenEnabled;

        NamedElements.Clear();
        foreach (var el in summary.NamedElements)
            NamedElements.Add(new CodeGenNamedElementItem
            {
                WpfTypeName = el.WpfTypeName,
                Name        = el.Name,
                SourceLine  = el.SourceLine
            });

        EventSinks.Clear();
        foreach (var sink in summary.EventSinks)
            EventSinks.Add(new CodeGenEventSinkItem
            {
                ElementName        = sink.ElementName,
                EventAttributeName = sink.EventAttributeName,
                HandlerName        = sink.HandlerName,
                SourceLine         = sink.SourceLine
            });

        StatusText   = summary.IsCodeGenEnabled
            ? $"{summary.ClassName} ({summary.RootTypeName})"
            : "No x:Class — code generation disabled.";
        LastGenTime  = $"Last generated: {DateTime.Now:HH:mm:ss}";
        HasError     = false;
        ErrorText    = "";

        OnPropertyChanged(nameof(NamedElementsCountLabel));
        OnPropertyChanged(nameof(EventSinksCountLabel));
    }

    private void UpdateError(string error)
    {
        HasError  = true;
        ErrorText = error;
    }

    private void Reset(string status)
    {
        NamedElements.Clear();
        EventSinks.Clear();
        HasXClass   = false;
        StatusText  = status;
        LastGenTime = "";
        HasError    = false;
        ErrorText   = "";
        OnPropertyChanged(nameof(NamedElementsCountLabel));
        OnPropertyChanged(nameof(EventSinksCountLabel));
    }
}

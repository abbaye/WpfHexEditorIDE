// ==========================================================
// Project: WpfHexEditor.App
// File: BinaryAnalysis/ViewModels/RepairPreviewViewModel.cs
// Description: P14 — ViewModel for the Repair Preview dialog.
//              Shows a list of RepairActions for the detected format, allows the
//              user to check/uncheck each, previews the byte diff, then applies
//              the selected repairs to the open file via ByteArrayRepairExecutor.
// Architecture: MVVM — ViewModel only; no UI logic. Bound to RepairPreviewDialog.xaml.
// ==========================================================

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using WpfHexEditor.Core.Contracts;
using WpfHexEditor.Core.Definitions.Metadata;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.App.BinaryAnalysis.ViewModels;

/// <summary>Row item for a single repair action in the dialog grid.</summary>
public sealed class RepairActionItem : ViewModelBase
{
    private bool _isSelected = true;

    public RepairAction Action      { get; }
    public string Name              => Action.Name;
    public string ActionType        => Action.Action;
    public string Description       => Action.Description ?? string.Empty;
    public string Target            => Action.Target ?? "—";

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public RepairActionItem(RepairAction action) => Action = action;
}

/// <summary>
/// P14 — ViewModel for the Repair Preview dialog.
/// </summary>
public sealed class RepairPreviewViewModel : ViewModelBase
{
    private readonly ByteArrayRepairExecutor _executor = new();

    private string _formatName      = string.Empty;
    private string _statusMessage   = "Select repairs and click Apply.";
    private string _previewHex      = string.Empty;
    private bool   _canApply;
    private bool   _isApplying;
    private byte[] _originalBytes   = [];
    private byte[] _patchedBytes    = [];

    // ── Properties ────────────────────────────────────────────────────────────

    public string FormatName    { get => _formatName;    set => SetField(ref _formatName, value); }
    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }
    public string PreviewHex    { get => _previewHex;    set => SetField(ref _previewHex, value); }
    public bool   CanApply      { get => _canApply;      set => SetField(ref _canApply, value); }
    public bool   IsApplying    { get => _isApplying;    set => SetField(ref _isApplying, value); }

    /// <summary>Patched bytes after simulation (empty until Simulate is called).</summary>
    public byte[] PatchedBytes => _patchedBytes;

    public ObservableCollection<RepairActionItem> Actions { get; } = [];

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand SimulateCommand { get; }
    public ICommand ApplyCommand    { get; }
    public ICommand CancelCommand   { get; }

    /// <summary>Fired when the user confirms the apply (dialog result = true).</summary>
    public event EventHandler<byte[]>? ApplyConfirmed;
    /// <summary>Fired when the user cancels.</summary>
    public event EventHandler?         Cancelled;

    public RepairPreviewViewModel()
    {
        SimulateCommand = new RelayCommand(Simulate, () => Actions.Any(a => a.IsSelected) && !IsApplying);
        ApplyCommand    = new RelayCommand(Apply,    () => CanApply && !IsApplying);
        CancelCommand   = new RelayCommand(() => Cancelled?.Invoke(this, EventArgs.Empty));
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    /// <summary>Populates the dialog from the format entry and current file bytes.</summary>
    public void Load(
        string                    formatName,
        IReadOnlyList<RepairAction> repairs,
        byte[]                    fileBytes)
    {
        FormatName     = formatName;
        _originalBytes = fileBytes;
        _patchedBytes  = fileBytes;
        PreviewHex     = string.Empty;
        CanApply       = false;
        StatusMessage  = $"{repairs.Count} repair action(s) available for {formatName}.";

        Actions.Clear();
        foreach (var r in repairs)
            Actions.Add(new RepairActionItem(r));
    }

    // ── Commands impl ─────────────────────────────────────────────────────────

    private void Simulate()
    {
        var selected = Actions.Where(a => a.IsSelected).ToList();
        if (selected.Count == 0) { StatusMessage = "No actions selected."; return; }

        byte[] working = (byte[])_originalBytes.Clone();
        int    total   = 0;

        foreach (var item in selected)
        {
            var result = _executor.Apply(working, item.Action);
            if (result.Success)
            {
                working = result.Patched;
                total  += result.BytesChanged;
            }
            else
            {
                StatusMessage = $"Simulation failed on '{item.Name}': {result.Message}";
                return;
            }
        }

        _patchedBytes = working;
        CanApply      = true;
        StatusMessage = $"Simulation OK — {total} byte(s) would be changed. Click Apply to confirm.";
        PreviewHex    = BuildDiffPreview(_originalBytes, working);
    }

    private void Apply()
    {
        IsApplying    = true;
        ApplyConfirmed?.Invoke(this, _patchedBytes);
        IsApplying    = false;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string BuildDiffPreview(byte[] original, byte[] patched)
    {
        int showBytes = Math.Min(256, Math.Max(original.Length, patched.Length));
        var sb        = new System.Text.StringBuilder();

        sb.AppendLine("  Offset   Original → Patched");
        sb.AppendLine("  ─────────────────────────────");

        for (int i = 0; i < showBytes; i++)
        {
            byte o = i < original.Length ? original[i] : (byte)0;
            byte p = i < patched.Length  ? patched[i]  : (byte)0;
            if (o != p)
                sb.AppendLine($"  0x{i:X6}  {o:X2} → {p:X2}");
        }

        if (!sb.ToString().Contains("→") || sb.Length < 60)
            sb.AppendLine("  (no differences in first 256 bytes)");

        return sb.ToString();
    }
}

/// <summary>Simple ICommand implementation for ViewModels without external deps.</summary>
file sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add    => System.Windows.Input.CommandManager.RequerySuggested += value;
        remove => System.Windows.Input.CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter)    => execute();
}

// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: AlignmentToolbarViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     ViewModel providing ICommand bindings for the 12 alignment and
//     distribution toolbar buttons in the XAML designer split host.
//
// Architecture Notes:
//     INPC + RelayCommand.
//     AlignmentService does the actual positioning work.
//     OperationsBatch event fires all resulting DesignOperations for undo/redo.
// ==========================================================

using System.Windows.Input;
using WpfHexEditor.Editor.XamlDesigner.Services;
using WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.Editor.XamlDesigner.ViewModels;

/// <summary>
/// Exposes alignment commands for the designer toolbar.
/// </summary>
public sealed class AlignmentToolbarViewModel
{
    private readonly AlignmentService _service = new();
    private Func<IReadOnlyList<(System.Windows.FrameworkElement, int)>>? _getSelection;

    // ── Constructor ───────────────────────────────────────────────────────────

    public AlignmentToolbarViewModel()
    {
        AlignLeftCommand      = new RelayCommand(_ => Execute(_service.AlignLeft));
        AlignCenterHCommand   = new RelayCommand(_ => Execute(_service.AlignCenterH));
        AlignRightCommand     = new RelayCommand(_ => Execute(_service.AlignRight));
        AlignTopCommand       = new RelayCommand(_ => Execute(_service.AlignTop));
        AlignCenterVCommand   = new RelayCommand(_ => Execute(_service.AlignCenterV));
        AlignBottomCommand    = new RelayCommand(_ => Execute(_service.AlignBottom));
        DistributeHCommand    = new RelayCommand(_ => Execute(_service.DistributeH));
        DistributeVCommand    = new RelayCommand(_ => Execute(_service.DistributeV));
        BringToFrontCommand   = new RelayCommand(_ => ExecuteSingle(AlignmentService.BringToFront));
        SendToBackCommand     = new RelayCommand(_ => ExecuteSingle(AlignmentService.SendToBack));
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand AlignLeftCommand    { get; }
    public ICommand AlignCenterHCommand { get; }
    public ICommand AlignRightCommand   { get; }
    public ICommand AlignTopCommand     { get; }
    public ICommand AlignCenterVCommand { get; }
    public ICommand AlignBottomCommand  { get; }
    public ICommand DistributeHCommand  { get; }
    public ICommand DistributeVCommand  { get; }
    public ICommand BringToFrontCommand { get; }
    public ICommand SendToBackCommand   { get; }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Raised after each alignment operation with the list of resulting DesignOperations.</summary>
    public event EventHandler<IReadOnlyList<AlignmentResult>>? OperationsBatch;

    // ── Wiring ────────────────────────────────────────────────────────────────

    /// <summary>Provides the current selection list to the commands.</summary>
    public void SetSelectionProvider(Func<IReadOnlyList<(System.Windows.FrameworkElement, int)>> provider)
        => _getSelection = provider;

    // ── Private ───────────────────────────────────────────────────────────────

    private void Execute(
        Func<IReadOnlyList<(System.Windows.FrameworkElement, int)>, IReadOnlyList<AlignmentResult>> op)
    {
        var sel = _getSelection?.Invoke();
        if (sel is null || sel.Count < 2) return;
        var results = op(sel);
        if (results.Count > 0)
            OperationsBatch?.Invoke(this, results);
    }

    private void ExecuteSingle(Action<System.Windows.FrameworkElement> op)
    {
        var sel = _getSelection?.Invoke();
        if (sel is null) return;
        foreach (var (el, _) in sel)
            op(el);
    }
}

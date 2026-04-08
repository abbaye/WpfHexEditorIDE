// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/DiffDetailViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     ViewModel for the Diff Detail pane shown below the diff results DataGrid.
//     When the user selects a diff entry, LoadAsync decompiles both baseline
//     and target members (via CSharpSkeletonEmitter) and computes the unified
//     diff via DiffTextService.
//
// Architecture Notes:
//     Pattern: MVVM â€” populated by AssemblyDiffViewModel.SelectedDiffEntry setter.
//     Decompilation runs on a background Task to avoid blocking the UI thread.
//     CancellationTokenSource is recreated on each selection to cancel stale loads.
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Core.AssemblyAnalysis.Models;
using WpfHexEditor.Core.AssemblyAnalysis.Services;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

/// <summary>
/// Provides decompiled code and unified diff for the selected diff entry.
/// </summary>
public sealed class DiffDetailViewModel : ViewModelBase
{
    private CancellationTokenSource? _cts;

    // ── INotifyPropertyChanged ────────────────────────────────────────────────



    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    // ── State ─────────────────────────────────────────────────────────────────

    private bool   _isLoading;
    private string _baselineCode = string.Empty;
    private string _targetCode   = string.Empty;
    private string _unifiedDiff  = string.Empty;
    private string _entryLabel   = string.Empty;

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    /// <summary>Decompiled C# text for the baseline member (left side).</summary>
    public string BaselineCode
    {
        get => _baselineCode;
        private set => SetField(ref _baselineCode, value);
    }

    /// <summary>Decompiled C# text for the target member (right side).</summary>
    public string TargetCode
    {
        get => _targetCode;
        private set => SetField(ref _targetCode, value);
    }

    /// <summary>
    /// Unified diff output (empty when the texts are identical or nothing is selected).
    /// </summary>
    public string UnifiedDiff
    {
        get => _unifiedDiff;
        private set => SetField(ref _unifiedDiff, value);
    }

    /// <summary>Display label for the currently loaded entry ("TypeName :: MemberSig").</summary>
    public string EntryLabel
    {
        get => _entryLabel;
        private set => SetField(ref _entryLabel, value);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Decompiles both sides of <paramref name="entry"/> and populates
    /// <see cref="BaselineCode"/>, <see cref="TargetCode"/>, and <see cref="UnifiedDiff"/>.
    /// Any previous in-flight load is cancelled before starting.
    /// </summary>
    public async Task LoadAsync(
        DiffEntryViewModel entry,
        AssemblyModel?     baselineModel,
        AssemblyModel?     targetModel)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        EntryLabel   = entry.DisplayName;
        BaselineCode = string.Empty;
        TargetCode   = string.Empty;
        UnifiedDiff  = string.Empty;

        try
        {
            var diffEntry = entry.Entry;

            var (baseCode, tgtCode) = await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var b = GetCode(diffEntry, isBaseline: true,  baselineModel);
                var t = GetCode(diffEntry, isBaseline: false, targetModel);
                return (b, t);
            }, ct);

            if (ct.IsCancellationRequested) return;

            BaselineCode = baseCode;
            TargetCode   = tgtCode;
            UnifiedDiff  = DiffTextService.ComputeUnifiedDiff(baseCode, tgtCode);
        }
        catch (OperationCanceledException) { /* silent */ }
        catch (Exception ex)
        {
            UnifiedDiff = $"// Diff computation failed: {ex.Message}";
        }
        finally { }
    }

    /// <summary>Clears all detail content.</summary>
    public void Clear()
    {
        _cts?.Cancel();
        EntryLabel   = string.Empty;
        BaselineCode = string.Empty;
        TargetCode   = string.Empty;
        UnifiedDiff  = string.Empty;
    }

    // ── Decompile helper ──────────────────────────────────────────────────────

    private static string GetCode(DiffEntry entry, bool isBaseline, AssemblyModel? model)
    {
        if (model is null)
            return isBaseline ? "// Baseline not available" : "// Target not available";

        var token = isBaseline ? entry.BaselineToken : entry.TargetToken;
        if (token == 0)
            return isBaseline ? "// (member not present in baseline)" : "// (member not present in target)";

        try
        {
            var type = model.Types.FirstOrDefault(t => t.FullName == entry.TypeFullName);
            if (type is null)
                return $"// Type '{entry.TypeFullName}' not found";

            // Emit the full type skeleton â€” both for type-level and member-level entries.
            // This gives meaningful context regardless of entry kind.
            var emitter = new CSharpSkeletonEmitter();
            return emitter.EmitType(type);
        }
        catch (Exception ex)
        {
            return $"// Decompile error: {ex.Message}";
        }
    }
}

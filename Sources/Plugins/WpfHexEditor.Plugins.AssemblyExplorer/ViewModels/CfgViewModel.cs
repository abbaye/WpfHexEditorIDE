// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/CfgViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     ViewModel for the CFG tab in the detail pane.
//     Owns the ControlFlowGraph displayed by CfgCanvas.
//     Fires BlockOffsetSelected when the user clicks a block,
//     so the parent VM can switch to the IL tab and scroll to the offset.
//
// Architecture Notes:
//     Pattern: MVVM — populated by AssemblyDetailViewModel.ShowNodeAsync.
//     LoadForMethodAsync runs CfgBuilder on a background thread.
//     IsLoading / ErrorMessage drive the overlay in AssemblyDetailPane.
// ==========================================================

using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using WpfHexEditor.Core.AssemblyAnalysis.Models;
using WpfHexEditor.Core.AssemblyAnalysis.Services;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

/// <summary>
/// Provides the <see cref="ControlFlowGraph"/> and loading state for the CFG tab.
/// </summary>
public sealed class CfgViewModel : AssemblyNodeViewModel
{
    // ── AssemblyNodeViewModel overrides (not a tree node) ────────────────────
    public override string DisplayName => "CFG";
    public override string IconGlyph   => "\uE9D9"; // Flow icon

    // ── Graph ─────────────────────────────────────────────────────────────────

    private ControlFlowGraph? _graph;
    public ControlFlowGraph? Graph
    {
        get => _graph;
        private set
        {
            SetField(ref _graph, value);
            OnPropertyChanged(nameof(IsAvailable));
        }
    }

    public bool IsAvailable => _graph is { Blocks.Count: > 0 };

    // ── Loading state ─────────────────────────────────────────────────────────

    private bool _isBuilding;
    public bool IsBuilding
    {
        get => _isBuilding;
        private set => SetField(ref _isBuilding, value);
    }

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    // ── Navigation event ─────────────────────────────────────────────────────

    /// <summary>
    /// Raised when the user clicks a block in the CFG canvas.
    /// Argument is the IL start offset of the clicked block.
    /// Subscriber (AssemblyDetailViewModel) switches to the IL tab.
    /// </summary>
    public event Action<int>? BlockOffsetSelected;

    internal void RaiseBlockOffsetSelected(int offset)
        => BlockOffsetSelected?.Invoke(offset);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Asynchronously builds the CFG for the given method node.
    /// Runs <see cref="CfgBuilder"/> on a background thread to avoid blocking the UI.
    /// </summary>
    public async Task LoadForMethodAsync(MemberModel method, string filePath, CancellationToken ct)
    {
        Graph        = null;
        ErrorMessage = string.Empty;
        IsBuilding   = true;

        try
        {
            var graph = await Task.Run(() => BuildGraph(method, filePath), ct);
            if (ct.IsCancellationRequested) return;

            Graph        = graph;
            ErrorMessage = graph is null ? "// No CFG available (abstract, extern, or empty method)." : string.Empty;
        }
        catch (OperationCanceledException)
        {
            // New node was selected — silent cancel.
        }
        catch (Exception ex)
        {
            ErrorMessage = $"// CFG build failed: {ex.Message}";
        }
        finally
        {
            IsBuilding = false;
        }
    }

    /// <summary>Clears the current graph and resets the loading state.</summary>
    public void Clear()
    {
        Graph        = null;
        IsBuilding   = false;
        ErrorMessage = string.Empty;
    }

    // ── Background worker ─────────────────────────────────────────────────────

    private static ControlFlowGraph? BuildGraph(MemberModel method, string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;
        if (method.MetadataToken == 0) return null;

        // FileShare.ReadWrite: allow concurrent access while HexEditor holds the file.
        using var stream   = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var peReader = new PEReader(stream);
        if (!peReader.HasMetadata) return null;

        var mdReader   = peReader.GetMetadataReader();
        var handle     = System.Reflection.Metadata.Ecma335.MetadataTokens.MethodDefinitionHandle(
                             method.MetadataToken & 0x00FFFFFF);
        var methodDef  = mdReader.GetMethodDefinition(handle);
        return CfgBuilder.BuildCfg(methodDef, mdReader, peReader);
    }
}

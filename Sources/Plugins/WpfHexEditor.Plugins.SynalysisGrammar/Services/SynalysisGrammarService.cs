// ==========================================================
// Project: WpfHexEditor.Plugins.SynalysisGrammar
// File: Services/SynalysisGrammarService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Orchestrates grammar loading and execution against the active hex file.
//     Publishes GrammarAppliedEvent (→ ParsedFieldsPlugin) and pushes
//     CustomBackgroundBlocks into the HexEditor overlay via IHexEditorService.
//
// Architecture Notes:
//     Pattern: Service / Orchestrator
//     - Uses SynalysisGrammarRepository for grammar lookup.
//     - Uses SynalysisGrammarInterpreter for binary execution.
//     - Bridges output through SynalysisToBackgroundBlockBridge and
//       SynalysisToFieldViewModelBridge (WPF-layer bridge classes).
//     - All methods are async-friendly (offloads interpretation to Task.Run).
// ==========================================================

using System.IO;
using System.Windows;
using WpfHexEditor.Core.SynalysisGrammar;
using WpfHexEditor.Plugins.SynalysisGrammar.Bridge;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Events;

namespace WpfHexEditor.Plugins.SynalysisGrammar.Services;

/// <summary>
/// Orchestrates grammar detection, execution, and result distribution.
/// </summary>
public sealed class SynalysisGrammarService
{
    private readonly SynalysisGrammarRepository _repository;
    private readonly IIDEHostContext _context;

    private const int SampleSize = 64 * 1024;   // 64 KB for initial parse

    public SynalysisGrammarService(SynalysisGrammarRepository repository, IIDEHostContext context)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _context    = context    ?? throw new ArgumentNullException(nameof(context));
    }

    // -- Public API --------------------------------------------------------

    /// <summary>
    /// Detects the grammar by file extension and applies it to the active file.
    /// No-op when no grammar covers the extension.
    /// </summary>
    public async Task AutoApplyAsync(string filePath, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(filePath)) return;

        var ext     = Path.GetExtension(filePath);
        var grammar = _repository.FindByExtension(ext);
        if (grammar is null) return;

        await ApplyCoreAsync(grammar, filePath, ct);
    }

    /// <summary>Applies a specific grammar (by repository key) to the active file.</summary>
    public async Task ApplyByKeyAsync(string grammarKey, CancellationToken ct = default)
    {
        var grammar = _repository.GetByKey(grammarKey);
        if (grammar is null) return;

        var filePath = _context.HexEditor.CurrentFilePath ?? string.Empty;
        await ApplyCoreAsync(grammar, filePath, ct);
    }

    // -- Core execution ----------------------------------------------------

    private async Task ApplyCoreAsync(UfwbRoot grammar, string filePath, CancellationToken ct)
    {
        // Read sample from HexEditor (non-blocking).
        var data = await ReadSampleAsync(ct);
        if (data is null || data.Length == 0) return;

        // Execute interpreter off the UI thread.
        SynalysisParseResult result;
        try
        {
            result = await Task.Run(() => new SynalysisGrammarInterpreter().Execute(grammar, data), ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            _context.Output?.Info($"[GrammarExplorer] Parse error: {ex.Message}");
            return;
        }

        // Log warnings.
        foreach (var w in result.Warnings)
            _context.Output?.Info($"[GrammarExplorer] Warning: {w}");

        // Push overlay blocks onto HexEditor (must be on UI thread).
        if (Application.Current?.Dispatcher is { } dispatcher)
            await dispatcher.InvokeAsync(() => PushBackgroundBlocks(result));
        else
            PushBackgroundBlocks(result);

        // Publish EventBus message (consumed by ParsedFieldsPlugin).
        _context.EventBus.Publish(new GrammarAppliedEvent
        {
            GrammarName  = result.GrammarName,
            FilePath     = filePath,
            Fields       = result.Fields,
            ColorRegions = result.ColorRegions,
            Warnings     = result.Warnings,
        });
    }

    private void PushBackgroundBlocks(SynalysisParseResult result)
    {
        if (!_context.HexEditor.IsActive) return;

        // Remove previous synalysis overlay blocks.
        _context.HexEditor.ClearCustomBackgroundBlockByTag("synalysis:");

        // Add new ones.
        var blocks = SynalysisToBackgroundBlockBridge.Convert(result.ColorRegions);
        foreach (var block in blocks)
            _context.HexEditor.AddCustomBackgroundBlock(block);
    }

    private async Task<byte[]?> ReadSampleAsync(CancellationToken ct)
    {
        if (!_context.HexEditor.IsActive) return null;

        return await Task.Run(() =>
        {
            var size = (int)Math.Min(_context.HexEditor.FileSize, SampleSize);
            return _context.HexEditor.ReadBytes(0, size);
        }, ct);
    }
}

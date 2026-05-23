// ==========================================================
// Project: WpfHexEditor.App
// File: BinaryAnalysis/Services/BlockInterpreterService.cs
// Description: P13 — bridges SimpleBlockInterpreter (StructureEditor) with
//              IParsedFieldsPanel (HexEditor). Runs the whfmt block interpreter
//              against the open file's bytes and pushes ParsedFieldViewModel
//              entries into the connected ParsedFieldsPanel.
// Architecture: Stateless orchestration service. Consumers call ParseAndPushAsync
//              on file-open or format-change events. Uses BeginBulkUpdate /
//              EndBulkUpdate to avoid per-field UI flicker.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.Editor.StructureEditor.Services;

namespace WpfHexEditor.App.BinaryAnalysis.Services;

/// <summary>
/// P13 — Runs <see cref="SimpleBlockInterpreter"/> on binary data and pushes the
/// resulting parsed fields into an <see cref="IParsedFieldsPanel"/>.
/// </summary>
public sealed class BlockInterpreterService
{
    /// <summary>
    /// Maximum bytes loaded for block interpretation (avoids OOM on huge files).
    /// The interpreter cannot seek back, so we cap at this window.
    /// </summary>
    public const int MaxInterpretBytes = 4 * 1024 * 1024;   // 4 MB

    /// <summary>
    /// Interprets <paramref name="format"/> against <paramref name="fileBytes"/> and
    /// pushes the resulting <see cref="ParsedFieldViewModel"/> rows into <paramref name="panel"/>.
    /// Safe to call from any thread; dispatches panel updates on the calling context.
    /// Cancellable — a new call should cancel the previous one.
    /// </summary>
    /// <param name="format">The whfmt <see cref="FormatDefinition"/> to interpret.</param>
    /// <param name="fileBytes">Binary content of the open file (clamped to <see cref="MaxInterpretBytes"/>).</param>
    /// <param name="panel">Target panel that receives the fields.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    public async Task ParseAndPushAsync(
        FormatDefinition    format,
        byte[]              fileBytes,
        IParsedFieldsPanel  panel,
        CancellationToken   ct = default)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(fileBytes);
        ArgumentNullException.ThrowIfNull(panel);

        byte[] data = fileBytes.Length > MaxInterpretBytes
            ? fileBytes[..MaxInterpretBytes]
            : fileBytes;

        // Run interpreter on background thread
        List<ParsedFieldViewModel> fields = await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            var interpreter = new SimpleBlockInterpreter(data);
            var results     = interpreter.Run(format);
            ct.ThrowIfCancellationRequested();
            return MapToViewModels(results);
        }, ct);

        ct.ThrowIfCancellationRequested();

        // Push to panel — must run on the UI thread (caller's sync context)
        panel.BeginBulkUpdate();
        try
        {
            panel.Clear();
            foreach (var vm in fields)
                panel.ParsedFields.Add(vm);
        }
        finally
        {
            panel.EndBulkUpdate();
        }
    }

    // ── Mapping ──────────────────────────────────────────────────────────────

    private static List<ParsedFieldViewModel> MapToViewModels(
        IReadOnlyList<BlockTestResult> results)
    {
        var vms    = new List<ParsedFieldViewModel>(results.Count);
        var groups = new Dictionary<string, ParsedFieldViewModel>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in results)
        {
            if (r.IsSummary)
            {
                // Summary rows become collapsible group headers
                var group = new ParsedFieldViewModel
                {
                    Name        = r.BlockName,
                    Offset      = r.Offset >= 0 ? r.Offset : 0,
                    Length      = r.Length,
                    RawValue    = r.RawHex,
                    ValueType   = r.BlockType,
                    IsGroup     = true,
                    IsExpanded  = true,
                    Color       = StatusColor(r.Status),
                    Description = r.Note,
                };
                vms.Add(group);
                groups[r.BlockName] = group;
                continue;
            }

            var vm = new ParsedFieldViewModel
            {
                Name           = r.BlockName,
                Offset         = r.Offset >= 0 ? r.Offset : 0,
                Length         = r.Length,
                RawValue       = r.RawHex,
                FormattedValue = r.ParsedValue,
                ValueType      = r.BlockType,
                Color          = StatusColor(r.Status),
                Description    = r.Note,
                IsValid        = r.Status is "OK" or "Warning",
                ValidationMessage = r.Status is "Error" ? r.Note : string.Empty,
            };

            vms.Add(vm);
        }

        return vms;
    }

    private static string StatusColor(string status) => status switch
    {
        "OK"      => "#3fb950",   // green
        "Warning" => "#d29922",   // yellow
        "Error"   => "#f85149",   // red
        "Skipped" => "#8b949e",   // grey
        _         => "#58a6ff"    // blue (info)
    };
}

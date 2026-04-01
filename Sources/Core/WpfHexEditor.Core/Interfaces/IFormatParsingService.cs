// ==========================================================
// Project: WpfHexEditor.Core
// File: IFormatParsingService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-26
// Description:
//     Universal format parsing orchestration contract.
//     Decouples parsed field display from any specific editor.
//     Any editor providing an IBinaryDataSource can trigger format
//     detection, field parsing, bookmark generation, and forensics.
//
// Architecture Notes:
//     Service facade — wraps FormatDetectionService, FormatScriptInterpreter,
//     VariableContext, AssertionRunner, FieldValueReader, etc.
//     Panel wiring is done via ConnectPanel/DisconnectPanel.
//     The whfmt engine (FormatDetection/) is consumed but never modified.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Core.Interfaces
{
    /// <summary>
    /// Event arguments raised when format parsing completes.
    /// </summary>
    public class FormatParsingCompleteEventArgs : EventArgs
    {
        /// <summary>Name of the detected format.</summary>
        public string? FormatName { get; init; }

        /// <summary>Number of parsed fields produced.</summary>
        public int FieldCount { get; init; }

        /// <summary>True if assertion failures (forensic alerts) were found.</summary>
        public bool HasForensicAlerts { get; init; }

        /// <summary>Number of navigation bookmarks generated from whfmt.</summary>
        public int BookmarkCount { get; init; }
    }

    /// <summary>
    /// Universal format detection + field parsing service.
    /// Editors attach an <see cref="IBinaryDataSource"/>; the service detects
    /// the binary format and populates an <see cref="IParsedFieldsPanel"/>.
    /// </summary>
    public interface IFormatParsingService
    {
        // ── Data Source ──────────────────────────────────────────────────

        /// <summary>
        /// Attach an editor's data source. Replaces any previously attached source.
        /// Raises <see cref="Cleared"/> to reset the panel, then optionally
        /// triggers auto-detection if <paramref name="autoDetect"/> is true.
        /// </summary>
        void Attach(IBinaryDataSource source, bool autoDetect = true);

        /// <summary>Detach the current source and clear all state.</summary>
        void Detach();

        /// <summary>Currently attached data source, or null.</summary>
        IBinaryDataSource? ActiveSource { get; }

        // ── Panel Wiring ─────────────────────────────────────────────────

        /// <summary>Connect a UI panel to receive parsed field data.</summary>
        void ConnectPanel(IParsedFieldsPanel panel);

        /// <summary>Disconnect the current panel.</summary>
        void DisconnectPanel();

        /// <summary>Currently connected panel, or null.</summary>
        IParsedFieldsPanel? ActivePanel { get; }

        // ── Detection & Parsing ──────────────────────────────────────────

        /// <summary>
        /// Run format detection on the attached source, then parse all fields.
        /// No-op if no source is attached.
        /// </summary>
        Task DetectAndParseAsync(CancellationToken ct = default);

        /// <summary>
        /// Re-parse fields using the currently active format (skip re-detection).
        /// Useful after byte edits or formatter changes.
        /// </summary>
        void Refresh();

        /// <summary>Clear all detection state, fields, bookmarks, and forensics.</summary>
        void Clear();

        // ── State ────────────────────────────────────────────────────────

        /// <summary>Full detection result from the last detection run.</summary>
        FormatDetectionResult? LastDetectionResult { get; }

        /// <summary>Currently active format definition (from detection or manual selection).</summary>
        FormatDefinition? ActiveFormat { get; }

        /// <summary>All format candidates ranked by confidence.</summary>
        IReadOnlyList<FormatMatchCandidate> Candidates { get; }

        // ── User Actions ─────────────────────────────────────────────────

        /// <summary>
        /// Switch to a different format candidate. Re-applies blocks and re-parses fields.
        /// </summary>
        void SelectCandidate(FormatMatchCandidate candidate);

        /// <summary>
        /// Change the value display formatter ("hex", "decimal", "mixed", "string").
        /// Re-formats all existing field values.
        /// </summary>
        void SetFormatter(string formatterType);

        // ── Parsed Field Access ──────────────────────────────────────────

        /// <summary>True when at least one field was parsed from the active format.</summary>
        bool HasParsedFields { get; }

        /// <summary>Returns a snapshot of all parsed fields from the active format.</summary>
        IReadOnlyList<ParsedFieldViewModel> GetParsedFields();

        // ── Events ───────────────────────────────────────────────────────

        /// <summary>Raised after format detection completes (success or failure).</summary>
        event EventHandler<FormatDetectedEventArgs>? FormatDetected;

        /// <summary>Raised after field parsing and metadata population completes.</summary>
        event EventHandler<FormatParsingCompleteEventArgs>? ParsingComplete;

        /// <summary>Raised when all state is cleared (detach, new file, etc.).</summary>
        event EventHandler? Cleared;
    }
}

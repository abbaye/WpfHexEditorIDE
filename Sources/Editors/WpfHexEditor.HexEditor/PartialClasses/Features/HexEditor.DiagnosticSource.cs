// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: HexEditor.DiagnosticSource.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Partial class implementing IDiagnosticSource for the HexEditor.
//     Provides structured diagnostic information (warnings, errors, info messages)
//     about the currently loaded file to the ErrorPanel in the IDE.
//
// Architecture Notes:
//     Implements IDiagnosticSource interface from WpfHexEditor.Editor.Core.
//     File-scoped namespace syntax (C# 10). Diagnostic entries are raised
//     reactively on file load, format detection, and edit events.
//
// ==========================================================

using System;
using System.Collections.Generic;
using System.IO;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.HexEditor;

/// <summary>
/// Partial class that implements <see cref="IDiagnosticSource"/> for HexEditor.
/// Collects parsing errors, format validation warnings and informational messages.
/// Currently scaffolded — will be populated by the format detection and parser pipeline.
/// </summary>
/// <remarks>
/// <see cref="GetDiagnostics()"/> (→ string) already exists in HexEditor.Diagnostics.cs,
/// so the interface method is implemented explicitly to avoid ambiguity.
/// </remarks>
public partial class HexEditor : IDiagnosticSource
{
    // -- IDiagnosticSource backing store --------------------------------------

    private readonly List<DiagnosticEntry> _panelDiagnostics = [];

    /// <inheritdoc/>
    public string SourceLabel
        => Path.GetFileName(FileName) is { Length: > 0 } f ? f : "HexEditor";

    /// <summary>
    /// Explicit interface implementation — avoids conflict with the existing
    /// <c>public string GetDiagnostics()</c> in HexEditor.Diagnostics.cs.
    /// </summary>
    IReadOnlyList<DiagnosticEntry> IDiagnosticSource.GetDiagnostics() => _panelDiagnostics;

    /// <inheritdoc/>
    public event EventHandler? DiagnosticsChanged;

    // -- Internal API (used by parsers/validators in future sprints) ----------

    /// <summary>
    /// Adds a diagnostic entry and notifies subscribers.
    /// </summary>
    internal void ReportDiagnostic(DiagnosticEntry entry)
    {
        _panelDiagnostics.Add(entry);
        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Removes all diagnostics and notifies subscribers.
    /// </summary>
    internal void ClearDiagnostics()
    {
        if (_panelDiagnostics.Count == 0) return;
        _panelDiagnostics.Clear();
        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
    }
}

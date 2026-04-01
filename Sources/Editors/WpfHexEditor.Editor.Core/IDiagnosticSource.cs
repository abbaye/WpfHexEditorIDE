//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Implemented by any component that can produce diagnostic entries
/// (parsing errors, validation warnings, project issues, etc.).
/// The error panel subscribes to <see cref="DiagnosticsChanged"/> and
/// calls <see cref="GetDiagnostics"/> to refresh its list.
/// </summary>
public interface IDiagnosticSource
{
    /// <summary>
    /// Human-readable label identifying this source (e.g. "HexEditor", "Solution").
    /// </summary>
    string SourceLabel { get; }

    /// <summary>
    /// Returns the current set of diagnostics produced by this source.
    /// </summary>
    IReadOnlyList<DiagnosticEntry> GetDiagnostics();

    /// <summary>
    /// Raised whenever the diagnostic list has changed.
    /// </summary>
    event EventHandler? DiagnosticsChanged;
}

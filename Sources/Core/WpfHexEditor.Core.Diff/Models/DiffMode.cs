// Project      : WpfHexEditorControl
// File         : Models/DiffMode.cs
// Description  : Comparison mode selector used by DiffEngine and DiffModeDetector.
// Architecture : Pure model — no WPF, no I/O.

namespace WpfHexEditor.Core.Diff.Models;

/// <summary>
/// Determines which diff algorithm <see cref="Services.DiffEngine"/> selects.
/// </summary>
public enum DiffMode
{
    /// <summary>Automatically detect the best mode from file extension and content sniff.</summary>
    Auto,

    /// <summary>Byte-level comparison — best for arbitrary binary files.</summary>
    Binary,

    /// <summary>Line-based text comparison using Myers O(ND) — best for source/config files.</summary>
    Text,

    /// <summary>
    /// Format-aware comparison (JSON, XML, C#/XAML) that diffs the parsed structure
    /// and falls back to <see cref="Text"/> on parse failure.
    /// </summary>
    Semantic
}

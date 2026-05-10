// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Suppressions/SuppressionMode.cs
// Description: Pyramid of suppression scopes: narrowest (inline at the
//              code construct) to broadest (rule disabled solution-wide).
// ==========================================================

namespace WpfHexEditor.App.Analysis.Suppressions;

internal enum SuppressionMode
{
    /// <summary>Insert `// CodeAnalysis: suppress WHxxxx` on the line above.</summary>
    InSource,

    /// <summary>Insert a file-scope `// CodeAnalysis: suppress-file WHxxxx` marker at line 1.</summary>
    InFile,

    /// <summary>Append the diagnostic identity to .ide/analysis-baseline.json.</summary>
    InBaseline,

    /// <summary>Set rule severity = Disabled in CodeAnalysisOptions.</summary>
    DisableRule,
}

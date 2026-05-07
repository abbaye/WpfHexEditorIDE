// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Models/AnalysisScope.cs
// Description: Defines the scope of a code analysis run.
// ==========================================================

namespace WpfHexEditor.App.Analysis.Models;

/// <summary>Defines how wide a code analysis run should be.</summary>
public enum AnalysisScope
{
    Solution,
    Project,
    File
}

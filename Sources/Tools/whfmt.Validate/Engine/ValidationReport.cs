// ==========================================================
// Project: whfmt.Validate
// File: Engine/ValidationReport.cs
// Description: Immutable result model for a single file validation run.
// ==========================================================

namespace WhfmtValidate.Engine;

internal sealed class ValidationReport
{
    public string FilePath       { get; init; } = "";
    public string FileName       { get; init; } = "";
    public long   FileSize       { get; init; }
    public string FormatName     { get; set;  } = "Unknown";
    public string FormatCategory { get; set;  } = "-";
    public double Confidence     { get; set;  }
    public string MatchSource    { get; set;  } = "None";
    public string ForensicRiskLevel { get; set; } = "low";
    public bool   FileNotFound   { get; private set; }

    public List<ValidationCheck> Checks { get; } = [];
    public List<ValidationIssue> Issues { get; } = [];

    public int ErrorCount   => Issues.Count(i => i.Severity == "error");
    public int WarningCount => Issues.Count(i => i.Severity == "warning");
    public int InfoCount    => Issues.Count(i => i.Severity == "info");
    public bool IsValid     => ErrorCount == 0;

    internal static ValidationReport NotFound(string path) => new()
    {
        FilePath    = path,
        FileName    = Path.GetFileName(path),
        FileNotFound = true
    };
}

internal sealed class ValidationCheck
{
    public string Category { get; init; } = "";
    public string Name     { get; init; } = "";
    public bool   Passed   { get; init; }
    public string Detail   { get; init; } = "";
}

internal sealed class ValidationIssue
{
    public string Severity { get; init; } = "warning";
    public string Category { get; init; } = "";
    public string Name     { get; init; } = "";
    public string Message  { get; init; } = "";
}

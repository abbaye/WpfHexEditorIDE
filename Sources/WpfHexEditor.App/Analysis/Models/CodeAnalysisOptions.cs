// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Models/CodeAnalysisOptions.cs
// Description: User preferences for the Code Analysis module (persisted as JSON).
// ==========================================================

namespace WpfHexEditor.App.Analysis.Models;

public sealed class CodeAnalysisOptions
{
    // -- General ----------------------------------------------------------
    public bool RunOnSolutionOpen       { get; set; } = false;
    public bool RunOnBuild              { get; set; } = false;
    public bool ShowStatusBarBadge      { get; set; } = true;
    public bool PushToErrorPanel        { get; set; } = true;
    public bool IncludeGeneratedFiles   { get; set; } = false;
    public int  SnapshotRetentionDays   { get; set; } = 30;

    /// <summary>Silent / Normal / Verbose</summary>
    public string OutputVerbosity       { get; set; } = "Normal";

    // -- Thresholds -------------------------------------------------------
    public int  CcWarning               { get; set; } = 10;
    public int  CcError                 { get; set; } = 20;
    public int  CognitiveWarning        { get; set; } = 15;
    public int  CognitiveError          { get; set; } = 30;
    public int  MethodLocWarning        { get; set; } = 25;
    public int  MethodLocError          { get; set; } = 50;
    public int  FileLocWarning          { get; set; } = 300;
    public int  FileLocError            { get; set; } = 600;
    public int  DupMinTokens            { get; set; } = 50;
    public int  DupPercentWarning       { get; set; } = 5;
    public int  DupPercentError         { get; set; } = 15;
    public int  MaxParamsWarning        { get; set; } = 5;
    public int  MaxParamsError          { get; set; } = 8;
    public int  DitWarning              { get; set; } = 4;
    public int  DitError                { get; set; } = 7;

    // -- Rules ------------------------------------------------------------
    public List<RuleConfiguration> Rules { get; set; } = CodeAnalysisOptions.DefaultRules();

    public RuleConfiguration? GetRule(string ruleId)
        => Rules.FirstOrDefault(r => r.RuleId == ruleId);

    public bool IsRuleEnabled(string ruleId)
        => GetRule(ruleId)?.IsEnabled == true;

    public static List<RuleConfiguration> DefaultRules() =>
    [
        new() { RuleId = "WH0001", Description = "Cyclomatic complexity exceeded",  Severity = RuleSeverity.Warning },
        new() { RuleId = "WH0002", Description = "Cognitive complexity exceeded",   Severity = RuleSeverity.Warning },
        new() { RuleId = "WH0003", Description = "Method too long",                 Severity = RuleSeverity.Warning },
        new() { RuleId = "WH0004", Description = "File too long",                   Severity = RuleSeverity.Info    },
        new() { RuleId = "WH0005", Description = "Too many parameters",             Severity = RuleSeverity.Warning },
        new() { RuleId = "WH0006", Description = "Deep inheritance",                Severity = RuleSeverity.Warning },
        new() { RuleId = "WH0010", Description = "Dead private member",             Severity = RuleSeverity.Warning },
        new() { RuleId = "WH0011", Description = "Dead internal member",            Severity = RuleSeverity.Info    },
        new() { RuleId = "WH0012", Description = "Unused parameter",                Severity = RuleSeverity.Info    },
        new() { RuleId = "WH0013", Description = "Unused local variable",           Severity = RuleSeverity.Info    },
        new() { RuleId = "WH0020", Description = "Duplication clone detected",      Severity = RuleSeverity.Warning },
        new() { RuleId = "WH0030", Description = "Naming convention violation",     Severity = RuleSeverity.Info    },
        new() { RuleId = "WH0031", Description = "File/class name mismatch",        Severity = RuleSeverity.Warning },
        new() { RuleId = "WH0032", Description = "TODO/FIXME/HACK marker",          Severity = RuleSeverity.Info    },
        new() { RuleId = "WH0040", Description = "High instability (Ce/Ca)",        Severity = RuleSeverity.Info    },
        new() { RuleId = "WH0041", Description = "High LCOM",                       Severity = RuleSeverity.Info    },
    ];
}

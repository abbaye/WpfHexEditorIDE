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
    public int  NocWarning              { get; set; } = 8;
    public int  NocError                { get; set; } = 15;
    public int  CommentDensityMinPct    { get; set; } = 5;   // below this → undocumented (Info)
    public int  CommentDensityMaxPct    { get; set; } = 40;  // above this → over-commented (Info)
    public int  MaintainabilityWarning  { get; set; } = 65;  // MS standard: yellow zone
    public int  MaintainabilityError    { get; set; } = 50;  // MS standard: red zone
    public int  HalsteadEffortWarning   { get; set; } = 1_000_000;
    public int  HalsteadEffortError     { get; set; } = 5_000_000;
    public int  LcomWarning             { get; set; } = 2;   // > 1 → multiple cohesion components
    public int  LcomError               { get; set; } = 5;   // god class candidate

    // -- Quality Gate (Phase 6) ------------------------------------------
    public bool QualityGateEnabled            { get; set; } = false;
    public int  QualityGateMinScore           { get; set; } = 70;
    public int  QualityGateMaxNegativeDelta   { get; set; } = -5;   // fail if score drops more than 5
    public int  QualityGateMaxErrors          { get; set; } = 0;

    // -- Baseline & Suppress (Phase 6) -----------------------------------
    public bool BaselineEnabled               { get; set; } = false;
    public bool RespectInlineSuppress         { get; set; } = true;

    // -- AI Insights (Phase 8) -------------------------------------------
    public bool   AiInsightsEnabled           { get; set; } = false;
    public string AiInsightsEndpoint          { get; set; } = "https://api.openai.com/v1/chat/completions";
    public string AiInsightsModel             { get; set; } = "gpt-4o-mini";
    public string AiInsightsApiKey            { get; set; } = string.Empty;

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
        new() { RuleId = "WH0033", Description = "Comment density too low",         Severity = RuleSeverity.Info    },
        new() { RuleId = "WH0034", Description = "Comment density too high",        Severity = RuleSeverity.Info    },
        new() { RuleId = "WH0040", Description = "High instability (Ce/Ca)",        Severity = RuleSeverity.Info    },
        new() { RuleId = "WH0041", Description = "High LCOM (cohesion)",            Severity = RuleSeverity.Info    },
        new() { RuleId = "WH0042", Description = "God class candidate (LCOM > 5)",  Severity = RuleSeverity.Warning },
        new() { RuleId = "WH0043", Description = "Feature envy",                    Severity = RuleSeverity.Info    },
        new() { RuleId = "WH0044", Description = "Data clump (≥3 params, ≥3 sites)",Severity = RuleSeverity.Info    },
        new() { RuleId = "WH0050", Description = "Cyclic project dependency",       Severity = RuleSeverity.Error   },
        new() { RuleId = "WH0051", Description = "Number of children too high",     Severity = RuleSeverity.Info    },
        new() { RuleId = "WH0052", Description = "Maintainability index low",       Severity = RuleSeverity.Warning },
        new() { RuleId = "WH0053", Description = "Halstead effort too high",        Severity = RuleSeverity.Info    },
        new() { RuleId = "WH0060", Description = "Async: .Result/.Wait() blocking", Severity = RuleSeverity.Warning },
        new() { RuleId = "WH0061", Description = "Async: async void method",        Severity = RuleSeverity.Warning },
        new() { RuleId = "WH0062", Description = "Async: missing ConfigureAwait",   Severity = RuleSeverity.Info    },
        new() { RuleId = "WH0070", Description = "LINQ: use Any() instead of Count()>0", Severity = RuleSeverity.Info },
        new() { RuleId = "WH0071", Description = "LINQ: combine Where+First",       Severity = RuleSeverity.Info    },
        new() { RuleId = "WH0072", Description = "LINQ: multiple enumeration",      Severity = RuleSeverity.Warning },
    ];
}

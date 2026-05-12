// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Models/RuleCategory.cs
// Description: Categorical bucket for the 35 WH00xx rules. Used by the
//              Options page TreeView and the SARIF metadata exporter.
// ==========================================================

namespace WpfHexEditor.App.Analysis.Models;

public enum RuleCategory
{
    Complexity,
    DeadCode,
    Duplication,
    Conventions,
    Architecture,
    Project,
    AsyncCode,
    Linq,
}

internal static class RuleCategoryHelper
{
    /// <summary>Map a rule id (WH00xx) to its canonical category. Single source of
    /// truth lives in RuleMetadata; this helper just dispatches into it so users
    /// don't have to know which lookup table to call.</summary>
    internal static RuleCategory FromRuleId(string ruleId)
        => RuleMetadata.Get(ruleId)?.Category ?? RuleCategory.Conventions;
}

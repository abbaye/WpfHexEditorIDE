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
    /// <summary>Map a rule id (WH00xx) to its canonical category — used on JSON load
    /// when the user's serialized options predate the Category field.</summary>
    internal static RuleCategory FromRuleId(string ruleId) => ruleId switch
    {
        "WH0001" or "WH0002" or "WH0003" or "WH0004" or "WH0005" or "WH0006" => RuleCategory.Complexity,
        "WH0010" or "WH0011" or "WH0012" or "WH0013"                          => RuleCategory.DeadCode,
        "WH0020"                                                              => RuleCategory.Duplication,
        "WH0030" or "WH0031" or "WH0032" or "WH0033" or "WH0034"              => RuleCategory.Conventions,
        "WH0040" or "WH0041" or "WH0042" or "WH0043" or "WH0044"              => RuleCategory.Architecture,
        "WH0050" or "WH0051" or "WH0052" or "WH0053"                          => RuleCategory.Project,
        "WH0060" or "WH0061" or "WH0062"                                      => RuleCategory.AsyncCode,
        "WH0070" or "WH0071" or "WH0072"                                      => RuleCategory.Linq,
        _                                                                     => RuleCategory.Conventions,
    };
}

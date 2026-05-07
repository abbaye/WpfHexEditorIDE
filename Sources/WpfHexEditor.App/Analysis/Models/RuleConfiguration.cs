// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Models/RuleConfiguration.cs
// Description: User-configurable severity override for a single analysis rule.
// ==========================================================

namespace WpfHexEditor.App.Analysis.Models;

public enum RuleSeverity { Disabled, Info, Warning, Error }

public sealed class RuleConfiguration
{
    public string       RuleId      { get; set; } = string.Empty;
    public string       Description { get; set; } = string.Empty;
    public RuleSeverity Severity    { get; set; } = RuleSeverity.Warning;
    public bool         IsEnabled   => Severity != RuleSeverity.Disabled;
}

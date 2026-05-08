// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Models/RuleIds.cs
// Description: Central registry of every Code Analysis rule id (WH00xx).
//              Avoids stringly-typed call sites and silent typos that disable
//              rules.
// ==========================================================

namespace WpfHexEditor.App.Analysis.Models;

internal static class RuleIds
{
    // Complexity & size
    public const string CyclomaticComplexity   = "WH0001";
    public const string CognitiveComplexity    = "WH0002";
    public const string MethodTooLong          = "WH0003";
    public const string FileTooLong            = "WH0004";
    public const string TooManyParameters      = "WH0005";
    public const string DeepInheritance        = "WH0006";

    // Dead code
    public const string DeadPrivate            = "WH0010";
    public const string DeadInternal           = "WH0011";
    public const string UnusedParameter        = "WH0012";
    public const string UnusedLocal            = "WH0013";

    // Duplication
    public const string DuplicationClone       = "WH0020";

    // Conventions
    public const string Naming                 = "WH0030";
    public const string FileClassMismatch      = "WH0031";
    public const string TodoMarker             = "WH0032";
    public const string CommentDensityLow      = "WH0033";
    public const string CommentDensityHigh     = "WH0034";

    // Architecture & cohesion
    public const string HighInstability        = "WH0040";
    public const string LowCohesion            = "WH0041";
    public const string GodClass               = "WH0042";
    public const string FeatureEnvy            = "WH0043";
    public const string DataClump              = "WH0044";

    // Project-level
    public const string CyclicDependency       = "WH0050";
    public const string TooManyChildren        = "WH0051";
    public const string LowMaintainability     = "WH0052";
    public const string HighHalsteadEffort     = "WH0053";

    // Async
    public const string AsyncBlocking          = "WH0060";
    public const string AsyncVoid              = "WH0061";
    public const string MissingConfigureAwait  = "WH0062";

    // LINQ
    public const string LinqCountVsAny         = "WH0070";
    public const string LinqWhereFirst         = "WH0071";
    public const string LinqMultipleEnumeration = "WH0072";
}

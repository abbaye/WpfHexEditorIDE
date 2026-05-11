// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Models/RuleMetadata.cs
// Description: Per-rule descriptive metadata: human-readable name, short and
//              full descriptions, help URI, default level, tags. Consumed by
//              SarifExporter (Phase 11 — rules[] array) and the "Open rule
//              help" context menu item.
// ==========================================================

namespace WpfHexEditor.App.Analysis.Models;

internal sealed record RuleInfo(
    string Name,
    string ShortDescription,
    string FullDescription,
    string HelpUri,
    RuleSeverity DefaultLevel,
    IReadOnlyList<string> Tags);

internal static class RuleMetadata
{
    private const string DocsRoot = "https://github.com/abbaye/WPFHexaEditor/blob/master/docs/rules";

    internal static readonly Dictionary<string, RuleInfo> Map = new()
    {
        // Complexity
        ["WH0001"] = new("CyclomaticComplexity",
            "Method has high cyclomatic complexity.",
            "The number of independent decision paths in this method exceeds the configured threshold. High complexity makes the method hard to test, review, and reason about. Consider extracting helper methods or replacing nested branches with table-driven dispatch.",
            $"{DocsRoot}/WH0001.md", RuleSeverity.Warning, ["maintainability", "complexity"]),
        ["WH0002"] = new("CognitiveComplexity",
            "Method has high cognitive complexity.",
            "Cognitive complexity weights nested control flow more heavily than cyclomatic complexity and reflects the mental effort to follow the code. Flatten nesting and prefer early returns.",
            $"{DocsRoot}/WH0002.md", RuleSeverity.Warning, ["maintainability", "complexity"]),
        ["WH0003"] = new("MethodTooLong",
            "Method body is longer than configured.",
            "Long methods usually do several things; splitting them by responsibility improves testability and readability.",
            $"{DocsRoot}/WH0003.md", RuleSeverity.Warning, ["maintainability"]),
        ["WH0004"] = new("FileTooLong",
            "File is longer than configured.",
            "Oversized files indicate a class doing too much or unrelated classes living together. Consider splitting by feature or responsibility.",
            $"{DocsRoot}/WH0004.md", RuleSeverity.Info, ["maintainability"]),
        ["WH0005"] = new("TooManyParameters",
            "Method has too many parameters.",
            "Long parameter lists suggest the method needs a parameter object or that several concerns are bundled. Group related parameters into a small record.",
            $"{DocsRoot}/WH0005.md", RuleSeverity.Warning, ["api-design"]),
        ["WH0006"] = new("DeepInheritance",
            "Type sits deep in an inheritance chain.",
            "Deep hierarchies are fragile under change. Prefer composition or sealed types when possible.",
            $"{DocsRoot}/WH0006.md", RuleSeverity.Warning, ["design"]),

        // Dead code
        ["WH0010"] = new("DeadPrivate",
            "Private member is never referenced.",
            "The private declaration has no in-project references. Either remove it or, if it is touched only via reflection or InternalsVisibleTo, add a suppress marker.",
            $"{DocsRoot}/WH0010.md", RuleSeverity.Warning, ["dead-code"]),
        ["WH0011"] = new("DeadInternal",
            "Internal member is never referenced.",
            "Internal declarations without callers across the assembly. Investigate before removing — InternalsVisibleTo or reflection may justify the symbol.",
            $"{DocsRoot}/WH0011.md", RuleSeverity.Info, ["dead-code"]),
        ["WH0012"] = new("UnusedParameter",
            "Method parameter is never used.",
            "Unused parameters either indicate a removed feature or a hidden bug. Remove or rename to `_` to mark intent.",
            $"{DocsRoot}/WH0012.md", RuleSeverity.Info, ["dead-code"]),
        ["WH0013"] = new("UnusedLocal",
            "Local variable is declared but never used.",
            "Dead locals waste readability budget. Remove the declaration.",
            $"{DocsRoot}/WH0013.md", RuleSeverity.Info, ["dead-code"]),

        // Duplication
        ["WH0020"] = new("DuplicationClone",
            "Duplicated code block detected.",
            "A near-identical token sequence appears in 2+ locations. Extract the common shape into a helper method or shared base class.",
            $"{DocsRoot}/WH0020.md", RuleSeverity.Warning, ["maintainability", "duplication"]),

        // Conventions
        ["WH0030"] = new("Naming",
            "Identifier breaks a naming convention.",
            "Symbol names should follow .NET conventions (PascalCase for types/methods, camelCase for parameters/locals, _camelCase for private fields).",
            $"{DocsRoot}/WH0030.md", RuleSeverity.Info, ["convention"]),
        ["WH0031"] = new("FileClassMismatch",
            "File name does not match the contained type.",
            "Convention is one top-level type per file, with the file named after the type. Easier to navigate, easier to grep.",
            $"{DocsRoot}/WH0031.md", RuleSeverity.Warning, ["convention"]),
        ["WH0032"] = new("TodoMarker",
            "TODO / FIXME / HACK marker in source.",
            "Markers are reminders; if they survive a release they become noise. Convert to issues or address them.",
            $"{DocsRoot}/WH0032.md", RuleSeverity.Info, ["convention"]),
        ["WH0033"] = new("CommentDensityLow",
            "File has very few comments relative to code.",
            "Some files (DTOs, generated code) legitimately have no comments — verify whether documentation is missing.",
            $"{DocsRoot}/WH0033.md", RuleSeverity.Info, ["documentation"]),
        ["WH0034"] = new("CommentDensityHigh",
            "File has more comments than code.",
            "Excess commentary often signals over-documented or unclear code. Prefer clearer identifiers over explanatory comments.",
            $"{DocsRoot}/WH0034.md", RuleSeverity.Info, ["documentation"]),

        // Architecture
        ["WH0040"] = new("HighInstability",
            "Type is highly unstable (Ce/Ca ratio).",
            "Highly unstable types depend on many others but are depended on by few; changes ripple downstream. Stabilize by extracting interfaces.",
            $"{DocsRoot}/WH0040.md", RuleSeverity.Info, ["architecture"]),
        ["WH0041"] = new("LowCohesion",
            "Type has low cohesion (LCOM).",
            "Members operate on disjoint state — the type likely bundles multiple responsibilities. Consider splitting.",
            $"{DocsRoot}/WH0041.md", RuleSeverity.Info, ["architecture"]),
        ["WH0042"] = new("GodClass",
            "God-class candidate (LCOM > 5).",
            "Very low cohesion combined with size — this type owns too many responsibilities.",
            $"{DocsRoot}/WH0042.md", RuleSeverity.Warning, ["architecture"]),
        ["WH0043"] = new("FeatureEnvy",
            "Method talks more to another type than its own.",
            "When a method's body chains many calls to a single foreign type, the behavior may belong on that type instead.",
            $"{DocsRoot}/WH0043.md", RuleSeverity.Info, ["architecture"]),
        ["WH0044"] = new("DataClump",
            "Same parameter triplet appears in multiple sites.",
            "A recurring (≥3) parameter group across (≥3) call sites is a missed record/struct.",
            $"{DocsRoot}/WH0044.md", RuleSeverity.Info, ["architecture"]),

        // Project
        ["WH0050"] = new("CyclicDependency",
            "Cyclic project dependency.",
            "Projects depend on each other through a cycle. Cycles break layered architectures and complicate testing.",
            $"{DocsRoot}/WH0050.md", RuleSeverity.Error, ["architecture", "project"]),
        ["WH0051"] = new("TooManyChildren",
            "Type has too many direct subclasses.",
            "Wide inheritance trees are hard to refactor. Prefer composition or strategy delegation.",
            $"{DocsRoot}/WH0051.md", RuleSeverity.Info, ["design"]),
        ["WH0052"] = new("LowMaintainability",
            "Maintainability index below threshold.",
            "Microsoft's MI combines Halstead, complexity and LOC. Low MI predicts bug density.",
            $"{DocsRoot}/WH0052.md", RuleSeverity.Warning, ["maintainability"]),
        ["WH0053"] = new("HighHalsteadEffort",
            "Halstead effort exceeds threshold.",
            "Effort estimates the mental work to read or write code, derived from operator/operand counts.",
            $"{DocsRoot}/WH0053.md", RuleSeverity.Info, ["maintainability"]),

        // Async
        ["WH0060"] = new("AsyncBlocking",
            "Synchronous wait on async code.",
            ".Result and .Wait() can deadlock under a synchronization context (WPF/ASP.NET). Use await.",
            $"{DocsRoot}/WH0060.md", RuleSeverity.Warning, ["async", "correctness"]),
        ["WH0061"] = new("AsyncVoid",
            "Method is async void.",
            "async void methods can't be awaited and swallow exceptions. Use async Task unless the signature is forced (event handler).",
            $"{DocsRoot}/WH0061.md", RuleSeverity.Warning, ["async", "correctness"]),
        ["WH0062"] = new("MissingConfigureAwait",
            "Awaited task is missing ConfigureAwait.",
            "In library code, always append .ConfigureAwait(false) to keep the continuation off the captured context (avoids deadlocks).",
            $"{DocsRoot}/WH0062.md", RuleSeverity.Info, ["async"]),

        // LINQ
        ["WH0070"] = new("LinqCountVsAny",
            "Count() > 0 should be Any().",
            "Count() enumerates the whole sequence; Any() short-circuits on the first element.",
            $"{DocsRoot}/WH0070.md", RuleSeverity.Info, ["linq", "performance"]),
        ["WH0071"] = new("LinqWhereFirst",
            "Where(...).First() can be First(...).",
            "Combining the predicate into First avoids one allocation of the Where iterator.",
            $"{DocsRoot}/WH0071.md", RuleSeverity.Info, ["linq", "performance"]),
        ["WH0072"] = new("LinqMultipleEnumeration",
            "IEnumerable is enumerated multiple times.",
            "Multiple foreach over the same IEnumerable re-evaluates the source. Materialize to a List/array if the source is expensive.",
            $"{DocsRoot}/WH0072.md", RuleSeverity.Warning, ["linq", "performance"]),
    };

    internal static RuleInfo? Get(string ruleId)
        => Map.TryGetValue(ruleId, out var info) ? info : null;
}

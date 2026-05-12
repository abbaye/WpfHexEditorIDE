//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7 (1M context)
//////////////////////////////////////////////
// Project: WpfHexEditor.Core.Definitions
// File: Models/Validation/WhfmtExpressionValidator.cs
// Description: R10 — static validation of whfmt expression strings.
//              Parses each expression at build time, then walks the AST to
//              verify every variable reference is declared in variables{}
//              and every function call is known to the function registry.
// Architecture notes (per Option A design discussion):
//              Catches whole-class-of-bugs without changing the JSON format —
//              expressions stay strings, validation happens once at build.
//////////////////////////////////////////////

using System.Text.Json;
using WpfHexEditor.Core.Definitions.Models.Expressions;
using WpfHexEditor.Core.Definitions.Models.Functions;

namespace WpfHexEditor.Core.Definitions.Models.Validation;

/// <summary>
/// R10 — validates the expression strings inside a whfmt document.
/// Returns a list of <see cref="WhfmtValidationIssue"/> for every problem.
/// </summary>
public static class WhfmtExpressionValidator
{
    /// <summary>
    /// Validates every expression-bearing field in <paramref name="whfmtJson"/>:
    /// <c>assertions[].expression</c>, <c>blocks[].expression</c>,
    /// <c>blocks[].condition</c>, <c>forensic.suspiciousPatterns[].condition</c>.
    /// Optional <paramref name="extraFunctions"/> add to the default registry.
    /// </summary>
    public static IReadOnlyList<WhfmtValidationIssue> Validate(
        string whfmtJson,
        WhfmtFunctionRegistry? functions = null)
    {
        var issues = new List<WhfmtValidationIssue>();
        functions ??= WhfmtFunctionRegistry.CreateDefault();

        JsonDocument doc;
        try { doc = JsonDocument.Parse(whfmtJson, WhfmtJsonOptions.Jsonc); }
        catch (JsonException ex)
        {
            issues.Add(new WhfmtValidationIssue(
                "R10-000", $"Document is not valid JSONC: {ex.Message}",
                WhfmtIssueSeverity.Error, null, null, -1));
            return issues;
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return issues;

            // Collect declared variable names — both schemas
            var declaredVars = CollectDeclaredVariables(root);

            // Collect declared function names (entries in functions{})
            var declaredFns = CollectDeclaredFunctions(root);

            // Walk every known expression-bearing location
            CheckAssertions(root, declaredVars, declaredFns, functions, issues);
            CheckBlocks(root, declaredVars, declaredFns, functions, issues);
            CheckForensic(root, declaredVars, declaredFns, functions, issues);
        }

        return issues;
    }

    // -- Collectors -----------------------------------------------------------

    private static HashSet<string> CollectDeclaredVariables(JsonElement root)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            foreach (var def in WhfmtVariableParser.ParseElement(root))
                set.Add(def.Name);
        }
        catch { /* malformed variables — surface as a separate issue elsewhere */ }
        return set;
    }

    private static HashSet<string> CollectDeclaredFunctions(JsonElement root)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (root.TryGetProperty("functions", out var fns) && fns.ValueKind == JsonValueKind.Object)
            foreach (var p in fns.EnumerateObject()) set.Add(p.Name);
        return set;
    }

    // -- Per-section checks ---------------------------------------------------

    private static void CheckAssertions(
        JsonElement root, HashSet<string> vars, HashSet<string> declaredFns,
        WhfmtFunctionRegistry fns, List<WhfmtValidationIssue> issues)
    {
        if (!root.TryGetProperty("assertions", out var arr) || arr.ValueKind != JsonValueKind.Array) return;
        int i = 0;
        foreach (var a in arr.EnumerateArray())
        {
            if (a.TryGetProperty("expression", out var e) && e.ValueKind == JsonValueKind.String)
                ValidateOne(e.GetString()!, $"assertions[{i}].expression", vars, declaredFns, fns, issues);
            i++;
        }
    }

    private static void CheckBlocks(
        JsonElement root, HashSet<string> vars, HashSet<string> declaredFns,
        WhfmtFunctionRegistry fns, List<WhfmtValidationIssue> issues)
    {
        if (!root.TryGetProperty("blocks", out var arr) || arr.ValueKind != JsonValueKind.Array) return;
        int i = 0;
        foreach (var b in arr.EnumerateArray())
        {
            if (b.TryGetProperty("expression", out var e) && e.ValueKind == JsonValueKind.String)
                ValidateOne(e.GetString()!, $"blocks[{i}].expression", vars, declaredFns, fns, issues);
            if (b.TryGetProperty("condition", out var c) && c.ValueKind == JsonValueKind.String)
                ValidateOne(c.GetString()!, $"blocks[{i}].condition", vars, declaredFns, fns, issues);
            i++;
        }
    }

    private static void CheckForensic(
        JsonElement root, HashSet<string> vars, HashSet<string> declaredFns,
        WhfmtFunctionRegistry fns, List<WhfmtValidationIssue> issues)
    {
        if (!root.TryGetProperty("forensic", out var f)) return;
        if (!f.TryGetProperty("suspiciousPatterns", out var arr) || arr.ValueKind != JsonValueKind.Array) return;
        int i = 0;
        foreach (var p in arr.EnumerateArray())
        {
            if (p.ValueKind == JsonValueKind.Object
                && p.TryGetProperty("condition", out var c)
                && c.ValueKind == JsonValueKind.String)
            {
                ValidateOne(c.GetString()!, $"forensic.suspiciousPatterns[{i}].condition",
                    vars, declaredFns, fns, issues);
            }
            i++;
        }
    }

    // -- Single-expression validation -----------------------------------------

    private static void ValidateOne(
        string source, string path,
        HashSet<string> declaredVars, HashSet<string> declaredFns,
        WhfmtFunctionRegistry fnRegistry, List<WhfmtValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(source)) return;

        WhfmtExprNode ast;
        try { ast = WhfmtExpressionParser.Parse(source); }
        catch (WhfmtExpressionException ex)
        {
            issues.Add(new WhfmtValidationIssue(
                "R10-001", $"Expression parse error: {ex.Message}",
                WhfmtIssueSeverity.Error, path, source, ex.Position));
            return;
        }

        WalkAst(ast, source, path, declaredVars, declaredFns, fnRegistry, issues);
    }

    // -- AST walker — records issues for undeclared vars / unknown fns -------

    private static void WalkAst(
        WhfmtExprNode node, string source, string path,
        HashSet<string> declaredVars, HashSet<string> declaredFns,
        WhfmtFunctionRegistry fnRegistry, List<WhfmtValidationIssue> issues)
    {
        switch (node)
        {
            case IdentifierNode id:
                if (!declaredVars.Contains(id.Name) && !declaredFns.Contains(id.Name)
                    && !fnRegistry.TryGet(id.Name, out _))
                {
                    issues.Add(new WhfmtValidationIssue(
                        "R10-002",
                        $"Undeclared identifier '{id.Name}' — not in variables{{}} or functions{{}}",
                        WhfmtIssueSeverity.Error, path, source, -1));
                }
                break;
            case UnaryNode u:   WalkAst(u.Operand, source, path, declaredVars, declaredFns, fnRegistry, issues); break;
            case BinaryNode b:
                WalkAst(b.Left,  source, path, declaredVars, declaredFns, fnRegistry, issues);
                WalkAst(b.Right, source, path, declaredVars, declaredFns, fnRegistry, issues);
                break;
            case TernaryNode t:
                WalkAst(t.Cond, source, path, declaredVars, declaredFns, fnRegistry, issues);
                WalkAst(t.Then, source, path, declaredVars, declaredFns, fnRegistry, issues);
                WalkAst(t.Else, source, path, declaredVars, declaredFns, fnRegistry, issues);
                break;
            case IndexNode ix:
                WalkAst(ix.Target, source, path, declaredVars, declaredFns, fnRegistry, issues);
                WalkAst(ix.Index,  source, path, declaredVars, declaredFns, fnRegistry, issues);
                break;
            case MemberNode m:
                // Members are dispatched at eval time on whatever the target is;
                // we don't statically know the receiver's type, so we just walk the target.
                WalkAst(m.Target, source, path, declaredVars, declaredFns, fnRegistry, issues);
                break;
            case CallNode c:
                // Free function call: identifier must resolve to a registered function
                // OR be declared in functions{}.
                if (c.Target is IdentifierNode fnId)
                {
                    if (!declaredFns.Contains(fnId.Name) && !fnRegistry.TryGet(fnId.Name, out _))
                    {
                        issues.Add(new WhfmtValidationIssue(
                            "R10-003",
                            $"Unknown function '{fnId.Name}' — not in functions{{}} and not a built-in",
                            WhfmtIssueSeverity.Error, path, source, -1));
                    }
                }
                else
                {
                    // Method calls on dynamic receivers — walk target, args
                    WalkAst(c.Target, source, path, declaredVars, declaredFns, fnRegistry, issues);
                }
                foreach (var a in c.Args)
                    WalkAst(a, source, path, declaredVars, declaredFns, fnRegistry, issues);
                break;
        }
    }
}

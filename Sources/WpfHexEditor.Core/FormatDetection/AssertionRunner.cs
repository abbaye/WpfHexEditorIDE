// ==========================================================
// Project: WpfHexEditor.Core
// File: AssertionRunner.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-25
// Description:
//     Evaluates the `assertions` array from a .whfmt v2.0 format definition
//     against resolved variables, using ExpressionEvaluator for boolean
//     expression evaluation.
//
// Architecture Notes:
//     Stateless.  Each assertion is a boolean expression over named variables.
//     Results carry severity (error/warning/info) and the message from the
//     definition or a default.  No WPF dependencies.
// ==========================================================

using System;
using System.Collections.Generic;

namespace WpfHexEditor.Core.FormatDetection
{
    /// <summary>
    /// Result of evaluating one assertion.
    /// </summary>
    public sealed class AssertionResult
    {
        /// <summary>Assertion name from definition.</summary>
        public string Name       { get; init; }

        /// <summary>Whether the assertion passed.</summary>
        public bool   Passed     { get; init; }

        /// <summary>The boolean expression that was evaluated.</summary>
        public string Expression { get; init; }

        /// <summary>Severity: "error" | "warning" | "info" (from definition, default "error").</summary>
        public string Severity   { get; init; }

        /// <summary>Human-readable message (from definition or default).</summary>
        public string Message    { get; init; }
    }

    /// <summary>
    /// Evaluates all assertions in a .whfmt format definition using the
    /// interpreter's final variable state.
    /// </summary>
    public sealed class AssertionRunner
    {
        /// <summary>
        /// Evaluate all assertion definitions against <paramref name="variables"/>.
        /// </summary>
        public IReadOnlyList<AssertionResult> Run(
            IReadOnlyList<AssertionDefinition> definitions,
            IReadOnlyDictionary<string, object> variables)
        {
            if (definitions == null || definitions.Count == 0)
                return Array.Empty<AssertionResult>();

            var results = new List<AssertionResult>(definitions.Count);
            var varsCopy = new Dictionary<string, object>(variables, StringComparer.OrdinalIgnoreCase);

            foreach (var def in definitions)
            {
                try
                {
                    results.Add(EvaluateOne(def, varsCopy));
                }
                catch (Exception ex)
                {
                    results.Add(new AssertionResult
                    {
                        Name       = def.Name ?? "assertion",
                        Passed     = false,
                        Expression = def.Expression,
                        Severity   = def.Severity ?? "error",
                        Message    = $"Assertion evaluation error: {ex.Message}"
                    });
                }
            }

            return results;
        }

        // ── Private ─────────────────────────────────────────────────────────────

        private static AssertionResult EvaluateOne(
            AssertionDefinition def,
            Dictionary<string, object> variables)
        {
            string name     = def.Name ?? "assertion";
            string severity = def.Severity ?? "error";
            string expr     = def.Expression ?? "";

            if (string.IsNullOrWhiteSpace(expr))
            {
                return new AssertionResult
                {
                    Name = name, Passed = true, Expression = expr, Severity = severity
                };
            }

            bool passed = EvaluateBoolean(expr, variables);
            string message = passed
                ? null
                : (def.Message ?? $"Assertion failed: {expr}");

            return new AssertionResult
            {
                Name       = name,
                Passed     = passed,
                Expression = expr,
                Severity   = severity,
                Message    = message
            };
        }

        /// <summary>
        /// Evaluates a boolean expression of the form "lhs op rhs" where op is
        /// one of ==, !=, &gt;, &lt;, &gt;=, &lt;=.
        /// Both sides are evaluated as arithmetic via ExpressionEvaluator.
        /// A bare numeric expression is truthy when != 0.
        /// </summary>
        private static bool EvaluateBoolean(string expression, Dictionary<string, object> variables)
        {
            string[] compOps = { "==", "!=", ">=", "<=", ">", "<" };

            foreach (var op in compOps)
            {
                int idx = expression.IndexOf(op, StringComparison.Ordinal);
                if (idx <= 0) continue;

                string left  = expression.Substring(0, idx).Trim();
                string right = expression.Substring(idx + op.Length).Trim();

                long lv = ExpressionEvaluator.EvaluateStatic(left,  variables);
                long rv = ExpressionEvaluator.EvaluateStatic(right, variables);

                return op switch
                {
                    "==" => lv == rv,
                    "!=" => lv != rv,
                    ">=" => lv >= rv,
                    "<=" => lv <= rv,
                    ">"  => lv >  rv,
                    "<"  => lv <  rv,
                    _    => false
                };
            }

            // No comparison operator — truthy check
            return ExpressionEvaluator.EvaluateStatic(expression, variables) != 0;
        }
    }
}

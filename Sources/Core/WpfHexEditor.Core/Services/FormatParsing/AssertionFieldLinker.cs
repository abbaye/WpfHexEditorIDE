// ==========================================================
// Project: WpfHexEditor.Core
// File: AssertionFieldLinker.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-26
// Description:
//     Links failed assertion results to individual parsed fields by matching
//     variable names in the assertion expression to field StoreAs values.
//     Extracted from HexEditor.ParsedFieldsIntegration.cs for reuse.
//
// Architecture Notes:
//     Static utility — no instance state. Safe to call from any thread.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Core.Services.FormatParsing
{
    /// <summary>
    /// Links failed assertion results to individual parsed field ViewModels.
    /// </summary>
    internal static class AssertionFieldLinker
    {
        private static readonly Regex IdentifierRegex =
            new(@"\b[a-zA-Z_][a-zA-Z0-9_]*\b", RegexOptions.Compiled);

        /// <summary>
        /// Links failed assertion results to individual parsed fields by matching
        /// variable names in the assertion expression to field StoreAs values.
        /// Sets IsValid=false + ValidationMessage on matched fields so the warning icon appears.
        /// </summary>
        public static void ApplyAssertionFailuresToFields(
            List<AssertionResult> failedAssertions,
            ObservableCollection<ParsedFieldViewModel> fields)
        {
            if (failedAssertions == null || failedAssertions.Count == 0 ||
                fields == null || fields.Count == 0)
                return;

            // Build map: variableName → (message, severityRank)
            var varToAssertion = new Dictionary<string, (string message, int rank)>(StringComparer.OrdinalIgnoreCase);

            foreach (var a in failedAssertions)
            {
                if (string.IsNullOrWhiteSpace(a.Expression)) continue;
                int rank = a.Severity switch { "error" => 0, "warning" => 1, _ => 2 };
                string msg = a.Message ?? $"Assertion failed: {a.Name}";

                foreach (Match m in IdentifierRegex.Matches(a.Expression))
                {
                    string varName = m.Value;

                    // Skip hex literal fragments (e.g. the "FF" in "0xFF")
                    if (m.Index >= 2 && a.Expression[m.Index - 1] == 'x' && a.Expression[m.Index - 2] == '0')
                        continue;

                    // Keep only the most severe assertion per variable
                    if (!varToAssertion.TryGetValue(varName, out var existing) || rank < existing.rank)
                        varToAssertion[varName] = (msg, rank);
                }
            }

            if (varToAssertion.Count == 0) return;

            ApplyToFieldTree(fields, varToAssertion);
        }

        private static void ApplyToFieldTree(
            IEnumerable<ParsedFieldViewModel> fields,
            Dictionary<string, (string message, int rank)> varToAssertion)
        {
            foreach (var field in fields)
            {
                var storeAs = field.BlockDefinition?.StoreAs;
                if (!string.IsNullOrWhiteSpace(storeAs) && varToAssertion.TryGetValue(storeAs, out var info))
                {
                    if (field.IsValid)
                    {
                        field.IsValid = false;
                        field.ValidationMessage = info.message;
                    }
                    else
                    {
                        field.ValidationMessage = field.ValidationMessage + "\n" + info.message;
                    }
                }

                // Recurse into children (repeating groups, conditionals, loops)
                if (field.ChildItems?.Count > 0)
                    ApplyToFieldTree(field.ChildItems, varToAssertion);
            }
        }
    }
}

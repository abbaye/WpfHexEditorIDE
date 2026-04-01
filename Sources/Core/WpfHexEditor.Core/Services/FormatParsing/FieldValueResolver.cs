// ==========================================================
// Project: WpfHexEditor.Core
// File: FieldValueResolver.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-26
// Description:
//     Resolves offset and length values from whfmt block definitions.
//     Handles int, long, var:name, calc:expression, and JsonElement values.
//     Extracted from HexEditor.ParsedFieldsIntegration.cs for reuse.
//
// Architecture Notes:
//     Stateless resolver — takes VariableContext and ExpressionEvaluator as params.
// ==========================================================

using System;
using System.Text.Json;
using WpfHexEditor.Core.FormatDetection;

namespace WpfHexEditor.Core.Services.FormatParsing
{
    /// <summary>
    /// Resolves whfmt offset/length values from various representations.
    /// </summary>
    internal static class FieldValueResolver
    {
        /// <summary>
        /// Resolve an offset value (handle int, var:name, calc:expression, JsonElement).
        /// </summary>
        public static long ResolveOffset(
            object offsetValue,
            VariableContext? variableContext,
            ExpressionEvaluator? expressionEvaluator)
        {
            return offsetValue switch
            {
                int intOffset => intOffset,
                long longOffset => longOffset,
                JsonElement jsonElement => ResolveJsonElementAsLong(jsonElement, variableContext, expressionEvaluator),
                string strOffset when strOffset.StartsWith("var:") =>
                    variableContext?.GetVariableAsLong(strOffset.Substring(4)) ?? 0,
                string strOffset when strOffset.StartsWith("calc:") =>
                    expressionEvaluator?.Evaluate(strOffset.Substring(5)) ?? 0,
                _ => 0
            };
        }

        /// <summary>
        /// Resolve a length value (handle int, var:name, calc:expression, JsonElement).
        /// </summary>
        public static int ResolveLength(
            object lengthValue,
            VariableContext? variableContext,
            ExpressionEvaluator? expressionEvaluator)
        {
            return lengthValue switch
            {
                int intLength => intLength,
                long longLength => (int)longLength,
                JsonElement jsonElement => (int)ResolveJsonElementAsLong(jsonElement, variableContext, expressionEvaluator),
                string strLength when strLength.StartsWith("var:") =>
                    (int)(variableContext?.GetVariableAsLong(strLength.Substring(4)) ?? 0),
                string strLength when strLength.StartsWith("calc:") =>
                    (int)(expressionEvaluator?.Evaluate(strLength.Substring(5)) ?? 0),
                _ => 1
            };
        }

        /// <summary>
        /// Resolve a JsonElement to a long value.
        /// Handles numbers and strings (including var: and calc: prefixes).
        /// </summary>
        public static long ResolveJsonElementAsLong(
            JsonElement element,
            VariableContext? variableContext,
            ExpressionEvaluator? expressionEvaluator)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Number:
                    return element.TryGetInt64(out long longValue) ? longValue :
                           element.TryGetInt32(out int intValue) ? intValue : 0;

                case JsonValueKind.String:
                    string strValue = element.GetString();
                    if (strValue.StartsWith("var:"))
                        return variableContext?.GetVariableAsLong(strValue.Substring(4)) ?? 0;
                    else if (strValue.StartsWith("calc:"))
                        return expressionEvaluator?.Evaluate(strValue.Substring(5)) ?? 0;
                    else if (long.TryParse(strValue, out long parsed))
                        return parsed;
                    return 0;

                default:
                    return 0;
            }
        }
    }
}

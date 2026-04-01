// ==========================================================
// Project: WpfHexEditor.Core
// File: ConditionEvaluator.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-26
// Description:
//     Evaluates conditional expressions from whfmt block definitions.
//     Supports ConditionDefinition objects and string-based expressions.
//     Extracted from HexEditor.ParsedFieldsIntegration.cs for reuse.
//
// Architecture Notes:
//     Requires IBinaryDataSource for offset: reads in ConditionDefinition.
//     Stateless methods — takes context objects as parameters.
// ==========================================================

using System;
using System.Globalization;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.Interfaces;

namespace WpfHexEditor.Core.Services.FormatParsing
{
    /// <summary>
    /// Evaluates conditional expressions from whfmt block definitions.
    /// </summary>
    internal static class ConditionEvaluator
    {
        /// <summary>
        /// Evaluate a <see cref="ConditionDefinition"/> object.
        /// </summary>
        public static bool Evaluate(
            ConditionDefinition condition,
            IBinaryDataSource source,
            VariableContext? variableContext)
        {
            if (condition == null)
                return true;

            try
            {
                long fieldValue = 0;
                if (!string.IsNullOrWhiteSpace(condition.Field))
                {
                    if (condition.Field.StartsWith("offset:"))
                    {
                        var offsetStr = condition.Field.Substring(7);
                        if (long.TryParse(offsetStr, out long offset))
                        {
                            var buffer = source.ReadBytes(offset, condition.Length);
                            if (buffer != null && buffer.Length == condition.Length)
                            {
                                fieldValue = condition.Length switch
                                {
                                    1 => buffer[0],
                                    2 => BitConverter.ToUInt16(buffer, 0),
                                    4 => BitConverter.ToUInt32(buffer, 0),
                                    8 => (long)BitConverter.ToUInt64(buffer, 0),
                                    _ => buffer[0]
                                };
                            }
                        }
                    }
                    else if (condition.Field.StartsWith("var:"))
                    {
                        fieldValue = variableContext?.GetVariableAsLong(condition.Field.Substring(4)) ?? 0;
                    }
                }

                long compareValue = 0;
                if (!string.IsNullOrWhiteSpace(condition.Value))
                {
                    if (condition.Value.StartsWith("0x"))
                        long.TryParse(condition.Value.Substring(2), NumberStyles.HexNumber, null, out compareValue);
                    else
                        long.TryParse(condition.Value, out compareValue);
                }

                return condition.Operator?.ToLowerInvariant() switch
                {
                    "equals" or "==" => fieldValue == compareValue,
                    "notequals" or "!=" => fieldValue != compareValue,
                    "greaterthan" or ">" => fieldValue > compareValue,
                    "lessthan" or "<" => fieldValue < compareValue,
                    "greaterorequal" or ">=" => fieldValue >= compareValue,
                    "lessorequal" or "<=" => fieldValue <= compareValue,
                    _ => false
                };
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Evaluate a string-based conditional expression.
        /// Supports comparison operators and variable references.
        /// </summary>
        public static bool Evaluate(
            string condition,
            VariableContext? variableContext,
            ExpressionEvaluator? expressionEvaluator)
        {
            if (string.IsNullOrWhiteSpace(condition))
                return true;

            try
            {
                string[] operators = { "==", "!=", "<=", ">=", "<", ">" };
                foreach (var op in operators)
                {
                    int opIndex = condition.IndexOf(op);
                    if (opIndex > 0)
                    {
                        string leftStr = condition.Substring(0, opIndex).Trim();
                        string rightStr = condition.Substring(opIndex + op.Length).Trim();

                        long leftValue = EvaluateValue(leftStr, variableContext, expressionEvaluator);
                        long rightValue = EvaluateValue(rightStr, variableContext, expressionEvaluator);

                        return op switch
                        {
                            "==" => leftValue == rightValue,
                            "!=" => leftValue != rightValue,
                            "<" => leftValue < rightValue,
                            ">" => leftValue > rightValue,
                            "<=" => leftValue <= rightValue,
                            ">=" => leftValue >= rightValue,
                            _ => false
                        };
                    }
                }

                return EvaluateValue(condition, variableContext, expressionEvaluator) != 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Evaluate a single value in a condition (variable reference or literal).
        /// </summary>
        public static long EvaluateValue(
            string value,
            VariableContext? variableContext,
            ExpressionEvaluator? expressionEvaluator)
        {
            value = value.Trim();

            if (value.StartsWith("var:"))
                return variableContext?.GetVariableAsLong(value.Substring(4)) ?? 0;
            else if (value.StartsWith("calc:"))
                return expressionEvaluator?.Evaluate(value.Substring(5)) ?? 0;
            else if (long.TryParse(value, out long numValue))
                return numValue;
            else if (value.StartsWith("0x") && long.TryParse(value.Substring(2), NumberStyles.HexNumber, null, out long hexValue))
                return hexValue;
            else
                return variableContext?.GetVariableAsLong(value) ?? 0;
        }
    }
}

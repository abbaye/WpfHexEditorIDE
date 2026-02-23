//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Text.RegularExpressions;

namespace WpfHexaEditor.Core.FormatDetection
{
    /// <summary>
    /// Evaluates simple arithmetic expressions for offset/length calculations
    /// Supports: +, -, *, /, (), variables via context
    /// Example: "calc:offset + 16", "calc:width * height", "calc:(size + 7) / 8"
    /// </summary>
    public class ExpressionEvaluator
    {
        private readonly VariableContext _context;

        public ExpressionEvaluator(VariableContext context)
        {
            _context = context ?? new VariableContext();
        }

        /// <summary>
        /// Evaluate an expression string to a long value
        /// </summary>
        public long Evaluate(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return 0;

            try
            {
                // Replace variable references with their values
                var expandedExpr = ReplaceVariables(expression);

                // Evaluate the arithmetic expression
                return EvaluateArithmetic(expandedExpr);
            }
            catch (Exception)
            {
                return 0; // Return 0 on evaluation error
            }
        }

        /// <summary>
        /// Replace variable names with their values
        /// </summary>
        private string ReplaceVariables(string expression)
        {
            // Match variable names (letters, digits, underscore)
            var pattern = @"\b([a-zA-Z_][a-zA-Z0-9_]*)\b";

            return Regex.Replace(expression, pattern, match =>
            {
                var varName = match.Groups[1].Value;

                // Skip operators and keywords
                if (varName == "calc" || varName == "var")
                    return varName;

                // Try to get variable value
                if (_context.HasVariable(varName))
                {
                    var value = _context.GetVariableAsLong(varName);
                    return value.ToString();
                }

                // If not a variable, keep as is (might be a number)
                return varName;
            });
        }

        /// <summary>
        /// Evaluate simple arithmetic expression
        /// Uses recursive descent parser for proper precedence
        /// </summary>
        private long EvaluateArithmetic(string expression)
        {
            expression = expression.Trim();

            // Remove spaces
            expression = Regex.Replace(expression, @"\s+", "");

            if (string.IsNullOrEmpty(expression))
                return 0;

            int pos = 0;
            return ParseExpression(expression, ref pos);
        }

        private long ParseExpression(string expr, ref int pos)
        {
            long result = ParseTerm(expr, ref pos);

            while (pos < expr.Length)
            {
                char op = expr[pos];
                if (op == '+' || op == '-')
                {
                    pos++;
                    long term = ParseTerm(expr, ref pos);
                    result = op == '+' ? result + term : result - term;
                }
                else
                {
                    break;
                }
            }

            return result;
        }

        private long ParseTerm(string expr, ref int pos)
        {
            long result = ParseFactor(expr, ref pos);

            while (pos < expr.Length)
            {
                char op = expr[pos];
                if (op == '*' || op == '/')
                {
                    pos++;
                    long factor = ParseFactor(expr, ref pos);
                    result = op == '*' ? result * factor : (factor != 0 ? result / factor : 0);
                }
                else
                {
                    break;
                }
            }

            return result;
        }

        private long ParseFactor(string expr, ref int pos)
        {
            // Skip whitespace
            while (pos < expr.Length && char.IsWhiteSpace(expr[pos]))
                pos++;

            if (pos >= expr.Length)
                return 0;

            // Handle parentheses
            if (expr[pos] == '(')
            {
                pos++; // Skip '('
                long result = ParseExpression(expr, ref pos);
                if (pos < expr.Length && expr[pos] == ')')
                    pos++; // Skip ')'
                return result;
            }

            // Handle unary minus
            if (expr[pos] == '-')
            {
                pos++;
                return -ParseFactor(expr, ref pos);
            }

            // Parse number (decimal or hex with 0x prefix)
            int start = pos;
            bool isHex = false;

            if (pos + 1 < expr.Length && expr[pos] == '0' && (expr[pos + 1] == 'x' || expr[pos + 1] == 'X'))
            {
                isHex = true;
                pos += 2;
                start = pos;

                while (pos < expr.Length && IsHexDigit(expr[pos]))
                    pos++;
            }
            else
            {
                while (pos < expr.Length && char.IsDigit(expr[pos]))
                    pos++;
            }

            if (pos > start)
            {
                string numStr = expr.Substring(start, pos - start);
                if (isHex)
                    return Convert.ToInt64(numStr, 16);
                else
                    return long.Parse(numStr);
            }

            return 0;
        }

        private bool IsHexDigit(char c)
        {
            return char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }
    }
}

//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7 (1M context)
//////////////////////////////////////////////
// Project: WpfHexEditor.Core.Definitions
// File: Models/Expressions/WhfmtExpressionLexer.cs
// Description: Tokenizer for whfmt expression strings. Single-pass, allocates
//              a list of tokens; consumed by WhfmtExpressionParser.
//////////////////////////////////////////////

using System.Globalization;
using System.Text;

namespace WpfHexEditor.Core.Definitions.Models.Expressions;

internal enum TokenKind
{
    Number, String, Identifier, Bool, Null,
    LParen, RParen, LBracket, RBracket,
    Comma, Dot, Question, Colon,
    Plus, Minus, Star, Slash, Percent,
    Eq, Neq, Lt, Gt, Le, Ge,
    And, Or, Not,
    BitAnd, BitOr, BitXor, BitNot, ShiftL, ShiftR,
    Eof,
}

internal readonly record struct Token(TokenKind Kind, string Text, int Position, double NumberValue = 0, bool NumberIsInt = false);

internal static class WhfmtExpressionLexer
{
    public static List<Token> Tokenize(string source)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < source.Length)
        {
            char c = source[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            int start = i;

            // Number (including 0x hex, decimals)
            if (char.IsDigit(c))
            {
                bool isHex = c == '0' && i + 1 < source.Length && (source[i + 1] == 'x' || source[i + 1] == 'X');
                if (isHex)
                {
                    i += 2;
                    int hexStart = i;
                    while (i < source.Length && IsHexDigit(source[i])) i++;
                    var hexStr = source.Substring(hexStart, i - hexStart);
                    if (hexStr.Length == 0)
                        throw new WhfmtExpressionException("Empty hex literal after '0x'", source, start);
                    long hex = long.Parse(hexStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    tokens.Add(new Token(TokenKind.Number, source.Substring(start, i - start), start, hex, true));
                    continue;
                }
                bool sawDot = false;
                while (i < source.Length && (char.IsDigit(source[i]) || source[i] == '.'))
                {
                    if (source[i] == '.') { if (sawDot) break; sawDot = true; }
                    i++;
                }
                var numStr = source.Substring(start, i - start);
                double d = double.Parse(numStr, CultureInfo.InvariantCulture);
                tokens.Add(new Token(TokenKind.Number, numStr, start, d, !sawDot));
                continue;
            }

            // String (single or double quoted)
            if (c == '\'' || c == '"')
            {
                char quote = c;
                i++;
                var sb = new StringBuilder();
                while (i < source.Length && source[i] != quote)
                {
                    if (source[i] == '\\' && i + 1 < source.Length)
                    {
                        i++;
                        sb.Append(source[i] switch
                        {
                            'n' => '\n', 't' => '\t', 'r' => '\r',
                            '\\' => '\\', '\'' => '\'', '"' => '"',
                            '0' => '\0',
                            var esc => esc
                        });
                        i++;
                    }
                    else
                    {
                        sb.Append(source[i++]);
                    }
                }
                if (i >= source.Length)
                    throw new WhfmtExpressionException("Unterminated string literal", source, start);
                i++; // closing quote
                tokens.Add(new Token(TokenKind.String, sb.ToString(), start));
                continue;
            }

            // Identifier / keyword
            if (char.IsLetter(c) || c == '_')
            {
                while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_')) i++;
                var id = source.Substring(start, i - start);
                tokens.Add(id switch
                {
                    "true"  => new Token(TokenKind.Bool, id, start, 1, true),
                    "false" => new Token(TokenKind.Bool, id, start, 0, true),
                    "null"  => new Token(TokenKind.Null, id, start),
                    _       => new Token(TokenKind.Identifier, id, start),
                });
                continue;
            }

            // Operators / punctuation
            switch (c)
            {
                case '(': tokens.Add(new Token(TokenKind.LParen, "(", start)); i++; break;
                case ')': tokens.Add(new Token(TokenKind.RParen, ")", start)); i++; break;
                case '[': tokens.Add(new Token(TokenKind.LBracket, "[", start)); i++; break;
                case ']': tokens.Add(new Token(TokenKind.RBracket, "]", start)); i++; break;
                case ',': tokens.Add(new Token(TokenKind.Comma, ",", start)); i++; break;
                case '.': tokens.Add(new Token(TokenKind.Dot, ".", start)); i++; break;
                case '?': tokens.Add(new Token(TokenKind.Question, "?", start)); i++; break;
                case ':': tokens.Add(new Token(TokenKind.Colon, ":", start)); i++; break;
                case '+': tokens.Add(new Token(TokenKind.Plus, "+", start)); i++; break;
                case '-': tokens.Add(new Token(TokenKind.Minus, "-", start)); i++; break;
                case '*': tokens.Add(new Token(TokenKind.Star, "*", start)); i++; break;
                case '/': tokens.Add(new Token(TokenKind.Slash, "/", start)); i++; break;
                case '%': tokens.Add(new Token(TokenKind.Percent, "%", start)); i++; break;
                case '~': tokens.Add(new Token(TokenKind.BitNot, "~", start)); i++; break;
                case '^': tokens.Add(new Token(TokenKind.BitXor, "^", start)); i++; break;
                case '=':
                    if (Peek(source, i, '=')) { tokens.Add(new Token(TokenKind.Eq, "==", start)); i += 2; }
                    else throw new WhfmtExpressionException("Single '=' not allowed; did you mean '=='?", source, start);
                    break;
                case '!':
                    if (Peek(source, i, '=')) { tokens.Add(new Token(TokenKind.Neq, "!=", start)); i += 2; }
                    else { tokens.Add(new Token(TokenKind.Not, "!", start)); i++; }
                    break;
                case '<':
                    if (Peek(source, i, '=')) { tokens.Add(new Token(TokenKind.Le, "<=", start)); i += 2; }
                    else if (Peek(source, i, '<')) { tokens.Add(new Token(TokenKind.ShiftL, "<<", start)); i += 2; }
                    else { tokens.Add(new Token(TokenKind.Lt, "<", start)); i++; }
                    break;
                case '>':
                    if (Peek(source, i, '=')) { tokens.Add(new Token(TokenKind.Ge, ">=", start)); i += 2; }
                    else if (Peek(source, i, '>')) { tokens.Add(new Token(TokenKind.ShiftR, ">>", start)); i += 2; }
                    else { tokens.Add(new Token(TokenKind.Gt, ">", start)); i++; }
                    break;
                case '&':
                    if (Peek(source, i, '&')) { tokens.Add(new Token(TokenKind.And, "&&", start)); i += 2; }
                    else { tokens.Add(new Token(TokenKind.BitAnd, "&", start)); i++; }
                    break;
                case '|':
                    if (Peek(source, i, '|')) { tokens.Add(new Token(TokenKind.Or, "||", start)); i += 2; }
                    else { tokens.Add(new Token(TokenKind.BitOr, "|", start)); i++; }
                    break;
                default:
                    throw new WhfmtExpressionException($"Unexpected character '{c}'", source, start);
            }
        }
        tokens.Add(new Token(TokenKind.Eof, "", source.Length));
        return tokens;
    }

    private static bool Peek(string s, int i, char expected) => i + 1 < s.Length && s[i + 1] == expected;

    private static bool IsHexDigit(char c) =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
}

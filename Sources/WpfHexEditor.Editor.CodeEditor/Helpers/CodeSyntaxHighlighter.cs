//////////////////////////////////////////////
// Apache 2.0  - 2026
// Custom CodeEditor - Syntax Highlighter (Phase 2)
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com), Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using WpfHexEditor.Editor.CodeEditor.Models;

namespace WpfHexEditor.Editor.CodeEditor.Helpers
{
    /// <summary>
    /// JSON syntax highlighter with token-based coloring.
    /// Parses JSON line-by-line and identifies syntax elements.
    /// Caches results per line for performance.
    /// </summary>
    public class CodeSyntaxHighlighter : ISyntaxHighlighter
    {
        // ── ISyntaxHighlighter — implicit context for interface calls ─────────
        private JsonParserContext _implicitContext = new();

        /// <inheritdoc />
        public IReadOnlyList<SyntaxHighlightToken> Highlight(string lineText, int lineIndex)
        {
            if (lineIndex == 0) Reset();
            var line   = new Models.CodeLine { Text = lineText };
            var tokens = HighlightLine(line, _implicitContext);
            return tokens.Select(t => new SyntaxHighlightToken(
                t.StartColumn, t.Length, t.Text ?? string.Empty,
                t.Foreground ?? System.Windows.Media.Brushes.WhiteSmoke,
                t.IsBold, t.IsItalic)).ToList();
        }

        /// <inheritdoc />
        public void Reset() => _implicitContext = new JsonParserContext();

        #region Token Types

        public enum TokenType
        {
            Default,
            Brace,           // { }
            Bracket,         // [ ]
            Comma,           // ,
            Colon,           // :
            Key,             // "formatName"
            StringValue,     // "PNG Image"
            Number,          // 123, 45.67
            Boolean,         // true, false
            Null,            // null
            Comment,         // // comment or /* comment */
            Keyword,         // signature, field, conditional, loop, action
            ValueType,       // uint8, uint16, string, etc.
            CalcExpression,  // calc:headerSize + 16
            VariableRef,     // var:chunkCount
            EscapeSequence,  // \n, \t, \\, \", etc. in strings
            Url,             // http://, https:// URLs in strings
            Deprecated,      // Deprecated keywords
            Error            // Invalid syntax
        }

        #endregion

        #region Color Configuration (Bindings to DPs)

        public Brush DefaultColor { get; set; } = Brushes.Black; // EditorForeground
        public Brush BraceColor { get; set; } = Brushes.Black;
        public Brush BracketColor { get; set; } = Brushes.Black;
        public Brush CommaColor { get; set; } = Brushes.Black;
        public Brush ColonColor { get; set; } = Brushes.Black;
        public Brush KeyColor { get; set; } = new SolidColorBrush(Color.FromRgb(0, 0, 255)); // Blue
        public Brush StringValueColor { get; set; } = new SolidColorBrush(Color.FromRgb(163, 21, 21)); // Dark Red
        public Brush NumberColor { get; set; } = new SolidColorBrush(Color.FromRgb(9, 134, 88)); // Green
        public Brush BooleanColor { get; set; } = new SolidColorBrush(Color.FromRgb(0, 0, 255)); // Blue
        public Brush NullColor { get; set; } = Brushes.Gray;
        public Brush CommentColor { get; set; } = Brushes.Green;
        public Brush KeywordColor { get; set; } = new SolidColorBrush(Color.FromRgb(0, 0, 255)); // Blue
        public Brush ValueTypeColor { get; set; } = new SolidColorBrush(Color.FromRgb(43, 145, 175)); // Cyan
        public Brush CalcExpressionColor { get; set; } = new SolidColorBrush(Color.FromRgb(128, 0, 128)); // Purple
        public Brush VariableReferenceColor { get; set; } = new SolidColorBrush(Color.FromRgb(128, 0, 128)); // Purple
        public Brush EscapeSequenceColor { get; set; } = new SolidColorBrush(Color.FromRgb(255, 140, 0)); // Dark Orange
        public Brush UrlColor { get; set; } = new SolidColorBrush(Color.FromRgb(0, 102, 204)); // Blue
        public Brush DeprecatedColor { get; set; } = Brushes.Gray;
        public Brush ErrorColor { get; set; } = Brushes.Red;

        #endregion

        #region Keywords & ValueTypes

        private static readonly HashSet<string> Keywords = new HashSet<string>
        {
            "signature", "field", "conditional", "loop", "action"
        };

        private static readonly HashSet<string> ValueTypes = new HashSet<string>
        {
            "uint8", "uint16", "uint32", "uint64",
            "int8", "int16", "int32", "int64",
            "float", "double",
            "string", "ascii", "utf8", "utf16",
            "bytes"
        };

        private static readonly HashSet<string> DeprecatedKeywords = new HashSet<string>
        {
            // Example deprecated keywords - adjust based on actual format definition spec
            "oldFormat", "legacyField", "deprecatedType"
        };

        #endregion

        /// <summary>
        /// Highlight a single line and return list of tokens.
        /// Uses cache if line is not dirty.
        /// </summary>
        public List<SyntaxToken> HighlightLine(CodeLine line, JsonParserContext context)
        {
            // Phase 11.4: Return cached tokens if available and update LRU timestamp
            if (!line.IsCacheDirty && line.TokensCache != null)
            {
                line.LastAccessTime = System.DateTime.UtcNow;
                return line.TokensCache;
            }

            var tokens = new List<SyntaxToken>();
            var text = line.Text;

            if (string.IsNullOrEmpty(text))
            {
                line.TokensCache = tokens;
                line.IsCacheDirty = false;
                line.LastAccessTime = System.DateTime.UtcNow;
                return tokens;
            }

            int i = 0;

            while (i < text.Length)
            {
                char ch = text[i];

                // Skip whitespace
                if (char.IsWhiteSpace(ch))
                {
                    i++;
                    continue;
                }

                // Comments
                if (ch == '/' && i + 1 < text.Length)
                {
                    if (text[i + 1] == '/') // Line comment
                    {
                        tokens.Add(new SyntaxToken
                        {
                            Type = TokenType.Comment,
                            StartColumn = i,
                            Length = text.Length - i,
                            Text = text.Substring(i),
                            Foreground = CommentColor,
                            IsItalic = true
                        });
                        break; // Rest of line is comment
                    }
                    else if (text[i + 1] == '*') // Block comment start
                    {
                        int end = text.IndexOf("*/", i + 2);
                        int length = (end != -1 ? end + 2 : text.Length) - i;
                        tokens.Add(new SyntaxToken
                        {
                            Type = TokenType.Comment,
                            StartColumn = i,
                            Length = length,
                            Text = text.Substring(i, length),
                            Foreground = CommentColor,
                            IsItalic = true
                        });
                        i += length;
                        continue;
                    }
                }

                // Braces
                if (ch == '{' || ch == '}')
                {
                    tokens.Add(new SyntaxToken
                    {
                        Type = TokenType.Brace,
                        StartColumn = i,
                        Length = 1,
                        Text = ch.ToString(),
                        Foreground = BraceColor,
                        IsBold = false
                    });
                    context.UpdateContext(ch);
                    i++;
                    continue;
                }

                // Brackets
                if (ch == '[' || ch == ']')
                {
                    tokens.Add(new SyntaxToken
                    {
                        Type = TokenType.Bracket,
                        StartColumn = i,
                        Length = 1,
                        Text = ch.ToString(),
                        Foreground = BracketColor,
                        IsBold = false
                    });
                    context.UpdateContext(ch);
                    i++;
                    continue;
                }

                // Comma
                if (ch == ',')
                {
                    tokens.Add(new SyntaxToken
                    {
                        Type = TokenType.Comma,
                        StartColumn = i,
                        Length = 1,
                        Text = ",",
                        Foreground = CommaColor
                    });
                    context.AfterComma();
                    i++;
                    continue;
                }

                // Colon
                if (ch == ':')
                {
                    tokens.Add(new SyntaxToken
                    {
                        Type = TokenType.Colon,
                        StartColumn = i,
                        Length = 1,
                        Text = ":",
                        Foreground = ColonColor
                    });
                    context.AfterColon();
                    i++;
                    continue;
                }

                // String (key or value)
                if (ch == '"')
                {
                    int end = FindStringEnd(text, i + 1);
                    string str = text.Substring(i + 1, end - i - 1);

                    // Determine if key or value based on context
                    bool isKey = context.IsInKeyPosition();

                    // Check for special patterns inside string
                    TokenType tokenType;
                    Brush foreground;
                    bool isBold = false;
                    bool isItalic = false;

                    if (str.StartsWith("calc:"))
                    {
                        tokenType = TokenType.CalcExpression;
                        foreground = CalcExpressionColor;
                        isItalic = true;
                    }
                    else if (str.StartsWith("var:"))
                    {
                        tokenType = TokenType.VariableRef;
                        foreground = VariableReferenceColor;
                        isItalic = true;
                    }
                    else if (isKey)
                    {
                        tokenType = TokenType.Key;
                        foreground = KeyColor;
                        isBold = true;
                    }
                    else if (DeprecatedKeywords.Contains(str))
                    {
                        tokenType = TokenType.Deprecated;
                        foreground = DeprecatedColor;
                        isBold = true;
                    }
                    else if (Keywords.Contains(str))
                    {
                        tokenType = TokenType.Keyword;
                        foreground = KeywordColor;
                        isBold = true;
                    }
                    else if (ValueTypes.Contains(str))
                    {
                        tokenType = TokenType.ValueType;
                        foreground = ValueTypeColor;
                    }
                    else if (str.StartsWith("http://") || str.StartsWith("https://"))
                    {
                        // URL detection
                        tokenType = TokenType.Url;
                        foreground = UrlColor;
                    }
                    else
                    {
                        tokenType = TokenType.StringValue;
                        foreground = StringValueColor;
                    }

                    // Check if string contains escape sequences and needs sub-token parsing
                    if (tokenType == TokenType.StringValue && ContainsEscapeSequences(str))
                    {
                        // Parse string with escape sequences as sub-tokens
                        var stringTokens = ParseStringWithEscapeSequences(text, i, end, foreground);
                        tokens.AddRange(stringTokens);
                    }
                    else
                    {
                        // Add as single token
                        tokens.Add(new SyntaxToken
                        {
                            Type = tokenType,
                            StartColumn = i,
                            Length = end - i + 1,
                            Text = text.Substring(i, end - i + 1),
                            Foreground = foreground,
                            IsBold = isBold,
                            IsItalic = isItalic
                        });
                    }

                    i = end + 1;
                    continue;
                }

                // Number
                if (char.IsDigit(ch) || (ch == '-' && i + 1 < text.Length && char.IsDigit(text[i + 1])))
                {
                    int start = i;
                    while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.' || text[i] == '-' || text[i] == 'e' || text[i] == 'E'))
                        i++;

                    tokens.Add(new SyntaxToken
                    {
                        Type = TokenType.Number,
                        StartColumn = start,
                        Length = i - start,
                        Text = text.Substring(start, i - start),
                        Foreground = NumberColor
                    });
                    continue;
                }

                // Boolean
                if (i + 4 <= text.Length && text.Substring(i, 4) == "true")
                {
                    tokens.Add(new SyntaxToken
                    {
                        Type = TokenType.Boolean,
                        StartColumn = i,
                        Length = 4,
                        Text = "true",
                        Foreground = BooleanColor,
                        IsBold = true
                    });
                    i += 4;
                    continue;
                }

                if (i + 5 <= text.Length && text.Substring(i, 5) == "false")
                {
                    tokens.Add(new SyntaxToken
                    {
                        Type = TokenType.Boolean,
                        StartColumn = i,
                        Length = 5,
                        Text = "false",
                        Foreground = BooleanColor,
                        IsBold = true
                    });
                    i += 5;
                    continue;
                }

                // Null
                if (i + 4 <= text.Length && text.Substring(i, 4) == "null")
                {
                    tokens.Add(new SyntaxToken
                    {
                        Type = TokenType.Null,
                        StartColumn = i,
                        Length = 4,
                        Text = "null",
                        Foreground = NullColor,
                        IsItalic = true
                    });
                    i += 4;
                    continue;
                }

                // Unknown/Error
                tokens.Add(new SyntaxToken
                {
                    Type = TokenType.Error,
                    StartColumn = i,
                    Length = 1,
                    Text = ch.ToString(),
                    Foreground = ErrorColor
                });
                i++;
            }

            // Cache tokens
            line.TokensCache = tokens;
            line.IsCacheDirty = false;

            return tokens;
        }

        /// <summary>
        /// Find end of string (handles escape sequences)
        /// </summary>
        private int FindStringEnd(string text, int start)
        {
            bool escaped = false;

            for (int i = start; i < text.Length; i++)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (text[i] == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (text[i] == '"')
                    return i;
            }

            return text.Length - 1; // Unterminated string
        }

        /// <summary>
        /// Check if string contains escape sequences that should be highlighted
        /// </summary>
        private bool ContainsEscapeSequences(string str)
        {
            return str.Contains("\\");
        }

        /// <summary>
        /// Parse string with escape sequences into sub-tokens
        /// </summary>
        private List<SyntaxToken> ParseStringWithEscapeSequences(string text, int start, int end, Brush defaultForeground)
        {
            var tokens = new List<SyntaxToken>();

            // Add opening quote
            tokens.Add(new SyntaxToken
            {
                Type = TokenType.StringValue,
                StartColumn = start,
                Length = 1,
                Text = "\"",
                Foreground = defaultForeground
            });

            int i = start + 1; // Skip opening quote
            int segmentStart = i;

            while (i < end)
            {
                if (text[i] == '\\' && i + 1 < end)
                {
                    // Add text before escape sequence
                    if (i > segmentStart)
                    {
                        tokens.Add(new SyntaxToken
                        {
                            Type = TokenType.StringValue,
                            StartColumn = segmentStart,
                            Length = i - segmentStart,
                            Text = text.Substring(segmentStart, i - segmentStart),
                            Foreground = defaultForeground
                        });
                    }

                    // Add escape sequence token (\ and next char)
                    int escLength = 2;
                    // Handle unicode escapes like \uXXXX
                    if (text[i + 1] == 'u' && i + 5 < end)
                    {
                        escLength = 6;
                    }

                    tokens.Add(new SyntaxToken
                    {
                        Type = TokenType.EscapeSequence,
                        StartColumn = i,
                        Length = escLength,
                        Text = text.Substring(i, Math.Min(escLength, end - i)),
                        Foreground = EscapeSequenceColor,
                        IsBold = true
                    });

                    i += escLength;
                    segmentStart = i;
                }
                else
                {
                    i++;
                }
            }

            // Add remaining text before closing quote
            if (i > segmentStart && segmentStart < end)
            {
                tokens.Add(new SyntaxToken
                {
                    Type = TokenType.StringValue,
                    StartColumn = segmentStart,
                    Length = end - segmentStart,
                    Text = text.Substring(segmentStart, end - segmentStart),
                    Foreground = defaultForeground
                });
            }

            // Add closing quote
            tokens.Add(new SyntaxToken
            {
                Type = TokenType.StringValue,
                StartColumn = end,
                Length = 1,
                Text = "\"",
                Foreground = defaultForeground
            });

            return tokens;
        }
    }

    /// <summary>
    /// Enhanced SyntaxToken with rendering properties
    /// </summary>
    public class SyntaxToken
    {
        public CodeSyntaxHighlighter.TokenType Type { get; set; }
        public int StartColumn { get; set; }
        public int Length { get; set; }
        public string Text { get; set; }
        public Brush Foreground { get; set; }
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
    }

    /// <summary>
    /// Parser context to determine if we're in key or value position
    /// </summary>
    public class JsonParserContext
    {
        private Stack<ContextType> _contextStack = new Stack<ContextType>();
        private bool _afterColon = false;

        public enum ContextType
        {
            Root,
            Object,
            Array
        }

        public JsonParserContext()
        {
            _contextStack.Push(ContextType.Root);
        }

        public bool IsInKeyPosition()
        {
            // We're in key position if:
            // 1. Inside object
            // 2. Not right after colon (value position)
            return _contextStack.Count > 0 &&
                   _contextStack.Peek() == ContextType.Object &&
                   !_afterColon;
        }

        public void UpdateContext(char ch)
        {
            if (ch == '{')
            {
                _contextStack.Push(ContextType.Object);
                _afterColon = false;
            }
            else if (ch == '[')
            {
                _contextStack.Push(ContextType.Array);
                _afterColon = false;
            }
            else if (ch == '}' || ch == ']')
            {
                if (_contextStack.Count > 1) // Keep at least Root
                    _contextStack.Pop();
                _afterColon = false;
            }
        }

        public void AfterColon()
        {
            _afterColon = true;
        }

        public void AfterComma()
        {
            _afterColon = false;
        }
    }
}

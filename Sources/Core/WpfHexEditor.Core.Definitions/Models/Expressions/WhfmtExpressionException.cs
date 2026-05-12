//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7 (1M context)
//////////////////////////////////////////////
// Project: WpfHexEditor.Core.Definitions
// File: Models/Expressions/WhfmtExpressionException.cs
// Description: Thrown by lexer/parser/evaluator for any whfmt expression error.
//              Carries the source string, the 0-based position, and an optional
//              identifier name (variable / function) — so consumers can render
//              IDE diagnostics with underline.
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Definitions.Models.Expressions;

/// <summary>
/// Thrown when a whfmt expression cannot be parsed or evaluated.
/// </summary>
public sealed class WhfmtExpressionException : Exception
{
    /// <summary>The original expression source string.</summary>
    public string Source { get; }

    /// <summary>0-based character position where the problem was detected. -1 when unknown.</summary>
    public int Position { get; }

    /// <summary>Identifier (variable or function name) tied to the error, when applicable.</summary>
    public string? Identifier { get; }

    public WhfmtExpressionException(string message, string source, int position = -1, string? identifier = null)
        : base(message)
    {
        Source = source;
        Position = position;
        Identifier = identifier;
    }
}

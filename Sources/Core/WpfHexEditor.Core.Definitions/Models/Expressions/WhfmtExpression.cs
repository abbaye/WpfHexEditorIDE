//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7 (1M context)
//////////////////////////////////////////////
// Project: WpfHexEditor.Core.Definitions
// File: Models/Expressions/WhfmtExpression.cs
// Description: AST node types for the whfmt expression subset (ADR-038 D5).
//              Closed hierarchy — every node is a sealed record. The evaluator
//              dispatches via pattern matching.
//
// Grammar (precedence low → high):
//   Ternary    : Logical (? Expr : Expr)?
//   Logical    : BitOr  (&& | ||)
//   BitOr      : BitXor (|)
//   BitXor     : BitAnd (^)
//   BitAnd     : Equality (&)
//   Equality   : Comparison (== | !=)
//   Comparison : Shift (< | > | <= | >=)
//   Shift      : Additive (<< | >>)
//   Additive   : Multiplicative (+ | -)
//   Multiplicative : Unary (* | / | %)
//   Unary      : (! | - | ~)? Primary
//   Primary    : Number | String | Bool | Null | '(' Expr ')'
//              | Identifier ('.' Member)*
//              | Identifier '(' Args? ')'
//              | Identifier '[' Expr ']'
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Definitions.Models.Expressions;

// AST surface kept internal so the evaluator implementation can evolve
// (bytecode lowering, alternative parsers) without ABI break.
// Consumers go through WhfmtExpressionEvaluator.Evaluate(string) which returns object?.

/// <summary>Base type for all expression AST nodes.</summary>
internal abstract record WhfmtExprNode;

// -- Literals ---------------------------------------------------------------

internal sealed record NumberNode(double Value, bool IsInteger) : WhfmtExprNode;
internal sealed record StringNode(string Value) : WhfmtExprNode;
internal sealed record BoolNode(bool Value) : WhfmtExprNode;
internal sealed record NullNode : WhfmtExprNode { public static readonly NullNode Instance = new(); }

// -- Identifiers / member access -------------------------------------------

/// <summary>Bare identifier — resolved against the variable store at eval time.</summary>
internal sealed record IdentifierNode(string Name) : WhfmtExprNode;

/// <summary>Member access: target.Member (e.g. "magic.length", "title.startsWith").</summary>
internal sealed record MemberNode(WhfmtExprNode Target, string Member) : WhfmtExprNode;

/// <summary>Indexer: target[index] (e.g. "nintendoLogo[0]").</summary>
internal sealed record IndexNode(WhfmtExprNode Target, WhfmtExprNode Index) : WhfmtExprNode;

/// <summary>Function or method call. When Target is a MemberNode, it's a method call.</summary>
internal sealed record CallNode(WhfmtExprNode Target, IReadOnlyList<WhfmtExprNode> Args) : WhfmtExprNode;

// -- Unary -----------------------------------------------------------------

internal enum UnaryOp { Negate, Not, BitNot }
internal sealed record UnaryNode(UnaryOp Op, WhfmtExprNode Operand) : WhfmtExprNode;

// -- Binary ----------------------------------------------------------------

internal enum BinaryOp
{
    // arithmetic
    Add, Sub, Mul, Div, Mod,
    // comparison
    Eq, Neq, Lt, Gt, Le, Ge,
    // logical
    And, Or,
    // bitwise
    BitAnd, BitOr, BitXor, ShiftL, ShiftR,
}
internal sealed record BinaryNode(BinaryOp Op, WhfmtExprNode Left, WhfmtExprNode Right) : WhfmtExprNode;

// -- Ternary ---------------------------------------------------------------

internal sealed record TernaryNode(WhfmtExprNode Cond, WhfmtExprNode Then, WhfmtExprNode Else) : WhfmtExprNode;

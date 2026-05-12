//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7 (1M context)
//////////////////////////////////////////////
// Project: WpfHexEditor.Core.Definitions
// File: Models/Expressions/WhfmtExpressionParser.cs
// Description: Recursive-descent parser for whfmt expressions. Consumes the
//              token list from WhfmtExpressionLexer and produces an AST.
//              See WhfmtExpression.cs for the grammar.
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Definitions.Models.Expressions;

internal sealed class WhfmtExpressionParser
{
    private readonly string _source;
    private readonly List<Token> _tokens;
    private int _pos;

    private WhfmtExpressionParser(string source, List<Token> tokens)
    {
        _source = source;
        _tokens = tokens;
    }

    internal static WhfmtExprNode Parse(string source)
    {
        var tokens = WhfmtExpressionLexer.Tokenize(source);
        var p = new WhfmtExpressionParser(source, tokens);
        var expr = p.ParseTernary();
        if (p.Current.Kind != TokenKind.Eof)
            throw new WhfmtExpressionException($"Unexpected token '{p.Current.Text}' after expression", source, p.Current.Position);
        return expr;
    }

    private Token Current => _tokens[_pos];

    private Token Consume(TokenKind kind, string what)
    {
        if (Current.Kind != kind)
            throw new WhfmtExpressionException($"Expected {what}, got '{Current.Text}'", _source, Current.Position);
        return _tokens[_pos++];
    }

    private bool Match(params TokenKind[] kinds)
    {
        if (Array.IndexOf(kinds, Current.Kind) >= 0) { _pos++; return true; }
        return false;
    }

    // -- Precedence ladder ----------------------------------------------------

    private WhfmtExprNode ParseTernary()
    {
        var cond = ParseLogicalOr();
        if (Current.Kind == TokenKind.Question)
        {
            _pos++;
            var then = ParseTernary();
            Consume(TokenKind.Colon, "':' in ternary");
            var els = ParseTernary();
            return new TernaryNode(cond, then, els);
        }
        return cond;
    }

    private WhfmtExprNode ParseLogicalOr()
    {
        var left = ParseLogicalAnd();
        while (Current.Kind == TokenKind.Or) { _pos++; left = new BinaryNode(BinaryOp.Or, left, ParseLogicalAnd()); }
        return left;
    }

    private WhfmtExprNode ParseLogicalAnd()
    {
        var left = ParseBitOr();
        while (Current.Kind == TokenKind.And) { _pos++; left = new BinaryNode(BinaryOp.And, left, ParseBitOr()); }
        return left;
    }

    private WhfmtExprNode ParseBitOr()
    {
        var left = ParseBitXor();
        while (Current.Kind == TokenKind.BitOr) { _pos++; left = new BinaryNode(BinaryOp.BitOr, left, ParseBitXor()); }
        return left;
    }

    private WhfmtExprNode ParseBitXor()
    {
        var left = ParseBitAnd();
        while (Current.Kind == TokenKind.BitXor) { _pos++; left = new BinaryNode(BinaryOp.BitXor, left, ParseBitAnd()); }
        return left;
    }

    private WhfmtExprNode ParseBitAnd()
    {
        var left = ParseEquality();
        while (Current.Kind == TokenKind.BitAnd) { _pos++; left = new BinaryNode(BinaryOp.BitAnd, left, ParseEquality()); }
        return left;
    }

    private WhfmtExprNode ParseEquality()
    {
        var left = ParseComparison();
        while (Current.Kind == TokenKind.Eq || Current.Kind == TokenKind.Neq)
        {
            var op = Current.Kind == TokenKind.Eq ? BinaryOp.Eq : BinaryOp.Neq;
            _pos++;
            left = new BinaryNode(op, left, ParseComparison());
        }
        return left;
    }

    private WhfmtExprNode ParseComparison()
    {
        var left = ParseShift();
        while (Current.Kind is TokenKind.Lt or TokenKind.Gt or TokenKind.Le or TokenKind.Ge)
        {
            var op = Current.Kind switch
            {
                TokenKind.Lt => BinaryOp.Lt,
                TokenKind.Gt => BinaryOp.Gt,
                TokenKind.Le => BinaryOp.Le,
                _            => BinaryOp.Ge,
            };
            _pos++;
            left = new BinaryNode(op, left, ParseShift());
        }
        return left;
    }

    private WhfmtExprNode ParseShift()
    {
        var left = ParseAdditive();
        while (Current.Kind is TokenKind.ShiftL or TokenKind.ShiftR)
        {
            var op = Current.Kind == TokenKind.ShiftL ? BinaryOp.ShiftL : BinaryOp.ShiftR;
            _pos++;
            left = new BinaryNode(op, left, ParseAdditive());
        }
        return left;
    }

    private WhfmtExprNode ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (Current.Kind is TokenKind.Plus or TokenKind.Minus)
        {
            var op = Current.Kind == TokenKind.Plus ? BinaryOp.Add : BinaryOp.Sub;
            _pos++;
            left = new BinaryNode(op, left, ParseMultiplicative());
        }
        return left;
    }

    private WhfmtExprNode ParseMultiplicative()
    {
        var left = ParseUnary();
        while (Current.Kind is TokenKind.Star or TokenKind.Slash or TokenKind.Percent)
        {
            var op = Current.Kind switch
            {
                TokenKind.Star    => BinaryOp.Mul,
                TokenKind.Slash   => BinaryOp.Div,
                _                 => BinaryOp.Mod,
            };
            _pos++;
            left = new BinaryNode(op, left, ParseUnary());
        }
        return left;
    }

    private WhfmtExprNode ParseUnary()
    {
        if (Current.Kind == TokenKind.Minus)  { _pos++; return new UnaryNode(UnaryOp.Negate, ParseUnary()); }
        if (Current.Kind == TokenKind.Not)    { _pos++; return new UnaryNode(UnaryOp.Not,    ParseUnary()); }
        if (Current.Kind == TokenKind.BitNot) { _pos++; return new UnaryNode(UnaryOp.BitNot, ParseUnary()); }
        return ParsePostfix();
    }

    private WhfmtExprNode ParsePostfix()
    {
        var expr = ParsePrimary();
        while (true)
        {
            switch (Current.Kind)
            {
                case TokenKind.Dot:
                    _pos++;
                    var member = Consume(TokenKind.Identifier, "member name");
                    expr = new MemberNode(expr, member.Text);
                    break;
                case TokenKind.LBracket:
                    _pos++;
                    var idx = ParseTernary();
                    Consume(TokenKind.RBracket, "']'");
                    expr = new IndexNode(expr, idx);
                    break;
                case TokenKind.LParen:
                    _pos++;
                    var args = ParseArgList();
                    Consume(TokenKind.RParen, "')'");
                    expr = new CallNode(expr, args);
                    break;
                default:
                    return expr;
            }
        }
    }

    private List<WhfmtExprNode> ParseArgList()
    {
        var args = new List<WhfmtExprNode>();
        if (Current.Kind == TokenKind.RParen) return args;
        args.Add(ParseTernary());
        while (Match(TokenKind.Comma)) args.Add(ParseTernary());
        return args;
    }

    private WhfmtExprNode ParsePrimary()
    {
        var t = Current;
        switch (t.Kind)
        {
            case TokenKind.Number:
                _pos++;
                return new NumberNode(t.NumberValue, t.NumberIsInt);
            case TokenKind.String:
                _pos++;
                return new StringNode(t.Text);
            case TokenKind.Bool:
                _pos++;
                return new BoolNode(t.NumberValue != 0);
            case TokenKind.Null:
                _pos++;
                return NullNode.Instance;
            case TokenKind.Identifier:
                _pos++;
                return new IdentifierNode(t.Text);
            case TokenKind.LParen:
                _pos++;
                var inner = ParseTernary();
                Consume(TokenKind.RParen, "')'");
                return inner;
            default:
                throw new WhfmtExpressionException($"Unexpected token '{t.Text}'", _source, t.Position);
        }
    }
}

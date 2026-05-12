//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7 (1M context)
//////////////////////////////////////////////
// Project: WpfHexEditor.Core.Definitions
// File: Models/Expressions/WhfmtExpressionEvaluator.cs
// Description: Public API for evaluating whfmt expression strings against a
//              variable store + function registry. Precompiles each unique
//              source string to an AST cached for the lifetime of this
//              evaluator instance — re-eval is just an AST walk.
// Architecture notes (per Option A design discussion):
//              Source-of-truth = string in .whfmt JSON (consistent with all
//              other declarative-format ecosystems). Cache makes runtime cost
//              comparable to a pre-serialized AST.
//////////////////////////////////////////////

using System.Collections.Concurrent;
using System.Globalization;
using WpfHexEditor.Core.Definitions.Models.Functions;

namespace WpfHexEditor.Core.Definitions.Models.Expressions;

/// <summary>
/// Evaluates whfmt expression strings against a <see cref="WhfmtVariableStore"/>
/// and a <see cref="WhfmtFunctionRegistry"/>.
/// </summary>
public sealed class WhfmtExpressionEvaluator
{
    private readonly WhfmtVariableStore _store;
    private readonly WhfmtFunctionRegistry _functions;
    private readonly ConcurrentDictionary<string, WhfmtExprNode> _astCache = new(StringComparer.Ordinal);

    public WhfmtExpressionEvaluator(WhfmtVariableStore store, WhfmtFunctionRegistry? functions = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _functions = functions ?? WhfmtFunctionRegistry.CreateDefault();
    }

    /// <summary>Underlying variable store (exposed for parsers that need to populate variables).</summary>
    public WhfmtVariableStore Variables => _store;

    /// <summary>Underlying function registry (exposed so hosts can register more functions).</summary>
    public WhfmtFunctionRegistry Functions => _functions;

    /// <summary>Returns the cached AST for <paramref name="source"/>, parsing on first call.</summary>
    internal WhfmtExprNode Compile(string source)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);
        return _astCache.GetOrAdd(source, WhfmtExpressionParser.Parse);
    }

    /// <summary>Evaluates the expression and returns the raw object result.</summary>
    public object? Evaluate(string source) => Eval(Compile(source));

    /// <summary>
    /// Evaluates the expression coerced to a boolean. Non-zero numbers are true,
    /// non-empty strings are true, null is false.
    /// </summary>
    public bool EvaluateBool(string source) => ToBool(Evaluate(source));

    /// <summary>Evaluates the expression coerced to an int64.</summary>
    public long EvaluateInt(string source) => ToInt64(Evaluate(source));

    /// <summary>Evaluates the expression coerced to a double.</summary>
    public double EvaluateDouble(string source) => ToDouble(Evaluate(source));

    // -- AST walker -----------------------------------------------------------

    private object? Eval(WhfmtExprNode node) => node switch
    {
        NumberNode n     => n.IsInteger ? (object)(long)n.Value : n.Value,
        StringNode s     => s.Value,
        BoolNode b       => b.Value,
        NullNode         => null,
        IdentifierNode i => _store.GetRaw(i.Name),
        UnaryNode u      => EvalUnary(u),
        BinaryNode b     => EvalBinary(b),
        TernaryNode t    => ToBool(Eval(t.Cond)) ? Eval(t.Then) : Eval(t.Else),
        IndexNode ix     => EvalIndex(ix),
        MemberNode m     => EvalMember(m),
        CallNode c       => EvalCall(c),
        _                => throw new InvalidOperationException($"Unknown node: {node.GetType().Name}"),
    };

    private object? EvalUnary(UnaryNode u)
    {
        var v = Eval(u.Operand);
        return u.Op switch
        {
            UnaryOp.Negate => -ToDouble(v),
            UnaryOp.Not    => !ToBool(v),
            UnaryOp.BitNot => ~ToInt64(v),
            _              => throw new InvalidOperationException($"Unknown unary {u.Op}"),
        };
    }

    private object? EvalBinary(BinaryNode b)
    {
        // Short-circuit logical ops
        if (b.Op == BinaryOp.And) return ToBool(Eval(b.Left)) && ToBool(Eval(b.Right));
        if (b.Op == BinaryOp.Or)  return ToBool(Eval(b.Left)) || ToBool(Eval(b.Right));

        var l = Eval(b.Left);
        var r = Eval(b.Right);

        return b.Op switch
        {
            BinaryOp.Eq  => AreEqual(l, r),
            BinaryOp.Neq => !AreEqual(l, r),
            BinaryOp.Add => l is string sl || r is string sr ? string.Concat(l, r) : (object)(ToDouble(l) + ToDouble(r)),
            BinaryOp.Sub => ToDouble(l) - ToDouble(r),
            BinaryOp.Mul => ToDouble(l) * ToDouble(r),
            BinaryOp.Div => ToDouble(l) / ToDouble(r),
            BinaryOp.Mod => ToDouble(l) % ToDouble(r),
            BinaryOp.Lt  => ToDouble(l) <  ToDouble(r),
            BinaryOp.Gt  => ToDouble(l) >  ToDouble(r),
            BinaryOp.Le  => ToDouble(l) <= ToDouble(r),
            BinaryOp.Ge  => ToDouble(l) >= ToDouble(r),
            BinaryOp.BitAnd => ToInt64(l) & ToInt64(r),
            BinaryOp.BitOr  => ToInt64(l) | ToInt64(r),
            BinaryOp.BitXor => ToInt64(l) ^ ToInt64(r),
            BinaryOp.ShiftL => ToInt64(l) << (int)ToInt64(r),
            BinaryOp.ShiftR => ToInt64(l) >> (int)ToInt64(r),
            _ => throw new InvalidOperationException($"Unknown binary {b.Op}"),
        };
    }

    private object? EvalIndex(IndexNode ix)
    {
        var target = Eval(ix.Target);
        var idx    = (int)ToInt64(Eval(ix.Index));
        return target switch
        {
            string s when idx >= 0 && idx < s.Length => (long)s[idx],
            byte[] b when idx >= 0 && idx < b.Length => (long)b[idx],
            System.Collections.IList list when idx >= 0 && idx < list.Count => list[idx],
            _ => null,
        };
    }

    private object? EvalMember(MemberNode m)
    {
        var target = Eval(m.Target);
        // Built-in property-style access: ".length"
        if (m.Member == "length")
        {
            return target switch
            {
                string s => (long)s.Length,
                byte[] b => (long)b.Length,
                System.Collections.ICollection c => (long)c.Count,
                null     => 0L,
                _        => 0L,
            };
        }
        // Method-style members are evaluated via CallNode (m.Member is the method name);
        // here we just package the target+name for CallNode to dispatch.
        return new BoundMethod(target, m.Member);
    }

    private object? EvalCall(CallNode c)
    {
        var args = new List<object?>(c.Args.Count);
        foreach (var a in c.Args) args.Add(Eval(a));

        // Method call: target.method(args)
        if (c.Target is MemberNode mn)
        {
            var receiver = Eval(mn.Target);
            return InvokeMethod(receiver, mn.Member, args);
        }

        // Free function call: name(args)
        if (c.Target is IdentifierNode id)
        {
            if (!_functions.TryGet(id.Name, out var fn))
                throw new WhfmtExpressionException(
                    $"Unknown function '{id.Name}'", "", -1, id.Name);
            return fn.Invoke(args);
        }

        throw new InvalidOperationException("Call target must be an identifier or member access");
    }

    /// <summary>Bound method used internally when MemberNode is the target of a CallNode.</summary>
    private readonly record struct BoundMethod(object? Target, string Method);

    private static object? InvokeMethod(object? receiver, string method, IReadOnlyList<object?> args)
    {
        // String methods used by .whfmt expressions today
        if (receiver is string s)
        {
            return method switch
            {
                "startsWith" => args.Count == 1 && args[0] is string a ? s.StartsWith(a, StringComparison.Ordinal) : false,
                "endsWith"   => args.Count == 1 && args[0] is string a2 ? s.EndsWith(a2, StringComparison.Ordinal) : false,
                "includes"   => args.Count == 1 && args[0] is string a3 ? s.Contains(a3, StringComparison.Ordinal) : false,
                "contains"   => args.Count == 1 && args[0] is string a4 ? s.Contains(a4, StringComparison.Ordinal) : false,
                "toUpper"    => s.ToUpperInvariant(),
                "toLower"    => s.ToLowerInvariant(),
                "trim"       => s.Trim(),
                _ => throw new InvalidOperationException($"Unknown string method '{method}'"),
            };
        }
        throw new InvalidOperationException($"Cannot invoke method '{method}' on {receiver?.GetType().Name ?? "null"}");
    }

    // -- Coercion helpers -----------------------------------------------------

    internal static bool ToBool(object? v) => v switch
    {
        null       => false,
        bool b     => b,
        string s   => !string.IsNullOrEmpty(s),
        long l     => l != 0,
        int i      => i != 0,
        double d   => d != 0.0,
        _          => true,
    };

    internal static long ToInt64(object? v) => v switch
    {
        null     => 0,
        bool b   => b ? 1 : 0,
        long l   => l,
        int i    => i,
        double d => (long)d,
        string s => long.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) ? p : 0,
        _        => Convert.ToInt64(v, CultureInfo.InvariantCulture),
    };

    internal static double ToDouble(object? v) => v switch
    {
        null     => 0.0,
        bool b   => b ? 1.0 : 0.0,
        long l   => l,
        int i    => i,
        double d => d,
        string s => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) ? p : 0.0,
        _        => Convert.ToDouble(v, CultureInfo.InvariantCulture),
    };

    private static bool AreEqual(object? l, object? r)
    {
        if (l is null && r is null) return true;
        if (l is null || r is null) return false;
        if (l is string ls && r is string rs) return ls == rs;
        if (l is bool lb && r is bool rb) return lb == rb;
        // Numeric comparison via double; tolerate small ULP differences for integer-valued doubles
        try { return ToDouble(l) == ToDouble(r); }
        catch { return l.Equals(r); }
    }
}

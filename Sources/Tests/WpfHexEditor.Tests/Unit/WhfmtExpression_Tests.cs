//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Project: WpfHexEditor.Tests
// File: Unit/WhfmtExpression_Tests.cs
// Description:
//     Coverage for the P4 whfmt expression engine: lexer, parser, evaluator,
//     function registry, and the precompile-to-AST cache.
//////////////////////////////////////////////

using Microsoft.VisualStudio.TestTools.UnitTesting;
using WpfHexEditor.Core.Definitions.Models;
using WpfHexEditor.Core.Definitions.Models.Expressions;
using WpfHexEditor.Core.Definitions.Models.Functions;

namespace WpfHexEditor.Tests.Unit
{
    [TestClass]
    public class WhfmtExpression_Tests
    {
        private static WhfmtExpressionEvaluator MakeEvaluator(System.Action<WhfmtVariableStore>? populate = null)
        {
            var store = new WhfmtVariableStore();
            populate?.Invoke(store);
            return new WhfmtExpressionEvaluator(store);
        }

        // ----- Literals -----------------------------------------------------

        [TestMethod]
        public void Literal_IntegerNumber()      => Assert.AreEqual(42L,    MakeEvaluator().Evaluate("42"));
        [TestMethod]
        public void Literal_HexNumber()          => Assert.AreEqual(0xFFL,  MakeEvaluator().Evaluate("0xFF"));
        [TestMethod]
        public void Literal_FloatNumber()        => Assert.AreEqual(3.14,   (double)MakeEvaluator().Evaluate("3.14")!, 0.0001);
        [TestMethod]
        public void Literal_String()             => Assert.AreEqual("abc",  MakeEvaluator().Evaluate("'abc'"));
        [TestMethod]
        public void Literal_TrueFalse()
        {
            Assert.AreEqual(true,  MakeEvaluator().Evaluate("true"));
            Assert.AreEqual(false, MakeEvaluator().Evaluate("false"));
        }

        // ----- Arithmetic ---------------------------------------------------

        [TestMethod] public void Arith_Add()  => Assert.AreEqual(5.0, MakeEvaluator().EvaluateDouble("2 + 3"));
        [TestMethod] public void Arith_Sub()  => Assert.AreEqual(-1.0, MakeEvaluator().EvaluateDouble("2 - 3"));
        [TestMethod] public void Arith_Mul()  => Assert.AreEqual(6.0, MakeEvaluator().EvaluateDouble("2 * 3"));
        [TestMethod] public void Arith_Div()  => Assert.AreEqual(0.5, MakeEvaluator().EvaluateDouble("1 / 2"));
        [TestMethod] public void Arith_Mod()  => Assert.AreEqual(1.0, MakeEvaluator().EvaluateDouble("10 % 3"));
        [TestMethod] public void Arith_Precedence() => Assert.AreEqual(14.0, MakeEvaluator().EvaluateDouble("2 + 3 * 4"));
        [TestMethod] public void Arith_Parens() => Assert.AreEqual(20.0, MakeEvaluator().EvaluateDouble("(2 + 3) * 4"));

        // ----- Comparison + Logical ----------------------------------------

        [TestMethod] public void Cmp_Eq()       => Assert.IsTrue (MakeEvaluator().EvaluateBool("1 == 1"));
        [TestMethod] public void Cmp_Neq()      => Assert.IsTrue (MakeEvaluator().EvaluateBool("1 != 2"));
        [TestMethod] public void Cmp_Lt()       => Assert.IsTrue (MakeEvaluator().EvaluateBool("1 < 2"));
        [TestMethod] public void Cmp_Ge_False() => Assert.IsFalse(MakeEvaluator().EvaluateBool("1 >= 2"));
        [TestMethod] public void Logic_And()    => Assert.IsTrue (MakeEvaluator().EvaluateBool("true && 1 < 2"));
        [TestMethod] public void Logic_Or()     => Assert.IsTrue (MakeEvaluator().EvaluateBool("false || 1 == 1"));
        [TestMethod] public void Logic_Not()    => Assert.IsTrue (MakeEvaluator().EvaluateBool("!false"));

        // ----- Bitwise -----------------------------------------------------

        [TestMethod] public void Bit_And()   => Assert.AreEqual(0x10L, MakeEvaluator().EvaluateInt("0xF0 & 0x18"));
        [TestMethod] public void Bit_Or()    => Assert.AreEqual(0xFFL, MakeEvaluator().EvaluateInt("0xF0 | 0x0F"));
        [TestMethod] public void Bit_Xor()   => Assert.AreEqual(0xF0L, MakeEvaluator().EvaluateInt("0xFF ^ 0x0F"));
        [TestMethod] public void Bit_Not()   => Assert.AreEqual(-1L,    MakeEvaluator().EvaluateInt("~0"));
        [TestMethod] public void Bit_ShiftL()=> Assert.AreEqual(16L,   MakeEvaluator().EvaluateInt("1 << 4"));
        [TestMethod] public void Bit_ShiftR()=> Assert.AreEqual(2L,    MakeEvaluator().EvaluateInt("16 >> 3"));

        // ----- Ternary -----------------------------------------------------

        [TestMethod]
        public void Ternary_PicksThen()  => Assert.AreEqual("yes", MakeEvaluator().Evaluate("1 < 2 ? 'yes' : 'no'"));
        [TestMethod]
        public void Ternary_PicksElse()  => Assert.AreEqual("no",  MakeEvaluator().Evaluate("1 > 2 ? 'yes' : 'no'"));

        // ----- Variables ---------------------------------------------------

        [TestMethod]
        public void Var_Resolves()
        {
            var ev = MakeEvaluator(s => s.Set("cgbFlag", 0x80));
            Assert.IsTrue(ev.EvaluateBool("cgbFlag == 128"));
        }

        [TestMethod]
        public void Var_RealAssertionFromGbcWhfmt()
        {
            // From ROM_GBC.whfmt: "cgbFlag == 128 || cgbFlag == 192"
            var ev = MakeEvaluator(s => s.Set("cgbFlag", 0x80));
            Assert.IsTrue(ev.EvaluateBool("cgbFlag == 128 || cgbFlag == 192"));
        }

        [TestMethod]
        public void Var_RealCompositeExpression()
        {
            // From XPS.whfmt-style: "uncompressedSize > 0 ? ((1 - compressedSize / uncompressedSize) * 100) : 0"
            var ev = MakeEvaluator(s =>
            {
                s.Set("uncompressedSize", 1000);
                s.Set("compressedSize",   250);
            });
            var ratio = ev.EvaluateDouble(
                "uncompressedSize > 0 ? ((1 - compressedSize / uncompressedSize) * 100) : 0");
            Assert.AreEqual(75.0, ratio, 0.001);
        }

        // ----- Strings -----------------------------------------------------

        [TestMethod]
        public void String_StartsWith()
            => Assert.IsTrue(MakeEvaluator(s => s.Set("parMagic", "# PHILIPS"))
                .EvaluateBool("parMagic.startsWith('#')"));

        [TestMethod]
        public void String_LengthMember()
            => Assert.AreEqual(5L, MakeEvaluator(s => s.Set("name", "hello")).EvaluateInt("name.length"));

        [TestMethod]
        public void String_Concat()
            => Assert.AreEqual("hello world",
                MakeEvaluator().Evaluate("'hello' + ' ' + 'world'"));

        // ----- Functions ---------------------------------------------------

        [TestMethod] public void Fn_Min() => Assert.AreEqual(2.0, MakeEvaluator().EvaluateDouble("min(5, 2, 8)"));
        [TestMethod] public void Fn_Max() => Assert.AreEqual(8.0, MakeEvaluator().EvaluateDouble("max(5, 2, 8)"));
        [TestMethod] public void Fn_Abs() => Assert.AreEqual(3.0, MakeEvaluator().EvaluateDouble("abs(-3)"));
        [TestMethod] public void Fn_Hex() => Assert.AreEqual("0xFF", MakeEvaluator().Evaluate("hex(255)"));

        [TestMethod]
        public void Fn_Unknown_Throws()
        {
            Assert.ThrowsExactly<WhfmtExpressionException>(
                () => MakeEvaluator().Evaluate("notARealFn(1)"));
        }

        [TestMethod]
        public void CustomFn_CanBeRegistered()
        {
            var ev = MakeEvaluator();
            ev.Functions.Register(new DoubleFn());
            Assert.AreEqual(6L, ev.EvaluateInt("double(3)"));
        }

        // ----- Errors ------------------------------------------------------

        [TestMethod] public void Err_SyntaxError_Throws()
            => Assert.ThrowsExactly<WhfmtExpressionException>(() => MakeEvaluator().Evaluate("1 + + 2"));

        [TestMethod] public void Err_UnterminatedString_Throws()
            => Assert.ThrowsExactly<WhfmtExpressionException>(() => MakeEvaluator().Evaluate("'abc"));

        // ----- AST cache ---------------------------------------------------

        [TestMethod]
        public void Cache_ReturnsSameAstInstance()
        {
            var ev = MakeEvaluator();
            var a = ev.Compile("1 + 2");
            var b = ev.Compile("1 + 2");
            Assert.AreSame(a, b);
        }

        private sealed class DoubleFn : IWhfmtFunction
        {
            public string Name => "double";
            public object? Invoke(System.Collections.Generic.IReadOnlyList<object?> args)
                => System.Convert.ToInt64(args[0]) * 2;
        }
    }
}

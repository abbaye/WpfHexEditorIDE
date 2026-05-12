//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Project: WpfHexEditor.Tests
// File: Unit/WhfmtAssertionEvaluator_Tests.cs
// Description:
//     F7 — verifies FormatAssertionEvaluator evaluates assertions[] declared
//     in real catalog entries against a populated WhfmtVariableStore.
//////////////////////////////////////////////

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WpfHexEditor.Core.Definitions;
using WpfHexEditor.Core.Definitions.Metadata;
using WpfHexEditor.Core.Definitions.Models;
using WpfHexEditor.Core.Definitions.Models.Expressions;

namespace WpfHexEditor.Tests.Unit
{
    [TestClass]
    public class WhfmtAssertionEvaluator_Tests
    {
        private static readonly EmbeddedFormatCatalog Cat = EmbeddedFormatCatalog.Instance;

        [TestMethod]
        public void EvaluateAll_NII_PassesWhenHeaderSizeMatches()
        {
            // NIfTI-1 declares: "headerSize == 348"
            var nii = Cat.GetByFormatId("NII");
            Assert.IsNotNull(nii);
            var store = new WhfmtVariableStore();
            store.Set("headerSize", 348);
            store.Set("magic", "n+1\0");

            var results = FormatAssertionEvaluator.EvaluateAll(nii, Cat, store);
            Assert.IsTrue(results.Count > 0);
            // At least the header-size assertion should pass.
            var headerSizeRule = results.FirstOrDefault(r => r.Rule.Expression.Contains("headerSize"));
            Assert.IsNotNull(headerSizeRule);
            Assert.AreEqual(AssertionStatus.Pass, headerSizeRule.Status);
        }

        [TestMethod]
        public void EvaluateAll_NII_FailsWhenHeaderSizeMismatch()
        {
            var nii = Cat.GetByFormatId("NII");
            Assert.IsNotNull(nii);
            var store = new WhfmtVariableStore();
            store.Set("headerSize", 540);    // wrong — would indicate NIfTI-2

            var results = FormatAssertionEvaluator.EvaluateAll(nii, Cat, store);
            var headerSizeRule = results.FirstOrDefault(r => r.Rule.Expression.Contains("headerSize"));
            Assert.IsNotNull(headerSizeRule);
            Assert.AreEqual(AssertionStatus.Fail, headerSizeRule.Status);
        }

        [TestMethod]
        public void EvaluateAll_EmptyForFormatWithoutAssertions()
        {
            var anyEmpty = Cat.GetAll()
                .First(e => e.ResourceKey.EndsWith(".whfmt")
                          && e.GetAssertions(Cat).Count == 0);
            var results = FormatAssertionEvaluator.EvaluateAll(anyEmpty, Cat, new WhfmtVariableStore());
            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void EvaluateOne_PendingForBlankExpression()
        {
            var rule = new AssertionRule("blank", "", "error", null);
            var evaluator = new WhfmtExpressionEvaluator(new WhfmtVariableStore());
            var r = FormatAssertionEvaluator.EvaluateOne(evaluator, rule);
            Assert.AreEqual(AssertionStatus.Pending, r.Status);
        }

        [TestMethod]
        public void EvaluateOne_ErrorForUnparseable()
        {
            var rule = new AssertionRule("bad", "1 + + 2", "error", null);
            var evaluator = new WhfmtExpressionEvaluator(new WhfmtVariableStore());
            var r = FormatAssertionEvaluator.EvaluateOne(evaluator, rule);
            Assert.AreEqual(AssertionStatus.Error, r.Status);
            Assert.IsNotNull(r.Error);
        }

        [TestMethod]
        public void EvaluateOne_PassForTrueExpression()
        {
            var rule = new AssertionRule("ok", "1 == 1", "error", null);
            var evaluator = new WhfmtExpressionEvaluator(new WhfmtVariableStore());
            Assert.AreEqual(AssertionStatus.Pass, FormatAssertionEvaluator.EvaluateOne(evaluator, rule).Status);
        }

        [TestMethod]
        public void EvaluateOne_FailForFalseExpression()
        {
            var rule = new AssertionRule("ko", "1 == 2", "error", null);
            var evaluator = new WhfmtExpressionEvaluator(new WhfmtVariableStore());
            Assert.AreEqual(AssertionStatus.Fail, FormatAssertionEvaluator.EvaluateOne(evaluator, rule).Status);
        }
    }
}

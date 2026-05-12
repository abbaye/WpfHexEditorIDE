//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Project: WpfHexEditor.Tests
// File: Unit/WhfmtExpressionValidator_Tests.cs
// Description:
//     Coverage for the P9 R10 static expression validator. Verifies that
//     expressions in assertions[], blocks[], and forensic.suspiciousPatterns[]
//     are parsed and that variable / function references are resolved.
//////////////////////////////////////////////

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WpfHexEditor.Core.Definitions.Models.Validation;

namespace WpfHexEditor.Tests.Unit
{
    [TestClass]
    public class WhfmtExpressionValidator_Tests
    {
        [TestMethod]
        public void Valid_DocumentHasNoIssues()
        {
            const string json = """
            {
              "variables": { "cgbFlag": 0, "size": 0 },
              "assertions": [
                { "name": "ok",   "expression": "cgbFlag == 128 || cgbFlag == 192", "severity": "warning" },
                { "name": "size", "expression": "size > 0",                          "severity": "error" }
              ]
            }
            """;
            var issues = WhfmtExpressionValidator.Validate(json);
            Assert.AreEqual(0, issues.Count, "Expected no issues, got: " + string.Join(", ", issues.Select(i => i.Message)));
        }

        [TestMethod]
        public void UndeclaredVariable_IsReported()
        {
            const string json = """
            {
              "variables": { "cgbFlag": 0 },
              "assertions": [
                { "name": "bad", "expression": "cgbFlag == 1 && missing == 2", "severity": "error" }
              ]
            }
            """;
            var issues = WhfmtExpressionValidator.Validate(json);
            var issue = issues.Single();
            Assert.AreEqual("R10-002", issue.RuleId);
            Assert.IsTrue(issue.Message.Contains("missing"));
            Assert.AreEqual("assertions[0].expression", issue.Path);
        }

        [TestMethod]
        public void SyntaxError_IsReported()
        {
            const string json = """
            {
              "variables": { "x": 0 },
              "assertions": [
                { "name": "bad", "expression": "x + + 1", "severity": "error" }
              ]
            }
            """;
            var issues = WhfmtExpressionValidator.Validate(json);
            var issue = issues.Single();
            Assert.AreEqual("R10-001", issue.RuleId);
        }

        [TestMethod]
        public void UnknownFunction_IsReported()
        {
            const string json = """
            {
              "variables": { "x": 0 },
              "assertions": [
                { "name": "bad", "expression": "myFn(x) > 0", "severity": "error" }
              ]
            }
            """;
            var issues = WhfmtExpressionValidator.Validate(json);
            var issue = issues.Single();
            Assert.AreEqual("R10-003", issue.RuleId);
            Assert.IsTrue(issue.Message.Contains("myFn"));
        }

        [TestMethod]
        public void DeclaredFunction_IsAccepted()
        {
            // Function declared in functions{} is treated as known even if not
            // registered as a built-in.
            const string json = """
            {
              "variables": { "x": 0 },
              "functions": { "myFn": "doc string" },
              "assertions": [
                { "name": "ok", "expression": "myFn(x) > 0", "severity": "error" }
              ]
            }
            """;
            var issues = WhfmtExpressionValidator.Validate(json);
            Assert.AreEqual(0, issues.Count);
        }

        [TestMethod]
        public void BuiltinFunction_IsAccepted()
        {
            const string json = """
            {
              "variables": { "x": 0 },
              "assertions": [
                { "name": "ok", "expression": "min(x, 5) >= 0", "severity": "error" }
              ]
            }
            """;
            var issues = WhfmtExpressionValidator.Validate(json);
            Assert.AreEqual(0, issues.Count);
        }

        [TestMethod]
        public void BlockExpressionAndCondition_AreValidated()
        {
            const string json = """
            {
              "variables": { "a": 0 },
              "blocks": [
                { "type": "field", "name": "X", "expression": "a + 1" },
                { "type": "conditional", "name": "Y", "condition": "a > unknownVar" }
              ]
            }
            """;
            var issues = WhfmtExpressionValidator.Validate(json);
            Assert.AreEqual(1, issues.Count);
            Assert.IsTrue(issues[0].Path!.StartsWith("blocks[1]"));
            Assert.IsTrue(issues[0].Message.Contains("unknownVar"));
        }

        [TestMethod]
        public void ForensicSuspiciousCondition_IsValidated()
        {
            const string json = """
            {
              "variables": { "magic": "" },
              "forensic": {
                "suspiciousPatterns": [
                  { "name": "ok",  "condition": "magic != ''" },
                  { "name": "bad", "condition": "unknownVar > 0" }
                ]
              }
            }
            """;
            var issues = WhfmtExpressionValidator.Validate(json);
            Assert.AreEqual(1, issues.Count);
            Assert.IsTrue(issues[0].Path!.StartsWith("forensic.suspiciousPatterns[1]"));
        }

        [TestMethod]
        public void EmptyExpression_IsIgnored()
        {
            // Defensive: empty / whitespace expression should NOT crash, NOT report.
            const string json = """
            { "assertions": [ { "name": "x", "expression": "" }, { "name": "y", "expression": "   " } ] }
            """;
            var issues = WhfmtExpressionValidator.Validate(json);
            Assert.AreEqual(0, issues.Count);
        }

        [TestMethod]
        public void MalformedJson_ProducesR10_000()
        {
            const string json = """{ "not": "closed" """;
            var issues = WhfmtExpressionValidator.Validate(json);
            var issue = issues.Single();
            Assert.AreEqual("R10-000", issue.RuleId);
            Assert.AreEqual(WhfmtIssueSeverity.Error, issue.Severity);
        }
    }
}

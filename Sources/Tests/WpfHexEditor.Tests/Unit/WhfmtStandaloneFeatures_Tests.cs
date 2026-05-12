//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Project: WpfHexEditor.Tests
// File: Unit/WhfmtStandaloneFeatures_Tests.cs
// Description:
//     Coverage for the P8 syntaxDefinition extensions: completions[],
//     outlineRules[], diagnosticRules[]. Verifies that the DTO + serializer
//     map correctly from JSON into the LanguageDefinition domain model.
//////////////////////////////////////////////

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WpfHexEditor.Core.ProjectSystem.Languages;

namespace WpfHexEditor.Tests.Unit
{
    [TestClass]
    public class WhfmtStandaloneFeatures_Tests
    {
        // Synthetic syntaxDefinition JSON used by the P8 tests — proves the
        // serializer wires the new sections even though no real .whfmt declares
        // them yet (the catalog will be populated progressively in P11).
        private const string SyntaxJson = """
        {
          "id": "demo",
          "name": "Demo",
          "extensions": [".demo"],
          "completions": [
            { "label": "if",    "kind": "keyword", "detail": "if statement" },
            { "label": "Task<T>","kind": "class",   "detail": "Task wrapper", "insertText": "Task<${1:T}>" }
          ],
          "outlineRules": [
            { "type": "class",  "pattern": "^\\s*class\\s+(\\w+)",     "group": 1 },
            { "type": "method", "pattern": "^\\s*def\\s+(\\w+)\\s*\\(", "group": 1 }
          ],
          "diagnosticRules": [
            { "id": "WH0001", "pattern": "TODO\\b",   "severity": "info",    "message": "TODO comment" },
            { "id": "WH0002", "pattern": "FIXME\\b",  "severity": "warning", "message": "FIXME comment" }
          ]
        }
        """;

        private static LanguageDefinition LoadDemo()
            => LanguageDefinitionSerializer.ParseSyntaxDefinitionBlock(
                SyntaxJson,
                formatName: "Demo",
                extensions: [".demo"],
                preferredEditor: "code-editor");

        // ----- Completions -------------------------------------------------

        [TestMethod]
        public void Completions_MappedFromJson()
        {
            var def = LoadDemo();
            Assert.AreEqual(2, def.Completions.Count);

            var ifKw = def.Completions.First(c => c.Label == "if");
            Assert.AreEqual(WhfmtCompletionKind.Keyword, ifKw.Kind);
            Assert.AreEqual("if statement", ifKw.Detail);
            Assert.IsNull(ifKw.InsertText);

            var task = def.Completions.First(c => c.Label == "Task<T>");
            Assert.AreEqual(WhfmtCompletionKind.Class, task.Kind);
            Assert.AreEqual("Task<${1:T}>", task.InsertText);
        }

        [TestMethod]
        public void Completions_UnknownKindFallsBackToOther()
        {
            const string json = """{ "name":"X", "completions":[ { "label":"foo", "kind":"madeup" } ] }""";
            var def = LanguageDefinitionSerializer.ParseSyntaxDefinitionBlock(json, "X", [".x"], null);
            Assert.AreEqual(WhfmtCompletionKind.Other, def.Completions[0].Kind);
        }

        [TestMethod]
        public void Completions_EmptyWhenAbsent()
        {
            const string json = """{ "name": "X" }""";
            var def = LanguageDefinitionSerializer.ParseSyntaxDefinitionBlock(json, "X", [".x"], null);
            Assert.AreEqual(0, def.Completions.Count);
        }

        // ----- OutlineRules ------------------------------------------------

        [TestMethod]
        public void OutlineRules_MappedFromJson()
        {
            var def = LoadDemo();
            Assert.AreEqual(2, def.OutlineRules.Count);

            var classRule = def.OutlineRules.First(r => r.Kind == "class");
            Assert.IsNotNull(classRule.Pattern);
            Assert.AreEqual(1, classRule.Group);

            // The compiled regex should actually match what we expect.
            var match = classRule.Pattern.Match("  class Foo");
            Assert.IsTrue(match.Success);
            Assert.AreEqual("Foo", match.Groups[1].Value);
        }

        [TestMethod]
        public void OutlineRules_GroupDefaultsTo1WhenZero()
        {
            const string json = """
            { "name":"X",
              "outlineRules":[ { "type":"sym", "pattern":"(\\w+)", "group": 0 } ] }
            """;
            var def = LanguageDefinitionSerializer.ParseSyntaxDefinitionBlock(json, "X", [".x"], null);
            Assert.AreEqual(1, def.OutlineRules[0].Group);
        }

        [TestMethod]
        public void OutlineRules_MalformedPatternIsSkipped()
        {
            const string json = """
            { "name":"X",
              "outlineRules":[
                { "type":"bad",  "pattern":"[unclosed" },
                { "type":"good", "pattern":"^(\\w+)$"  }
              ] }
            """;
            var def = LanguageDefinitionSerializer.ParseSyntaxDefinitionBlock(json, "X", [".x"], null);
            Assert.AreEqual(1, def.OutlineRules.Count);
            Assert.AreEqual("good", def.OutlineRules[0].Kind);
        }

        // ----- DiagnosticRules ---------------------------------------------

        [TestMethod]
        public void DiagnosticRules_MappedFromJson()
        {
            var def = LoadDemo();
            Assert.AreEqual(2, def.DiagnosticRules.Count);

            var todo = def.DiagnosticRules.First(r => r.Id == "WH0001");
            Assert.AreEqual(WhfmtDiagnosticSeverity.Info,    todo.Severity);
            Assert.AreEqual("TODO comment",             todo.Message);
            Assert.IsTrue(todo.Pattern.IsMatch("// TODO fix this"));

            var fixme = def.DiagnosticRules.First(r => r.Id == "WH0002");
            Assert.AreEqual(WhfmtDiagnosticSeverity.Warning, fixme.Severity);
        }

        [TestMethod]
        public void DiagnosticRules_UnknownSeverityFallsBackToInfo()
        {
            const string json = """
            { "name":"X",
              "diagnosticRules":[ { "id":"X1", "pattern":"x", "severity":"madeup", "message":"m" } ] }
            """;
            var def = LanguageDefinitionSerializer.ParseSyntaxDefinitionBlock(json, "X", [".x"], null);
            Assert.AreEqual(WhfmtDiagnosticSeverity.Info, def.DiagnosticRules[0].Severity);
        }

        [TestMethod]
        public void DiagnosticRules_MissingFieldsAreSkipped()
        {
            const string json = """
            { "name":"X",
              "diagnosticRules":[
                { "id":"X1", "pattern":"x" },
                { "id":"X2", "pattern":"y", "message":"valid" }
              ] }
            """;
            var def = LanguageDefinitionSerializer.ParseSyntaxDefinitionBlock(json, "X", [".x"], null);
            // First entry has no message → skipped.
            Assert.AreEqual(1, def.DiagnosticRules.Count);
            Assert.AreEqual("X2", def.DiagnosticRules[0].Id);
        }
    }
}

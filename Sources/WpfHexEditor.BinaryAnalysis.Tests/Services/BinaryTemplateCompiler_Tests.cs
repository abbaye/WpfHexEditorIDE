//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text.Json.Nodes;
using WpfHexEditor.BinaryAnalysis.Services;

namespace WpfHexEditor.BinaryAnalysis.Tests.Services
{
    [TestClass]
    public class BinaryTemplateCompiler_Tests
    {
        private BinaryTemplateCompiler _compiler = null!;

        [TestInitialize]
        public void Setup() => _compiler = new BinaryTemplateCompiler();

        [TestMethod]
        public void CompileTemplate_EmptyScript_Throws()
        {
            try { _compiler.CompileTemplate(""); Assert.Fail("Expected ArgumentException"); }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void CompileTemplate_SimpleStruct_ReturnsValidJson()
        {
            var script = @"
struct FileHeader {
    uint32 magic;
    uint16 version;
    uint16 flags;
};";
            var result = _compiler.CompileTemplate(script, "TestFormat");

            Assert.IsNotNull(result);
            Assert.AreEqual("TestFormat", result["formatName"]?.ToString());
        }

        [TestMethod]
        public void CompileTemplate_SimpleStruct_HasBlocks()
        {
            var script = "struct Hdr { int magic; };";
            var result = _compiler.CompileTemplate(script);
            var blocks = result["blocks"];
            Assert.IsNotNull(blocks);
            Assert.IsTrue(blocks is JsonArray blocksArr && blocksArr.Count > 0);
        }

        [TestMethod]
        public void CompileTemplate_FlatFields_ParsesWithoutStruct()
        {
            var script = "uint32 offset;\nuint32 length;\n";
            var result = _compiler.CompileTemplate(script);
            Assert.IsNotNull(result);
            var blocks = result["blocks"];
            Assert.IsNotNull(blocks);
        }

        [TestMethod]
        public void CompileTemplate_WindowsTypes_MappedCorrectly()
        {
            var script = "struct S { DWORD size; WORD type; BYTE flags; };";
            var result = _compiler.CompileTemplate(script);
            var blocks = result["blocks"]!.AsArray();
            var fields = blocks[0]!["fields"]!.AsArray();

            Assert.AreEqual("uint32", fields[0]!["type"]?.ToString()); // DWORD → uint32
            Assert.AreEqual("uint16", fields[1]!["type"]?.ToString()); // WORD  → uint16
            Assert.AreEqual("uint8",  fields[2]!["type"]?.ToString()); // BYTE  → uint8
        }

        [TestMethod]
        public void CompileTemplate_FieldWithComment_CommentPreserved()
        {
            var script = "struct S {\n// Size in bytes\nuint32 size;\n};";
            var result = _compiler.CompileTemplate(script);
            var field = result["blocks"]!.AsArray()[0]!["fields"]!.AsArray()[0]!;
            Assert.IsNotNull(field["description"]);
            StringAssert.Contains(field["description"]!.ToString(), "Size in bytes");
        }

        [TestMethod]
        public void ParseTemplateToModel_ExtractsNameFromComment()
        {
            var script = "// Template: MyFormat\n// Description: Test template\nuint32 magic;";
            var model = _compiler.ParseTemplateToModel(script);
            Assert.AreEqual("MyFormat", model.Name);
            Assert.AreEqual("Test template", model.Description);
        }

        [TestMethod]
        public void ParseTemplateToModel_ExtractsFields()
        {
            var script = "uint32 magic;\nuint16 version;\nbyte flags;";
            var model = _compiler.ParseTemplateToModel(script);
            Assert.IsTrue(model.Fields.Count >= 2);
        }

        [TestMethod]
        public void GenerateTemplateFromFormat_RoundTrip_ContainsStructKeyword()
        {
            var script = "struct FileHeader { uint32 magic; uint16 version; };";
            var format = _compiler.CompileTemplate(script, "RoundTrip");
            var generated = _compiler.GenerateTemplateFromFormat(format);
            StringAssert.Contains(generated, "struct");
        }

        [TestMethod]
        public void CompileTemplate_ArrayField_ParsedCorrectly()
        {
            var script = "struct S { byte data[16]; };";
            var result = _compiler.CompileTemplate(script);
            var field = result["blocks"]!.AsArray()[0]!["fields"]!.AsArray()[0]!;
            Assert.IsNotNull(field["length"]);
            Assert.AreEqual(16, (int)field["length"]!);
        }
    }
}

//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Project: WpfHexEditor.Tests
// File: Unit/WhfmtVariables_Tests.cs
// Description:
//     Coverage for the P2 variables engine: WhfmtVariableParser handles both
//     whfmt v2 schemas (dict + typed-array), and WhfmtVariableStore preserves
//     types across Set/TryGet roundtrips.
//////////////////////////////////////////////

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WpfHexEditor.Core.Definitions.Models;

namespace WpfHexEditor.Tests.Unit
{
    [TestClass]
    public class WhfmtVariables_Tests
    {
        // ----- WhfmtValueTypes.Parse --------------------------------------------------

        [TestMethod]
        public void Parse_RecognizesCanonicalNames()
        {
            Assert.AreEqual(WhfmtValueType.UInt8,   WhfmtValueTypes.Parse("uint8"));
            Assert.AreEqual(WhfmtValueType.UInt32,  WhfmtValueTypes.Parse("uint32"));
            Assert.AreEqual(WhfmtValueType.Float32, WhfmtValueTypes.Parse("float32"));
            Assert.AreEqual(WhfmtValueType.Ascii,   WhfmtValueTypes.Parse("ascii"));
            Assert.AreEqual(WhfmtValueType.Bytes,   WhfmtValueTypes.Parse("bytes"));
        }

        [TestMethod]
        public void Parse_IsCaseInsensitive()
        {
            Assert.AreEqual(WhfmtValueType.UInt16, WhfmtValueTypes.Parse("UINT16"));
            Assert.AreEqual(WhfmtValueType.UInt16, WhfmtValueTypes.Parse("UInt16"));
        }

        [TestMethod]
        public void Parse_AcceptsCommonAliases()
        {
            Assert.AreEqual(WhfmtValueType.UInt8,   WhfmtValueTypes.Parse("byte"));
            Assert.AreEqual(WhfmtValueType.Int32,   WhfmtValueTypes.Parse("int"));
            Assert.AreEqual(WhfmtValueType.Float64, WhfmtValueTypes.Parse("double"));
            Assert.AreEqual(WhfmtValueType.Utf8,    WhfmtValueTypes.Parse("utf-8"));
        }

        [TestMethod]
        public void Parse_ReturnsUnknownForGarbage()
        {
            Assert.AreEqual(WhfmtValueType.Unknown, WhfmtValueTypes.Parse(""));
            Assert.AreEqual(WhfmtValueType.Unknown, WhfmtValueTypes.Parse(null));
            Assert.AreEqual(WhfmtValueType.Unknown, WhfmtValueTypes.Parse("notatype"));
        }

        [TestMethod]
        public void FixedSizeBytes_ReturnsExpectedWidths()
        {
            Assert.AreEqual(1, WhfmtValueTypes.FixedSizeBytes(WhfmtValueType.UInt8));
            Assert.AreEqual(2, WhfmtValueTypes.FixedSizeBytes(WhfmtValueType.UInt16));
            Assert.AreEqual(4, WhfmtValueTypes.FixedSizeBytes(WhfmtValueType.UInt32));
            Assert.AreEqual(8, WhfmtValueTypes.FixedSizeBytes(WhfmtValueType.UInt64));
            Assert.AreEqual(4, WhfmtValueTypes.FixedSizeBytes(WhfmtValueType.Float32));
            Assert.AreEqual(0, WhfmtValueTypes.FixedSizeBytes(WhfmtValueType.Ascii));
            Assert.AreEqual(0, WhfmtValueTypes.FixedSizeBytes(WhfmtValueType.Unknown));
        }

        // ----- Schema A (dict) --------------------------------------------------------

        [TestMethod]
        public void ParseDict_InfersTypeFromLiteralInitialValue()
        {
            const string json = """
                { "variables": {
                    "currentOffset": 0,
                    "magic": "",
                    "size": 1024,
                    "ratio": 3.14
                  } }
                """;

            var vars = WhfmtVariableParser.ParseDocument(json);
            Assert.AreEqual(4, vars.Count);

            var byName = vars.ToDictionary(v => v.Name);
            Assert.AreEqual(WhfmtValueType.Int32,   byName["currentOffset"].Type);
            Assert.AreEqual(WhfmtValueType.Ascii,   byName["magic"].Type);
            Assert.AreEqual(WhfmtValueType.Int32,   byName["size"].Type);
            Assert.AreEqual(WhfmtValueType.Float64, byName["ratio"].Type);

            // Numeric literals come back as long or double depending on parse path
            Assert.AreEqual(0L,    System.Convert.ToInt64(byName["currentOffset"].InitialValue));
            Assert.AreEqual("",    byName["magic"].InitialValue);
            Assert.AreEqual(1024L, System.Convert.ToInt64(byName["size"].InitialValue));
            Assert.AreEqual(3.14,  System.Convert.ToDouble(byName["ratio"].InitialValue), 0.0001);
        }

        [TestMethod]
        public void ParseDict_HandlesNestedObjectEntries()
        {
            // Mixed dict where some entries are nested objects with type/offset
            const string json = """
                { "variables": {
                    "magic":     "",
                    "headerLen": { "type": "uint32", "offset": 0, "length": 4 }
                  } }
                """;

            var vars = WhfmtVariableParser.ParseDocument(json).ToDictionary(v => v.Name);
            Assert.AreEqual(WhfmtValueType.UInt32, vars["headerLen"].Type);
            Assert.AreEqual(0, vars["headerLen"].Offset);
            Assert.AreEqual(4, vars["headerLen"].Length);
        }

        // ----- Schema B (typed array) -------------------------------------------------

        [TestMethod]
        public void ParseTypedArray_ReadsAllFields()
        {
            const string json = """
                { "variables": [
                    { "name": "aoutMagic",    "type": "uint16", "offset": 0,  "length": 2, "endian": "big",    "description": "magic" },
                    { "name": "aoutTextSize", "type": "uint32", "offset": 4,  "length": 4, "endian": "big",    "description": "text"  },
                    { "name": "littleField",  "type": "uint16", "offset": 8,  "length": 2, "endian": "little"                          }
                  ] }
                """;

            var vars = WhfmtVariableParser.ParseDocument(json).ToDictionary(v => v.Name);
            Assert.AreEqual(3, vars.Count);

            Assert.AreEqual(WhfmtValueType.UInt16, vars["aoutMagic"].Type);
            Assert.AreEqual(0, vars["aoutMagic"].Offset);
            Assert.AreEqual(2, vars["aoutMagic"].Length);
            Assert.AreEqual(WhfmtEndian.Big, vars["aoutMagic"].Endian);
            Assert.AreEqual("magic", vars["aoutMagic"].Description);

            Assert.AreEqual(WhfmtEndian.Little, vars["littleField"].Endian);
            Assert.AreEqual("", vars["littleField"].Description);
        }

        [TestMethod]
        public void ParseTypedArray_SkipsEntriesWithoutName()
        {
            const string json = """
                { "variables": [
                    { "name": "valid",   "type": "uint8" },
                    { "type": "uint8" }
                  ] }
                """;
            var vars = WhfmtVariableParser.ParseDocument(json);
            Assert.AreEqual(1, vars.Count);
            Assert.AreEqual("valid", vars[0].Name);
        }

        // ----- Absent / malformed -----------------------------------------------------

        [TestMethod]
        public void ParseDocument_ReturnsEmptyWhenVariablesAbsent()
        {
            const string json = """{ "formatName": "X" }""";
            Assert.AreEqual(0, WhfmtVariableParser.ParseDocument(json).Count);
        }

        [TestMethod]
        public void ParseDocument_HandlesJsonc()
        {
            const string json = """
                // header comment
                { "variables": { "x": 1, } }
                """;
            var vars = WhfmtVariableParser.ParseDocument(json);
            Assert.AreEqual(1, vars.Count);
        }

        // ----- WhfmtVariableStore -----------------------------------------------------

        [TestMethod]
        public void Store_RegistersAndRetrievesByName()
        {
            var store = new WhfmtVariableStore();
            store.Register(new VariableDefinition("magic", WhfmtValueType.UInt32, 0, 4, WhfmtEndian.Little, "", 0x5A4D));

            Assert.IsTrue(store.Contains("magic"));
            Assert.IsTrue(store.TryGet<int>("magic", out var v));
            Assert.AreEqual(0x5A4D, v);
            Assert.AreEqual(1, store.Count);
        }

        [TestMethod]
        public void Store_SetOverwritesExistingValue()
        {
            var store = new WhfmtVariableStore();
            store.Register(new VariableDefinition("x", WhfmtValueType.Int32, 0, 4, WhfmtEndian.Little, "", 1));
            store.Set("x", 42);
            Assert.IsTrue(store.TryGet<int>("x", out var v));
            Assert.AreEqual(42, v);
        }

        [TestMethod]
        public void Store_TryGetReturnsFalseForMissing()
        {
            var store = new WhfmtVariableStore();
            Assert.IsFalse(store.TryGet<int>("nope", out var _));
        }

        [TestMethod]
        public void Store_TryGetConvertsCompatibleTypes()
        {
            var store = new WhfmtVariableStore();
            store.Set("count", (long)100);
            Assert.IsTrue(store.TryGet<int>("count", out var asInt));
            Assert.AreEqual(100, asInt);
        }

        [TestMethod]
        public void BuildStore_PopulatesFromJson()
        {
            const string json = """
                { "variables": {
                    "currentOffset": 0,
                    "magic": "MZ"
                  } }
                """;
            var store = WhfmtVariableParser.BuildStore(json);
            Assert.AreEqual(2, store.Count);
            Assert.IsTrue(store.TryGet<string>("magic", out var m));
            Assert.AreEqual("MZ", m);
        }
    }
}

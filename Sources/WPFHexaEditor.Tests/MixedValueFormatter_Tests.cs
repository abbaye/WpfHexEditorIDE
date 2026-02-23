//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using Microsoft.VisualStudio.TestTools.UnitTesting;
using WpfHexaEditor.Formatters;

namespace WPFHexaEditor.Tests
{
    [TestClass]
    public class MixedValueFormatter_Tests
    {
        private MixedValueFormatter _formatter;

        [TestInitialize]
        public void Setup()
        {
            _formatter = new MixedValueFormatter();
        }

        [TestMethod]
        public void DisplayName_ReturnsMixedSmart()
        {
            Assert.AreEqual("Mixed (Smart)", _formatter.DisplayName);
        }

        [TestMethod]
        public void Supports_AllTypes_ReturnsTrue()
        {
            Assert.IsTrue(_formatter.Supports("uint8"));
            Assert.IsTrue(_formatter.Supports("string"));
            Assert.IsTrue(_formatter.Supports("bytes"));
            Assert.IsTrue(_formatter.Supports("float"));
            Assert.IsTrue(_formatter.Supports("unknown_type"));
        }

        [TestMethod]
        public void Format_Byte_ShowsDecimalAndHex()
        {
            // Act
            string result = _formatter.Format((byte)42, "uint8", 1);

            // Assert
            Assert.AreEqual("42 (0x2A)", result);
        }

        [TestMethod]
        public void Format_SByte_Positive_ShowsDecimalAndHex()
        {
            // Act
            string result = _formatter.Format((sbyte)42, "int8", 1);

            // Assert
            Assert.AreEqual("42 (0x2A)", result);
        }

        [TestMethod]
        public void Format_SByte_Negative_ShowsDecimalAndHex()
        {
            // Act
            string result = _formatter.Format((sbyte)-42, "int8", 1);

            // Assert
            Assert.IsTrue(result.Contains("-42"));
            Assert.IsTrue(result.Contains("0x"));
        }

        [TestMethod]
        public void Format_UInt16_ShowsDecimalAndHex()
        {
            // Act
            string result = _formatter.Format((ushort)1000, "uint16", 2);

            // Assert
            Assert.AreEqual("1000 (0x03E8)", result);
        }

        [TestMethod]
        public void Format_UInt32_ShowsDecimalWithThousandsSeparator()
        {
            // Act
            string result = _formatter.Format(1234567u, "uint32", 4);

            // Assert
            Assert.IsTrue(result.Contains("1,234,567"));
            Assert.IsTrue(result.Contains("0x0012D687"));
        }

        [TestMethod]
        public void Format_Int32_Negative_ShowsDecimalAndHex()
        {
            // Act
            string result = _formatter.Format(-42, "int32", 4);

            // Assert
            Assert.IsTrue(result.Contains("-42"));
            Assert.IsTrue(result.Contains("0x"));
        }

        [TestMethod]
        public void Format_UInt64_ShowsDecimalWithThousandsSeparator()
        {
            // Act
            string result = _formatter.Format(12345678901234UL, "uint64", 8);

            // Assert
            Assert.IsTrue(result.Contains("12,345,678,901,234"));
            Assert.IsTrue(result.Contains("0x"));
        }

        [TestMethod]
        public void Format_Float_ShowsDecimalWithPrecision()
        {
            // Act
            string result = _formatter.Format(3.14159f, "float", 4);

            // Assert
            Assert.IsTrue(result.StartsWith("3.14159"));
        }

        [TestMethod]
        public void Format_Float_NaN_ShowsNaN()
        {
            // Act
            string result = _formatter.Format(float.NaN, "float", 4);

            // Assert
            Assert.AreEqual("NaN", result);
        }

        [TestMethod]
        public void Format_Float_PositiveInfinity_ShowsInfinity()
        {
            // Act
            string result = _formatter.Format(float.PositiveInfinity, "float", 4);

            // Assert
            Assert.AreEqual("+Infinity", result);
        }

        [TestMethod]
        public void Format_Float_NegativeInfinity_ShowsInfinity()
        {
            // Act
            string result = _formatter.Format(float.NegativeInfinity, "float", 4);

            // Assert
            Assert.AreEqual("-Infinity", result);
        }

        [TestMethod]
        public void Format_Double_ShowsDecimalWithPrecision()
        {
            // Act
            string result = _formatter.Format(3.141592653589793, "double", 8);

            // Assert
            Assert.IsTrue(result.StartsWith("3.14159"));
        }

        [TestMethod]
        public void Format_String_ShowsTextWithLength()
        {
            // Act
            string result = _formatter.Format("Hello", "string", 5);

            // Assert
            Assert.AreEqual("\"Hello\" (5 bytes)", result);
        }

        [TestMethod]
        public void Format_String_Empty_ShowsEmpty()
        {
            // Act
            string result = _formatter.Format("", "string", 0);

            // Assert
            Assert.AreEqual("<empty>", result);
        }

        [TestMethod]
        public void Format_String_EscapesSpecialChars()
        {
            // Act
            string result = _formatter.Format("Line1\r\nLine2\tTab", "string", 15);

            // Assert
            Assert.IsTrue(result.Contains("\\r\\n"));
            Assert.IsTrue(result.Contains("\\t"));
        }

        [TestMethod]
        public void Format_String_TruncatesLongStrings()
        {
            // Arrange
            string longString = new string('A', 100);

            // Act
            string result = _formatter.Format(longString, "string", 100);

            // Assert
            Assert.IsTrue(result.Contains("..."), "Long strings should be truncated");
            Assert.IsTrue(result.Length < longString.Length + 20, "Result should be shorter than original");
        }

        [TestMethod]
        public void Format_ByteArray_Short_ShowsAllBytes()
        {
            // Arrange
            byte[] bytes = { 0x50, 0x4B, 0x03, 0x04 };

            // Act
            string result = _formatter.Format(bytes, "bytes", 4);

            // Assert
            Assert.IsTrue(result.Contains("50 4B 03 04"));
            Assert.IsTrue(result.Contains("\"PK..\""));
        }

        [TestMethod]
        public void Format_ByteArray_Long_ShowsFirstAndLast()
        {
            // Arrange
            byte[] bytes = new byte[20];
            for (int i = 0; i < 20; i++)
                bytes[i] = (byte)i;

            // Act
            string result = _formatter.Format(bytes, "bytes", 20);

            // Assert
            Assert.IsTrue(result.Contains("..."), "Long arrays should show ellipsis");
            Assert.IsTrue(result.Contains("(20 bytes)"), "Should show total byte count");
        }

        [TestMethod]
        public void Format_ByteArray_Empty_ShowsEmptyBrackets()
        {
            // Arrange
            byte[] bytes = new byte[0];

            // Act
            string result = _formatter.Format(bytes, "bytes", 0);

            // Assert
            Assert.AreEqual("[]", result);
        }

        [TestMethod]
        public void Format_Null_ReturnsNull()
        {
            // Act
            string result = _formatter.Format(null, "uint32", 4);

            // Assert
            Assert.AreEqual("null", result);
        }

        [TestMethod]
        public void Format_ASCII_NonPrintable_ShowsDots()
        {
            // Arrange
            byte[] bytes = { 0x00, 0x01, 0x02, 0x48, 0x69 }; // \0\x01\x02Hi

            // Act
            string result = _formatter.Format(bytes, "bytes", 5);

            // Assert
            Assert.IsTrue(result.Contains("\"...Hi\""), "Non-printable chars should show as dots");
        }

        [TestMethod]
        public void Format_UnknownType_ShowsHex()
        {
            // Act
            string result = _formatter.Format(42, "unknown_type", 4);

            // Assert
            Assert.IsTrue(result.StartsWith("0x"));
        }
    }
}

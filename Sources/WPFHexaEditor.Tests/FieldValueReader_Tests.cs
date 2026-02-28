//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text;
using WpfHexEditor.Core;

namespace WPFHexaEditor.Tests
{
    [TestClass]
    public class FieldValueReader_Tests
    {
        private FieldValueReader _reader;

        [TestInitialize]
        public void Setup()
        {
            _reader = new FieldValueReader();
        }

        #region Integer Reading Tests

        [TestMethod]
        public void ReadValue_UInt8_ReturnsCorrectValue()
        {
            var data = new byte[] { 0x42, 0x00, 0x00, 0x00 };
            var result = _reader.ReadValue(data, 0, 1, "uint8");

            Assert.AreEqual((byte)0x42, result);
        }

        [TestMethod]
        public void ReadValue_Int8_ReturnsCorrectValue()
        {
            var data = new byte[] { 0xFF, 0x00, 0x00, 0x00 }; // -1 in sbyte
            var result = _reader.ReadValue(data, 0, 1, "int8");

            Assert.AreEqual((sbyte)-1, result);
        }

        [TestMethod]
        public void ReadValue_UInt16_LittleEndian_ReturnsCorrectValue()
        {
            var data = new byte[] { 0x34, 0x12, 0x00, 0x00 }; // 0x1234 little-endian
            var result = _reader.ReadValue(data, 0, 2, "uint16", bigEndian: false);

            Assert.AreEqual((ushort)0x1234, result);
        }

        [TestMethod]
        public void ReadValue_UInt16_BigEndian_ReturnsCorrectValue()
        {
            var data = new byte[] { 0x12, 0x34, 0x00, 0x00 }; // 0x1234 big-endian
            var result = _reader.ReadValue(data, 0, 2, "uint16", bigEndian: true);

            Assert.AreEqual((ushort)0x1234, result);
        }

        [TestMethod]
        public void ReadValue_UInt32_LittleEndian_ReturnsCorrectValue()
        {
            var data = new byte[] { 0x78, 0x56, 0x34, 0x12 }; // 0x12345678 little-endian
            var result = _reader.ReadValue(data, 0, 4, "uint32", bigEndian: false);

            Assert.AreEqual((uint)0x12345678, result);
        }

        [TestMethod]
        public void ReadValue_UInt32_BigEndian_ReturnsCorrectValue()
        {
            var data = new byte[] { 0x12, 0x34, 0x56, 0x78 }; // 0x12345678 big-endian
            var result = _reader.ReadValue(data, 0, 4, "uint32", bigEndian: true);

            Assert.AreEqual((uint)0x12345678, result);
        }

        [TestMethod]
        public void ReadValue_UInt64_LittleEndian_ReturnsCorrectValue()
        {
            var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
            var result = _reader.ReadValue(data, 0, 8, "uint64", bigEndian: false);

            Assert.AreEqual((ulong)0x0807060504030201, result);
        }

        [TestMethod]
        public void ReadValue_UInt64_BigEndian_ReturnsCorrectValue()
        {
            var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
            var result = _reader.ReadValue(data, 0, 8, "uint64", bigEndian: true);

            Assert.AreEqual((ulong)0x0102030405060708, result);
        }

        [TestMethod]
        public void ReadValue_Int16_ReturnsCorrectNegativeValue()
        {
            var data = new byte[] { 0xFF, 0xFF, 0x00, 0x00 }; // -1 in little-endian
            var result = _reader.ReadValue(data, 0, 2, "int16");

            Assert.AreEqual((short)-1, result);
        }

        [TestMethod]
        public void ReadValue_Int32_ReturnsCorrectNegativeValue()
        {
            var data = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }; // -1
            var result = _reader.ReadValue(data, 0, 4, "int32");

            Assert.AreEqual(-1, result);
        }

        #endregion

        #region Floating-Point Tests

        [TestMethod]
        public void ReadValue_Float_ReturnsCorrectValue()
        {
            float expectedValue = 3.14159f;
            var data = BitConverter.GetBytes(expectedValue);
            var result = _reader.ReadValue(data, 0, 4, "float");

            Assert.IsInstanceOfType(result, typeof(float));
            Assert.AreEqual(expectedValue, (float)result, 0.0001f);
        }

        [TestMethod]
        public void ReadValue_Double_ReturnsCorrectValue()
        {
            double expectedValue = 3.141592653589793;
            var data = BitConverter.GetBytes(expectedValue);
            var result = _reader.ReadValue(data, 0, 8, "double");

            Assert.IsInstanceOfType(result, typeof(double));
            Assert.AreEqual(expectedValue, (double)result, 0.0000001);
        }

        #endregion

        #region String Tests

        [TestMethod]
        public void ReadValue_String_ASCII_ReturnsCorrectString()
        {
            var data = Encoding.ASCII.GetBytes("Hello");
            var result = _reader.ReadValue(data, 0, 5, "string");

            Assert.AreEqual("Hello", result);
        }

        [TestMethod]
        public void ReadValue_String_UTF8_ReturnsCorrectString()
        {
            var text = "Bonjour";
            var data = Encoding.UTF8.GetBytes(text);
            var result = _reader.ReadValue(data, 0, data.Length, "utf8");

            Assert.AreEqual(text, result);
        }

        [TestMethod]
        public void ReadValue_String_UTF16_ReturnsCorrectString()
        {
            var text = "Hello世界";
            var data = Encoding.Unicode.GetBytes(text);
            var result = _reader.ReadValue(data, 0, data.Length, "utf16");

            Assert.AreEqual(text, result);
        }

        [TestMethod]
        public void ReadValue_String_WithNullTerminator_TrimsCorrectly()
        {
            var data = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x00, 0x58, 0x58 }; // "Hello\0XX"
            var result = _reader.ReadValue(data, 0, 8, "string");

            Assert.AreEqual("Hello", result);
        }

        [TestMethod]
        public void ReadStringNullTerminated_ASCII_ReturnsCorrectString()
        {
            var data = Encoding.ASCII.GetBytes("Hello\0World");
            var result = _reader.ReadStringNullTerminated(data, 0, 100);

            Assert.AreEqual("Hello", result);
        }

        [TestMethod]
        public void ReadStringNullTerminated_EmptyString_ReturnsEmpty()
        {
            var data = new byte[] { 0x00, 0x48, 0x65 }; // "\0He"
            var result = _reader.ReadStringNullTerminated(data, 0, 100);

            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void ReadStringNullTerminated_MaxLength_Enforced()
        {
            var data = Encoding.ASCII.GetBytes("ThisIsAVeryLongStringWithoutNullTerminator");
            var result = _reader.ReadStringNullTerminated(data, 0, maxLength: 10);

            Assert.AreEqual(10, result.Length);
        }

        [TestMethod]
        public void ReadStringUTF8NullTerminated_ReturnsCorrectString()
        {
            var data = Encoding.UTF8.GetBytes("Bonjour\0Monde");
            var result = _reader.ReadStringUTF8NullTerminated(data, 0, 100);

            Assert.AreEqual("Bonjour", result);
        }

        #endregion

        #region Bytes Tests

        [TestMethod]
        public void ReadValue_Bytes_ReturnsCorrectByteArray()
        {
            var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
            var result = _reader.ReadValue(data, 1, 3, "bytes");

            Assert.IsInstanceOfType(result, typeof(byte[]));
            var bytes = (byte[])result;
            CollectionAssert.AreEqual(new byte[] { 0x02, 0x03, 0x04 }, bytes);
        }

        [TestMethod]
        public void ReadBytesAsHex_ReturnsCorrectHexString()
        {
            var data = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // ZIP signature
            var result = _reader.ReadBytesAsHex(data, 0, 4);

            Assert.AreEqual("504B0304", result);
        }

        [TestMethod]
        public void ReadBytesAsHex_EmptyArray_ReturnsEmptyString()
        {
            var data = new byte[] { };
            var result = _reader.ReadBytesAsHex(data, 0, 0);

            Assert.AreEqual(string.Empty, result);
        }

        #endregion

        #region Signature Checking Tests

        [TestMethod]
        public void CheckSignature_ValidSignature_ReturnsTrue()
        {
            var data = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00, 0x00 }; // ZIP signature
            var result = FieldValueReader.CheckSignature(data, 0, "504B0304");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void CheckSignature_InvalidSignature_ReturnsFalse()
        {
            var data = new byte[] { 0x50, 0x4B, 0x03, 0x05, 0x00, 0x00 }; // Wrong byte at position 3
            var result = FieldValueReader.CheckSignature(data, 0, "504B0304");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void CheckSignature_AtOffset_ReturnsTrue()
        {
            var data = new byte[] { 0xFF, 0xFF, 0x50, 0x4B, 0x03, 0x04 }; // ZIP signature at offset 2
            var result = FieldValueReader.CheckSignature(data, 2, "504B0304");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void CheckSignature_OutOfBounds_ReturnsFalse()
        {
            var data = new byte[] { 0x50, 0x4B }; // Too short
            var result = FieldValueReader.CheckSignature(data, 0, "504B0304");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void CheckSignature_NullData_ReturnsFalse()
        {
            var result = FieldValueReader.CheckSignature(null, 0, "504B0304");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void CheckSignature_EmptySignature_ReturnsFalse()
        {
            var data = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
            var result = FieldValueReader.CheckSignature(data, 0, "");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void FindSignature_AtBeginning_ReturnsZero()
        {
            var data = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00, 0x00 };
            var result = FieldValueReader.FindSignature(data, "504B0304");

            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public void FindSignature_AtOffset_ReturnsCorrectOffset()
        {
            var data = new byte[] { 0xFF, 0xFF, 0xFF, 0x50, 0x4B, 0x03, 0x04 };
            var result = FieldValueReader.FindSignature(data, "504B0304");

            Assert.AreEqual(3, result);
        }

        [TestMethod]
        public void FindSignature_NotFound_ReturnsMinusOne()
        {
            var data = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
            var result = FieldValueReader.FindSignature(data, "504B0304");

            Assert.AreEqual(-1, result);
        }

        [TestMethod]
        public void FindSignature_WithStartOffset_FindsCorrectMatch()
        {
            // Two ZIP signatures at offset 0 and 10
            var data = new byte[] {
                0x50, 0x4B, 0x03, 0x04,  // First ZIP signature
                0xFF, 0xFF,
                0x50, 0x4B, 0x03, 0x04,  // Second ZIP signature at offset 6
                0xFF, 0xFF
            };

            var result = FieldValueReader.FindSignature(data, "504B0304", startOffset: 4);

            Assert.AreEqual(6, result); // Should find second signature
        }

        [TestMethod]
        public void FindSignature_WithMaxOffset_StopsSearching()
        {
            var data = new byte[] {
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0x50, 0x4B, 0x03, 0x04  // Signature at offset 5
            };

            var result = FieldValueReader.FindSignature(data, "504B0304", startOffset: 0, maxOffset: 4);

            Assert.AreEqual(-1, result); // Should not find (search stops before offset 5)
        }

        [TestMethod]
        public void FindSignature_HexWithSpaces_HandlesCorrectly()
        {
            var data = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
            var result = FieldValueReader.FindSignature(data, "50 4B 03 04");

            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public void FindSignature_HexWithDashes_HandlesCorrectly()
        {
            var data = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
            var result = FieldValueReader.FindSignature(data, "50-4B-03-04");

            Assert.AreEqual(0, result);
        }

        #endregion

        #region Boundary Tests

        [TestMethod]
        public void ReadValue_NullData_ReturnsNull()
        {
            var result = _reader.ReadValue(null, 0, 4, "uint32");

            Assert.IsNull(result);
        }

        [TestMethod]
        public void ReadValue_NegativeOffset_ReturnsNull()
        {
            var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var result = _reader.ReadValue(data, -1, 4, "uint32");

            Assert.IsNull(result);
        }

        [TestMethod]
        public void ReadValue_OffsetOutOfBounds_ReturnsNull()
        {
            var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var result = _reader.ReadValue(data, 10, 4, "uint32");

            Assert.IsNull(result);
        }

        [TestMethod]
        public void ReadValue_LengthExceedsData_ReturnsNull()
        {
            var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var result = _reader.ReadValue(data, 2, 10, "bytes");

            Assert.IsNull(result);
        }

        [TestMethod]
        public void ReadStringNullTerminated_NullData_ReturnsEmpty()
        {
            var result = _reader.ReadStringNullTerminated(null, 0);

            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void ReadStringNullTerminated_NegativeOffset_ReturnsEmpty()
        {
            var data = new byte[] { 0x48, 0x65 };
            var result = _reader.ReadStringNullTerminated(data, -1);

            Assert.AreEqual(string.Empty, result);
        }

        #endregion

        #region ShouldUseBigEndian Tests

        [TestMethod]
        public void ShouldUseBigEndian_NetworkFormat_ReturnsTrue()
        {
            Assert.IsTrue(FieldValueReader.ShouldUseBigEndian("TCP Network Packet"));
            Assert.IsTrue(FieldValueReader.ShouldUseBigEndian("IP Header"));
            Assert.IsTrue(FieldValueReader.ShouldUseBigEndian("PCAP File"));
        }

        [TestMethod]
        public void ShouldUseBigEndian_JavaClass_ReturnsTrue()
        {
            Assert.IsTrue(FieldValueReader.ShouldUseBigEndian("Java Class File"));
            Assert.IsTrue(FieldValueReader.ShouldUseBigEndian("CLASS"));
        }

        [TestMethod]
        public void ShouldUseBigEndian_TIFFMotorola_ReturnsTrue()
        {
            Assert.IsTrue(FieldValueReader.ShouldUseBigEndian("TIFF (Motorola)"));
        }

        [TestMethod]
        public void ShouldUseBigEndian_StandardFormat_ReturnsFalse()
        {
            Assert.IsFalse(FieldValueReader.ShouldUseBigEndian("ZIP Archive"));
            Assert.IsFalse(FieldValueReader.ShouldUseBigEndian("PE Executable"));
            Assert.IsFalse(FieldValueReader.ShouldUseBigEndian("PNG Image"));
        }

        [TestMethod]
        public void ShouldUseBigEndian_EmptyString_ReturnsFalse()
        {
            Assert.IsFalse(FieldValueReader.ShouldUseBigEndian(""));
            Assert.IsFalse(FieldValueReader.ShouldUseBigEndian(null));
        }

        #endregion
    }
}

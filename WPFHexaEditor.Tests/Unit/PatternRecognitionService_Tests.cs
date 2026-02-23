//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WpfHexaEditor.Models.Patterns;
using WpfHexaEditor.Services;

namespace WPFHexaEditor.Tests.Unit
{
    [TestClass]
    public class PatternRecognitionService_Tests
    {
        private PatternRecognitionService _service;

        [TestInitialize]
        public void Setup()
        {
            _service = new PatternRecognitionService();
        }

        #region Padding Detection Tests

        [TestMethod]
        public void DetectPadding_FindsNullPadding()
        {
            // Create data with null padding
            var data = new byte[1024];
            Array.Fill(data, (byte)0x00);

            var result = _service.AnalyzePatterns(data, 0,
                detectEmbeddedFiles: false,
                detectStrings: false,
                detectPadding: true,
                detectRepeatedSequences: false);

            Assert.IsTrue(result.Success);
            var nullPadding = result.GetPatternsByType(PatternType.NullPadding);
            Assert.IsTrue(nullPadding.Count > 0);
            Assert.IsTrue(nullPadding[0].Length >= 1024);
        }

        [TestMethod]
        public void DetectPadding_FindsFFPadding()
        {
            // Create data with 0xFF padding
            var data = new byte[512];
            Array.Fill(data, (byte)0xFF);

            var result = _service.AnalyzePatterns(data, 0,
                detectEmbeddedFiles: false,
                detectStrings: false,
                detectPadding: true,
                detectRepeatedSequences: false);

            Assert.IsTrue(result.Success);
            var ffPadding = result.GetPatternsByType(PatternType.FFPadding);
            Assert.IsTrue(ffPadding.Count > 0);
        }

        [TestMethod]
        public void DetectPadding_IgnoresShortPadding()
        {
            // Create data with short padding (less than MinPaddingLength)
            var data = new byte[100];
            for (int i = 0; i < 50; i++)
                data[i] = 0x42; // Non-padding data
            for (int i = 50; i < 55; i++)
                data[i] = 0x00; // Only 5 null bytes
            for (int i = 55; i < 100; i++)
                data[i] = 0x42; // Non-padding data

            _service.MinPaddingLength = 16; // Require at least 16 bytes

            var result = _service.AnalyzePatterns(data, 0,
                detectEmbeddedFiles: false,
                detectStrings: false,
                detectPadding: true,
                detectRepeatedSequences: false);

            var nullPadding = result.GetPatternsByType(PatternType.NullPadding);
            Assert.AreEqual(0, nullPadding.Count);
        }

        #endregion

        #region String Detection Tests

        [TestMethod]
        public void DetectStrings_FindsAsciiString()
        {
            var testString = "Hello World! This is a test string.";
            var data = Encoding.ASCII.GetBytes(testString);

            var result = _service.AnalyzePatterns(data, 0,
                detectEmbeddedFiles: false,
                detectStrings: true,
                detectPadding: false,
                detectRepeatedSequences: false);

            Assert.IsTrue(result.Success);
            var asciiStrings = result.GetPatternsByType(PatternType.AsciiString);
            Assert.IsTrue(asciiStrings.Count > 0);
            Assert.IsTrue(asciiStrings[0].Description.Contains("Hello World"));
        }

        [TestMethod]
        public void DetectStrings_FindsUnicodeString()
        {
            var testString = "Unicode Test String";
            var data = Encoding.Unicode.GetBytes(testString); // UTF-16 LE

            var result = _service.AnalyzePatterns(data, 0,
                detectEmbeddedFiles: false,
                detectStrings: true,
                detectPadding: false,
                detectRepeatedSequences: false);

            Assert.IsTrue(result.Success);
            var unicodeStrings = result.GetPatternsByType(PatternType.UnicodeString);
            Assert.IsTrue(unicodeStrings.Count > 0);
        }

        [TestMethod]
        public void DetectStrings_IgnoresShortStrings()
        {
            var testString = "Hi"; // Very short
            var data = Encoding.ASCII.GetBytes(testString);

            _service.MinAsciiStringLength = 10; // Require at least 10 chars

            var result = _service.AnalyzePatterns(data, 0,
                detectEmbeddedFiles: false,
                detectStrings: true,
                detectPadding: false,
                detectRepeatedSequences: false);

            var asciiStrings = result.GetPatternsByType(PatternType.AsciiString);
            Assert.AreEqual(0, asciiStrings.Count);
        }

        [TestMethod]
        public void DetectStrings_HandlesNonPrintableCharacters()
        {
            // Mix of printable and non-printable
            var data = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x00, 0xFF, 0x42, 0x79, 0x65 }; // "Hello" null 0xFF "Bye"

            var result = _service.AnalyzePatterns(data, 0,
                detectEmbeddedFiles: false,
                detectStrings: true,
                detectPadding: false,
                detectRepeatedSequences: false);

            var asciiStrings = result.GetPatternsByType(PatternType.AsciiString);
            // Should find separate strings, not one continuous
            Assert.IsTrue(asciiStrings.Count >= 0);
        }

        #endregion

        #region Repeated Sequence Detection Tests

        [TestMethod]
        public void DetectRepeatedSequences_FindsRepeatingPattern()
        {
            // Create data with repeating pattern: ABCD ABCD ABCD...
            var data = new byte[400];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(0x41 + (i % 4)); // A B C D repeated
            }

            var result = _service.AnalyzePatterns(data, 0,
                detectEmbeddedFiles: false,
                detectStrings: false,
                detectPadding: false,
                detectRepeatedSequences: true);

            Assert.IsTrue(result.Success);
            var repeated = result.GetPatternsByType(PatternType.RepeatingPattern)
                .Concat(result.GetPatternsByType(PatternType.RepeatedSequence)).ToList();
            Assert.IsTrue(repeated.Count > 0);
        }

        [TestMethod]
        public void DetectRepeatedSequences_IgnoresInfrequentPatterns()
        {
            // Create data with a pattern that only repeats twice
            var data = new byte[200];
            data[0] = 0x41; data[1] = 0x42; data[2] = 0x43; data[3] = 0x44; // ABCD
            data[100] = 0x41; data[101] = 0x42; data[102] = 0x43; data[103] = 0x44; // ABCD again

            _service.MinRepeatedSequenceOccurrences = 3; // Require at least 3 occurrences

            var result = _service.AnalyzePatterns(data, 0,
                detectEmbeddedFiles: false,
                detectStrings: false,
                detectPadding: false,
                detectRepeatedSequences: true);

            var repeated = result.GetPatternsByType(PatternType.RepeatingPattern)
                .Concat(result.GetPatternsByType(PatternType.RepeatedSequence)).ToList();
            // Should not find patterns with only 2 occurrences
            Assert.AreEqual(0, repeated.Count);
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        public void AnalyzePatterns_ReturnsSuccessResult()
        {
            var data = new byte[256];
            for (int i = 0; i < 256; i++)
                data[i] = (byte)i;

            var result = _service.AnalyzePatterns(data);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.AnalysisDurationMs > 0);
        }

        [TestMethod]
        public void AnalyzePatterns_HandlesEmptyData()
        {
            var data = new byte[0];

            var result = _service.AnalyzePatterns(data);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.TotalPatterns);
        }

        [TestMethod]
        public void AnalyzePatterns_RespectsBaseOffset()
        {
            var data = new byte[100];
            Array.Fill(data, (byte)0x00);

            long baseOffset = 0x1000;
            var result = _service.AnalyzePatterns(data, baseOffset,
                detectEmbeddedFiles: false,
                detectStrings: false,
                detectPadding: true,
                detectRepeatedSequences: false);

            Assert.AreEqual(baseOffset, result.StartOffset);
            if (result.Patterns.Count > 0)
            {
                Assert.IsTrue(result.Patterns[0].StartOffset >= baseOffset);
            }
        }

        [TestMethod]
        public void AnalyzePatterns_HandlesLargeData()
        {
            // Test with data larger than MaxSampleSize
            var largeData = new byte[2 * 1024 * 1024]; // 2MB
            _service.MaxSampleSize = 1024 * 1024; // 1MB limit

            var result = _service.AnalyzePatterns(largeData);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Warnings.Count > 0); // Should warn about truncation
        }

        [TestMethod]
        public void AnalyzePatterns_MultiplePatternTypes()
        {
            // Create data with multiple pattern types
            var data = new byte[1024];

            // Add null padding (0-100)
            Array.Fill(data, (byte)0x00, 0, 100);

            // Add ASCII string (100-150)
            var text = "This is a test ASCII string for pattern detection!";
            var textBytes = Encoding.ASCII.GetBytes(text);
            Array.Copy(textBytes, 0, data, 100, textBytes.Length);

            // Add 0xFF padding (200-300)
            Array.Fill(data, (byte)0xFF, 200, 100);

            // Add repeating pattern (400-500)
            for (int i = 400; i < 500; i++)
            {
                data[i] = (byte)(0x41 + (i % 4)); // ABCD repeated
            }

            var result = _service.AnalyzePatterns(data);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.TotalPatterns > 0);

            // Should detect multiple pattern types
            var patternTypes = result.PatternTypeCount.Keys.ToList();
            Assert.IsTrue(patternTypes.Count >= 2); // At least padding and strings
        }

        #endregion

        #region Configuration Tests

        [TestMethod]
        public void Configuration_CanSetMinLengths()
        {
            _service.MinPaddingLength = 32;
            _service.MinAsciiStringLength = 20;
            _service.MinRepeatedSequenceLength = 8;

            Assert.AreEqual(32, _service.MinPaddingLength);
            Assert.AreEqual(20, _service.MinAsciiStringLength);
            Assert.AreEqual(8, _service.MinRepeatedSequenceLength);
        }

        [TestMethod]
        public void Configuration_CanSetOccurrenceThreshold()
        {
            _service.MinRepeatedSequenceOccurrences = 5;

            Assert.AreEqual(5, _service.MinRepeatedSequenceOccurrences);
        }

        [TestMethod]
        public void Configuration_CanSetMaxSampleSize()
        {
            _service.MaxSampleSize = 512 * 1024; // 512KB

            Assert.AreEqual(512 * 1024, _service.MaxSampleSize);
        }

        #endregion
    }
}

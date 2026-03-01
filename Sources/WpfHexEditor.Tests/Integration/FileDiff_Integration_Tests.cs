//////////////////////////////////////////////
// Apache 2.0  - 2026
// File Diff Integration Tests
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com), Claude Sonnet 4.6
//////////////////////////////////////////////

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using WpfHexEditor.Core.Bytes;
using WpfHexEditor.Core.Models.Comparison;
using WpfHexEditor.Core.Services;

namespace WpfHexEditor.Tests.Integration
{
    [TestClass]
    public class FileDiff_Integration_Tests
    {
        private FileDiffService _service;

        [TestInitialize]
        public void Setup()
        {
            _service = new FileDiffService();
        }

        [TestMethod]
        public void CompareFiles_IdenticalFiles_ReturnsNoDifferences()
        {
            // Arrange
            var bytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var provider1 = new ByteProvider();
            provider1.OpenStream(new MemoryStream(bytes));
            var provider2 = new ByteProvider();
            provider2.OpenStream(new MemoryStream(bytes));

            // Act
            var differences = _service.CompareFiles(provider1, provider2);

            // Assert
            Assert.AreEqual(0, differences.Count);
        }

        [TestMethod]
        public void CompareFiles_SingleByteDifference_ReturnsOneDifference()
        {
            // Arrange
            var bytes1 = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var bytes2 = new byte[] { 0x01, 0xFF, 0x03, 0x04 };
            var provider1 = new ByteProvider();
            provider1.OpenStream(new MemoryStream(bytes1));
            var provider2 = new ByteProvider();
            provider2.OpenStream(new MemoryStream(bytes2));

            // Act
            var differences = _service.CompareFiles(provider1, provider2);

            // Assert
            Assert.AreEqual(1, differences.Count);
            Assert.AreEqual(DifferenceType.Modified, differences[0].Type);
            Assert.AreEqual(1, differences[0].Offset);
            Assert.AreEqual(1, differences[0].Length);
        }

        [TestMethod]
        public void CompareFiles_MultipleDifferences_ReturnsAllDifferences()
        {
            // Arrange
            var bytes1 = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
            var bytes2 = new byte[] { 0x01, 0xFF, 0x03, 0xAA, 0x05 };
            var provider1 = new ByteProvider();
            provider1.OpenStream(new MemoryStream(bytes1));
            var provider2 = new ByteProvider();
            provider2.OpenStream(new MemoryStream(bytes2));

            // Act
            var differences = _service.CompareFiles(provider1, provider2);

            // Assert
            Assert.AreEqual(2, differences.Count);
        }

        [TestMethod]
        public void CompareFiles_File1Longer_ReturnsDeletedDifference()
        {
            // Arrange
            var bytes1 = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
            var bytes2 = new byte[] { 0x01, 0x02, 0x03 };
            var provider1 = new ByteProvider();
            provider1.OpenStream(new MemoryStream(bytes1));
            var provider2 = new ByteProvider();
            provider2.OpenStream(new MemoryStream(bytes2));

            // Act
            var differences = _service.CompareFiles(provider1, provider2);

            // Assert
            Assert.IsTrue(differences.Any(d => d.Type == DifferenceType.DeletedInSecond));
            var deletedDiff = differences.First(d => d.Type == DifferenceType.DeletedInSecond);
            Assert.AreEqual(3, deletedDiff.Offset);
            Assert.AreEqual(2, deletedDiff.Length);
        }

        [TestMethod]
        public void CompareFiles_File2Longer_ReturnsAddedDifference()
        {
            // Arrange
            var bytes1 = new byte[] { 0x01, 0x02, 0x03 };
            var bytes2 = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
            var provider1 = new ByteProvider();
            provider1.OpenStream(new MemoryStream(bytes1));
            var provider2 = new ByteProvider();
            provider2.OpenStream(new MemoryStream(bytes2));

            // Act
            var differences = _service.CompareFiles(provider1, provider2);

            // Assert
            Assert.IsTrue(differences.Any(d => d.Type == DifferenceType.AddedInSecond));
            var addedDiff = differences.First(d => d.Type == DifferenceType.AddedInSecond);
            Assert.AreEqual(3, addedDiff.Offset);
            Assert.AreEqual(2, addedDiff.Length);
        }

        [TestMethod]
        public void GetStatistics_MultipleTypes_ReturnsCorrectCounts()
        {
            // Arrange
            var bytes1 = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
            var bytes2 = new byte[] { 0x01, 0xFF, 0x03 };
            var provider1 = new ByteProvider();
            provider1.OpenStream(new MemoryStream(bytes1));
            var provider2 = new ByteProvider();
            provider2.OpenStream(new MemoryStream(bytes2));
            var differences = _service.CompareFiles(provider1, provider2);

            // Act
            var stats = _service.GetStatistics(differences);

            // Assert
            Assert.IsTrue(stats.ModifiedCount > 0);
            Assert.IsTrue(stats.DeletedCount > 0);
        }

        [TestMethod]
        public void ExportDiffReport_ValidDifferences_ReturnsReport()
        {
            // Arrange
            var bytes1 = new byte[] { 0x01, 0x02, 0x03 };
            var bytes2 = new byte[] { 0x01, 0xFF, 0x03 };
            var provider1 = new ByteProvider();
            provider1.OpenStream(new MemoryStream(bytes1));
            var provider2 = new ByteProvider();
            provider2.OpenStream(new MemoryStream(bytes2));
            var differences = _service.CompareFiles(provider1, provider2);
            var stats = _service.GetStatistics(differences);

            // Act
            var report = _service.ExportDiffReport(differences, stats);

            // Assert
            Assert.IsFalse(string.IsNullOrEmpty(report));
            Assert.IsTrue(report.Contains("File Comparison Report"));
            Assert.IsTrue(report.Contains("Total Differences"));
        }
    }
}

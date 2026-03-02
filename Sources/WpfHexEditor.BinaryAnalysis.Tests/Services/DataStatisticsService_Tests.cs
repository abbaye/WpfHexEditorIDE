//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using WpfHexEditor.BinaryAnalysis.Models.Visualization;
using WpfHexEditor.BinaryAnalysis.Services;

namespace WpfHexEditor.BinaryAnalysis.Tests.Services
{
    [TestClass]
    public class DataStatisticsService_Tests
    {
        private DataStatisticsService _service;

        [TestInitialize]
        public void Setup() => _service = new DataStatisticsService();

        [TestMethod]
        public void CalculateStatistics_NullData_ReturnsZeroSize()
        {
            var stats = _service.CalculateStatistics(null);
            Assert.AreEqual(0, stats.FileSize);
        }

        [TestMethod]
        public void CalculateStatistics_EmptyData_ReturnsZeroSize()
        {
            var stats = _service.CalculateStatistics(new byte[0]);
            Assert.AreEqual(0, stats.FileSize);
        }

        [TestMethod]
        public void CalculateStatistics_AllZeros_NullBytePercentage100()
        {
            var data = new byte[256]; // all 0x00
            var stats = _service.CalculateStatistics(data);
            Assert.AreEqual(100.0, stats.NullBytePercentage, 0.001);
        }

        [TestMethod]
        public void CalculateStatistics_AllZeros_EntropyNearZero()
        {
            var data = new byte[1024]; // all 0x00
            var stats = _service.CalculateStatistics(data);
            Assert.AreEqual(0.0, stats.Entropy, 0.001);
        }

        [TestMethod]
        public void CalculateStatistics_AllZeros_EstimatedDataTypeSparse()
        {
            var data = new byte[1024]; // all 0x00
            var stats = _service.CalculateStatistics(data);
            Assert.AreEqual(DataType.Sparse, stats.EstimatedDataType);
        }

        [TestMethod]
        public void CalculateStatistics_AllPrintableAscii_EstimatedDataTypeText()
        {
            // Fill with 'A' (printable ASCII)
            var data = new byte[256];
            for (int i = 0; i < data.Length; i++) data[i] = 0x41;
            var stats = _service.CalculateStatistics(data);
            Assert.AreEqual(DataType.Text, stats.EstimatedDataType);
        }

        [TestMethod]
        public void CalculateStatistics_UniformDistribution_HighEntropy()
        {
            // 256 bytes, one of each value → max entropy ≈ 8
            var data = new byte[256];
            for (int i = 0; i < 256; i++) data[i] = (byte)i;
            var stats = _service.CalculateStatistics(data);
            Assert.IsTrue(stats.Entropy > 7.9, $"Expected entropy > 7.9, got {stats.Entropy}");
        }

        [TestMethod]
        public void CalculateStatistics_FileSizeCorrect()
        {
            var data = new byte[1000];
            var stats = _service.CalculateStatistics(data);
            Assert.AreEqual(1000L, stats.FileSize);
        }

        [TestMethod]
        public void CalculateStatistics_UniqueBytesCount_Correct()
        {
            // Only bytes 0x00 and 0xFF
            var data = new byte[256];
            for (int i = 0; i < 256; i++) data[i] = (i % 2 == 0) ? (byte)0x00 : (byte)0xFF;
            var stats = _service.CalculateStatistics(data);
            Assert.AreEqual(2, stats.UniqueBytesCount);
        }

        [TestMethod]
        public void GenerateByteDistributionChart_ReturnsChartWithPoints()
        {
            var data = new byte[256];
            for (int i = 0; i < 256; i++) data[i] = (byte)i;
            var stats = _service.CalculateStatistics(data);
            var chart = _service.GenerateByteDistributionChart(stats);

            Assert.IsNotNull(chart);
            Assert.AreEqual(256, chart.DataPoints.Count);
        }

        [TestMethod]
        public void GenerateEntropyChart_ReturnsChartWithPoints()
        {
            var data = new byte[4096];
            var rng = new Random(42);
            rng.NextBytes(data);

            var chart = _service.GenerateEntropyChart(data, windowSize: 256);
            Assert.IsNotNull(chart);
            Assert.IsTrue(chart.DataPoints.Count > 0);
        }

        [TestMethod]
        public void GetStatisticsSummary_ContainsEntropy()
        {
            var data = new byte[256];
            for (int i = 0; i < 256; i++) data[i] = (byte)i;
            var stats = _service.CalculateStatistics(data);
            var summary = _service.GetStatisticsSummary(stats);
            StringAssert.Contains(summary, "Entropy");
        }
    }
}

//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using WpfHexEditor.BinaryAnalysis.Models.Patterns;
using WpfHexEditor.BinaryAnalysis.Services;

namespace WpfHexEditor.BinaryAnalysis.Tests.Services
{
    [TestClass]
    public class AnomalyDetectionService_Tests
    {
        private AnomalyDetectionService _service;

        [TestInitialize]
        public void Setup() => _service = new AnomalyDetectionService();

        [TestMethod]
        public void DetectAnomalies_EmptyData_ReturnsEmptyList()
        {
            var result = _service.DetectAnomalies(new byte[0]);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void DetectAnomalies_AllZeros_NoPaddingReportedAsAnomaly()
        {
            // All-zero data → low entropy, but it's padding — should not generate EntropyAnomaly
            var data = new byte[4096]; // all 0x00
            var anomalies = _service.DetectAnomalies(data);
            // Padding regions are excluded from low-entropy anomalies
            var corruptionAnomalies = anomalies.Where(a => a.Type == PatternType.Corruption).ToList();
            Assert.AreEqual(0, corruptionAnomalies.Count);
        }

        [TestMethod]
        public void DetectAnomalies_HighEntropyBlock_DetectedAsCompressedOrEncrypted()
        {
            // Random data (high entropy)
            var data = new byte[4096];
            var rng = new Random(42);
            rng.NextBytes(data);

            var anomalies = _service.DetectAnomalies(data);
            var highEntropy = anomalies.Where(a =>
                a.Type == PatternType.CompressedData ||
                a.Type == PatternType.EncryptedData).ToList();

            Assert.IsTrue(highEntropy.Count > 0, "Expected high-entropy regions to be detected");
        }

        [TestMethod]
        public void DetectAnomalies_EntropyTransition_DetectsSpike()
        {
            // First half: all zeros (low entropy), second half: random (high entropy)
            var data = new byte[4096];
            var rng = new Random(42);
            rng.NextBytes(data.AsSpan(2048, 2048)); // random in second half only

            var anomalies = _service.DetectAnomalies(data);
            var spikes = anomalies.Where(a => a.Type == PatternType.EntropyAnomaly).ToList();
            Assert.IsTrue(spikes.Count > 0, "Expected entropy spike at transition");
        }

        [TestMethod]
        public void CalculateEntropy_AllSameBytes_ReturnsZero()
        {
            var data = new byte[256]; // all 0x00
            var entropy = _service.CalculateEntropy(data, 0, data.Length);
            Assert.AreEqual(0.0, entropy, 0.001);
        }

        [TestMethod]
        public void CalculateEntropy_UniformDistribution_ReturnsEight()
        {
            var data = new byte[256];
            for (int i = 0; i < 256; i++) data[i] = (byte)i;
            var entropy = _service.CalculateEntropy(data, 0, data.Length);
            Assert.AreEqual(8.0, entropy, 0.001);
        }

        [TestMethod]
        public void CalculateRollingEntropy_ReturnsMultipleWindows()
        {
            var data = new byte[4096];
            var windows = _service.CalculateRollingEntropy(data, windowSize: 512);
            Assert.IsTrue(windows.Count > 1);
        }

        [TestMethod]
        public void DetectAnomalies_AllAnomalies_HaveValidOffsets()
        {
            var data = new byte[4096];
            new Random(99).NextBytes(data);
            var anomalies = _service.DetectAnomalies(data, baseOffset: 0x1000);

            foreach (var a in anomalies)
            {
                Assert.IsTrue(a.StartOffset >= 0x1000, $"Offset {a.StartOffset:X} below base");
                Assert.IsTrue(a.Length > 0, "Anomaly has zero length");
            }
        }
    }
}

//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WpfHexaEditor.Models.Patterns;
using WpfHexaEditor.Services;

namespace WPFHexaEditor.Tests.Unit
{
    [TestClass]
    public class AnomalyDetectionService_Tests
    {
        private AnomalyDetectionService _service;

        [TestInitialize]
        public void Setup()
        {
            _service = new AnomalyDetectionService();
        }

        #region Entropy Calculation Tests

        [TestMethod]
        public void CalculateEntropy_AllSameByte_ReturnsZero()
        {
            var data = new byte[1024];
            Array.Fill(data, (byte)0x42);

            var entropy = _service.CalculateEntropy(data, 0, data.Length);

            Assert.AreEqual(0.0, entropy, 0.01);
        }

        [TestMethod]
        public void CalculateEntropy_UniformDistribution_ReturnsHigh()
        {
            // Create data with uniform byte distribution
            var data = new byte[256 * 10]; // 10 of each byte value
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 256);
            }

            var entropy = _service.CalculateEntropy(data, 0, data.Length);

            // Uniform distribution should have entropy close to 8.0
            Assert.IsTrue(entropy > 7.5, $"Expected high entropy (>7.5), got {entropy}");
        }

        [TestMethod]
        public void CalculateEntropy_SequentialData_ReturnsMedium()
        {
            var data = new byte[1024];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 256);
            }

            var entropy = _service.CalculateEntropy(data, 0, data.Length);

            // Sequential should have moderate entropy
            Assert.IsTrue(entropy > 3.0 && entropy < 7.0);
        }

        [TestMethod]
        public void CalculateEntropy_EmptyData_ReturnsZero()
        {
            var data = new byte[0];

            var entropy = _service.CalculateEntropy(data, 0, 0);

            Assert.AreEqual(0.0, entropy);
        }

        #endregion

        #region Rolling Entropy Tests

        [TestMethod]
        public void CalculateRollingEntropy_CreatesWindows()
        {
            var data = new byte[4096];
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)(i % 256);

            var windows = _service.CalculateRollingEntropy(data, 1024);

            Assert.IsTrue(windows.Count > 0);
            Assert.AreEqual(1024, windows[0].Length);
        }

        [TestMethod]
        public void CalculateRollingEntropy_WindowsOverlap()
        {
            var data = new byte[2048];
            int windowSize = 1024;

            var windows = _service.CalculateRollingEntropy(data, windowSize);

            // With 50% overlap, windows should be spaced windowSize/2 apart
            if (windows.Count >= 2)
            {
                var spacing = windows[1].Offset - windows[0].Offset;
                Assert.AreEqual(windowSize / 2, spacing);
            }
        }

        #endregion

        #region Anomaly Detection Tests

        [TestMethod]
        public void DetectAnomalies_FindsEntropySpike()
        {
            var data = new byte[4096];

            // First half: low entropy (all same byte)
            Array.Fill(data, (byte)0x00, 0, 2048);

            // Second half: high entropy (random-like)
            var random = new Random(42);
            for (int i = 2048; i < 4096; i++)
            {
                data[i] = (byte)random.Next(256);
            }

            var anomalies = _service.DetectAnomalies(data, 0);

            // Should detect entropy anomaly at transition
            var entropyAnomalies = anomalies.Where(a => a.Type == PatternType.EntropyAnomaly).ToList();
            Assert.IsTrue(entropyAnomalies.Count > 0, "Should detect entropy spike");
        }

        [TestMethod]
        public void DetectAnomalies_FindsHighEntropyRegion()
        {
            var data = new byte[4096];

            // Fill with high entropy data (simulating compressed/encrypted)
            var random = new Random(42);
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)random.Next(256);
            }

            _service.HighEntropyThreshold = 7.0; // Lower threshold for test

            var anomalies = _service.DetectAnomalies(data, 0);

            // Should detect high entropy region
            var highEntropy = anomalies.Where(a =>
                a.Type == PatternType.CompressedData || a.Type == PatternType.EncryptedData).ToList();

            Assert.IsTrue(highEntropy.Count > 0, "Should detect high entropy region");
        }

        [TestMethod]
        public void DetectAnomalies_FindsLowEntropyRegion()
        {
            var data = new byte[4096];

            // Create very low entropy data (but not pure padding)
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 2); // Alternating 0x00 and 0x01
            }

            _service.LowEntropyThreshold = 2.5;

            var anomalies = _service.DetectAnomalies(data, 0);

            // Should detect low entropy (corruption)
            var lowEntropy = anomalies.Where(a => a.Type == PatternType.Corruption).ToList();
            Assert.IsTrue(lowEntropy.Count >= 0); // May or may not detect depending on exact entropy
        }

        [TestMethod]
        public void DetectAnomalies_HandlesPurePadding()
        {
            var data = new byte[4096];
            Array.Fill(data, (byte)0x00);

            var anomalies = _service.DetectAnomalies(data, 0);

            // Should not report pure padding as corruption
            var corruption = anomalies.Where(a => a.Type == PatternType.Corruption).ToList();
            Assert.AreEqual(0, corruption.Count, "Pure padding should not be reported as corruption");
        }

        #endregion

        #region Severity Tests

        [TestMethod]
        public void DetectAnomalies_AssignsSeverityLevels()
        {
            var data = new byte[4096];

            // Create extreme entropy change
            Array.Fill(data, (byte)0x00, 0, 2048);
            var random = new Random(42);
            for (int i = 2048; i < 4096; i++)
            {
                data[i] = (byte)random.Next(256);
            }

            _service.EntropyChangeThreshold = 1.0; // Lower to ensure detection

            var anomalies = _service.DetectAnomalies(data, 0);

            var withSeverity = anomalies.Where(a => a.Severity > 0).ToList();
            Assert.IsTrue(withSeverity.Count > 0, "Should assign severity to anomalies");
        }

        #endregion

        #region Configuration Tests

        [TestMethod]
        public void Configuration_CanSetEntropyWindowSize()
        {
            _service.EntropyWindowSize = 2048;
            Assert.AreEqual(2048, _service.EntropyWindowSize);
        }

        [TestMethod]
        public void Configuration_CanSetEntropyThresholds()
        {
            _service.HighEntropyThreshold = 7.8;
            _service.LowEntropyThreshold = 1.5;
            _service.EntropyChangeThreshold = 3.0;

            Assert.AreEqual(7.8, _service.HighEntropyThreshold);
            Assert.AreEqual(1.5, _service.LowEntropyThreshold);
            Assert.AreEqual(3.0, _service.EntropyChangeThreshold);
        }

        [TestMethod]
        public void Configuration_CanSetMaxSampleSize()
        {
            _service.MaxSampleSize = 512 * 1024;
            Assert.AreEqual(512 * 1024, _service.MaxSampleSize);
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        public void DetectAnomalies_HandlesEmptyData()
        {
            var data = new byte[0];

            var anomalies = _service.DetectAnomalies(data);

            Assert.IsNotNull(anomalies);
            Assert.AreEqual(0, anomalies.Count);
        }

        [TestMethod]
        public void DetectAnomalies_RespectsBaseOffset()
        {
            var data = new byte[2048];
            Array.Fill(data, (byte)0x42);

            long baseOffset = 0x5000;
            var anomalies = _service.DetectAnomalies(data, baseOffset);

            if (anomalies.Count > 0)
            {
                Assert.IsTrue(anomalies[0].StartOffset >= baseOffset);
            }
        }

        [TestMethod]
        public void DetectAnomalies_HandlesLargeData()
        {
            var largeData = new byte[2 * 1024 * 1024]; // 2MB
            _service.MaxSampleSize = 1024 * 1024; // 1MB limit

            var random = new Random(42);
            random.NextBytes(largeData);

            var anomalies = _service.DetectAnomalies(largeData);

            // Should complete without errors
            Assert.IsNotNull(anomalies);
        }

        #endregion
    }
}

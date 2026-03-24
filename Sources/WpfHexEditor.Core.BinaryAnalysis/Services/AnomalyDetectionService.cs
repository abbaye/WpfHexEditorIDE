//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using WpfHexEditor.Core.BinaryAnalysis.Models.Patterns;

namespace WpfHexEditor.Core.BinaryAnalysis.Services
{
    /// <summary>
    /// Service for detecting anomalies and corruption in binary data
    /// Uses entropy analysis and pattern detection
    /// </summary>
    public class AnomalyDetectionService
    {
        /// <summary>
        /// Window size for rolling entropy calculation (default: 1KB)
        /// </summary>
        public int EntropyWindowSize { get; set; } = 1024;

        /// <summary>
        /// Entropy threshold for detecting encryption/compression (default: 7.5)
        /// </summary>
        public double HighEntropyThreshold { get; set; } = 7.5;

        /// <summary>
        /// Entropy threshold for detecting suspicious low entropy (default: 2.0)
        /// </summary>
        public double LowEntropyThreshold { get; set; } = 2.0;

        /// <summary>
        /// Minimum entropy change to detect anomaly (default: 2.0)
        /// </summary>
        public double EntropyChangeThreshold { get; set; } = 2.0;

        /// <summary>
        /// Maximum sample size for analysis (default: 1MB)
        /// </summary>
        public int MaxSampleSize { get; set; } = 1024 * 1024;

        /// <summary>
        /// Detect anomalies in binary data
        /// </summary>
        /// <param name="data">Binary data to analyze</param>
        /// <param name="baseOffset">Base offset in file</param>
        /// <returns>List of detected anomalies</returns>
        public List<DetectedPattern> DetectAnomalies(byte[] data, long baseOffset = 0)
        {
            var anomalies = new List<DetectedPattern>();

            // Limit data size
            var analyzedData = data;
            if (data.Length > MaxSampleSize)
            {
                analyzedData = new byte[MaxSampleSize];
                Array.Copy(data, analyzedData, MaxSampleSize);
            }

            // 1. Detect entropy anomalies (sudden changes)
            anomalies.AddRange(DetectEntropyAnomalies(analyzedData, baseOffset));

            // 2. Detect high entropy regions (compressed/encrypted)
            anomalies.AddRange(DetectHighEntropyRegions(analyzedData, baseOffset));

            // 3. Detect suspicious low entropy regions
            anomalies.AddRange(DetectLowEntropyRegions(analyzedData, baseOffset));

            return anomalies;
        }

        #region Entropy Calculation

        /// <summary>
        /// Calculate Shannon entropy for a byte array
        /// Returns value between 0 (all same byte) and 8 (uniform distribution)
        /// Algorithm from BarChartPanel.CalculateEntropy()
        /// </summary>
        public double CalculateEntropy(byte[] data, int offset, int length)
        {
            if (length == 0)
                return 0;

            // Count byte frequencies
            var frequencies = new int[256];
            for (int i = offset; i < offset + length && i < data.Length; i++)
            {
                frequencies[data[i]]++;
            }

            // Calculate Shannon entropy
            double entropy = 0;
            for (int i = 0; i < 256; i++)
            {
                if (frequencies[i] > 0)
                {
                    double probability = frequencies[i] / (double)length;
                    entropy -= probability * Math.Log(probability, 2);
                }
            }

            return entropy;
        }

        /// <summary>
        /// Calculate entropy for rolling windows
        /// </summary>
        public List<EntropyWindow> CalculateRollingEntropy(byte[] data, int windowSize)
        {
            var windows = new List<EntropyWindow>();

            for (int offset = 0; offset < data.Length; offset += windowSize / 2) // 50% overlap
            {
                var length = Math.Min(windowSize, data.Length - offset);
                if (length < windowSize / 2) // Skip partial windows at end
                    break;

                var entropy = CalculateEntropy(data, offset, length);

                windows.Add(new EntropyWindow
                {
                    Offset = offset,
                    Length = length,
                    Entropy = entropy
                });
            }

            return windows;
        }

        #endregion

        #region Anomaly Detection

        /// <summary>
        /// Detect sudden entropy changes (potential corruption or transition points)
        /// </summary>
        private List<DetectedPattern> DetectEntropyAnomalies(byte[] data, long baseOffset)
        {
            var anomalies = new List<DetectedPattern>();
            var windows = CalculateRollingEntropy(data, EntropyWindowSize);

            for (int i = 1; i < windows.Count; i++)
            {
                var prevWindow = windows[i - 1];
                var currentWindow = windows[i];

                var entropyChange = Math.Abs(currentWindow.Entropy - prevWindow.Entropy);

                if (entropyChange > EntropyChangeThreshold)
                {
                    var severity = CalculateSeverity(entropyChange, 2.0, 4.0);

                    var pattern = new DetectedPattern(
                        PatternType.EntropyAnomaly,
                        baseOffset + currentWindow.Offset,
                        currentWindow.Length)
                    {
                        Description = $"Entropy spike: {prevWindow.Entropy:F2} → {currentWindow.Entropy:F2}",
                        Entropy = currentWindow.Entropy,
                        Severity = severity,
                        Confidence = 0.8,
                        VisualizationColor = "#FF5722" // Red for anomalies
                    };

                    pattern.Metadata["EntropyChange"] = entropyChange.ToString("F2");
                    pattern.Metadata["PreviousEntropy"] = prevWindow.Entropy.ToString("F2");
                    pattern.Metadata["CurrentEntropy"] = currentWindow.Entropy.ToString("F2");

                    anomalies.Add(pattern);
                }
            }

            return anomalies;
        }

        /// <summary>
        /// Detect high entropy regions (likely compressed or encrypted)
        /// </summary>
        private List<DetectedPattern> DetectHighEntropyRegions(byte[] data, long baseOffset)
        {
            var regions = new List<DetectedPattern>();
            var windows = CalculateRollingEntropy(data, EntropyWindowSize);

            // Find consecutive high-entropy windows
            int startWindow = -1;

            for (int i = 0; i < windows.Count; i++)
            {
                if (windows[i].Entropy > HighEntropyThreshold)
                {
                    if (startWindow == -1)
                        startWindow = i;
                }
                else
                {
                    if (startWindow != -1)
                    {
                        // End of high-entropy region
                        var firstWindow = windows[startWindow];
                        var lastWindow = windows[i - 1];

                        var offset = firstWindow.Offset;
                        var length = (lastWindow.Offset + lastWindow.Length) - firstWindow.Offset;

                        // Average entropy of region
                        var avgEntropy = windows.Skip(startWindow).Take(i - startWindow)
                            .Average(w => w.Entropy);

                        var patternType = avgEntropy > 7.9
                            ? PatternType.EncryptedData
                            : PatternType.CompressedData;

                        var pattern = new DetectedPattern(patternType, baseOffset + offset, length)
                        {
                            Description = patternType == PatternType.EncryptedData
                                ? $"Likely encrypted data (entropy: {avgEntropy:F2})"
                                : $"Likely compressed data (entropy: {avgEntropy:F2})",
                            Entropy = avgEntropy,
                            Confidence = 0.75,
                            VisualizationColor = patternType == PatternType.EncryptedData ? "#9C27B0" : "#FF9800"
                        };

                        pattern.Metadata["AverageEntropy"] = avgEntropy.ToString("F2");
                        regions.Add(pattern);

                        startWindow = -1;
                    }
                }
            }

            // Handle region extending to end of data
            if (startWindow != -1)
            {
                var firstWindow = windows[startWindow];
                var lastWindow = windows[windows.Count - 1];

                var offset = firstWindow.Offset;
                var length = (lastWindow.Offset + lastWindow.Length) - firstWindow.Offset;

                var avgEntropy = windows.Skip(startWindow).Average(w => w.Entropy);

                var patternType = avgEntropy > 7.9 ? PatternType.EncryptedData : PatternType.CompressedData;

                var pattern = new DetectedPattern(patternType, baseOffset + offset, length)
                {
                    Description = $"High entropy region (entropy: {avgEntropy:F2})",
                    Entropy = avgEntropy,
                    Confidence = 0.75,
                    VisualizationColor = "#9C27B0"
                };

                regions.Add(pattern);
            }

            return regions;
        }

        /// <summary>
        /// Detect suspiciously low entropy regions (potential corruption)
        /// </summary>
        private List<DetectedPattern> DetectLowEntropyRegions(byte[] data, long baseOffset)
        {
            var regions = new List<DetectedPattern>();
            var windows = CalculateRollingEntropy(data, EntropyWindowSize);

            foreach (var window in windows)
            {
                if (window.Entropy < LowEntropyThreshold)
                {
                    // Check if it's just padding (already detected)
                    bool isPadding = IsPaddingRegion(data, window.Offset, window.Length);
                    if (isPadding)
                        continue;

                    var severity = CalculateSeverity(LowEntropyThreshold - window.Entropy, 0.5, 1.5);

                    var pattern = new DetectedPattern(
                        PatternType.Corruption,
                        baseOffset + window.Offset,
                        window.Length)
                    {
                        Description = $"Suspicious low entropy (entropy: {window.Entropy:F2})",
                        Entropy = window.Entropy,
                        Severity = severity,
                        Confidence = 0.6,
                        VisualizationColor = "#FFC107" // Yellow/amber for warnings
                    };

                    regions.Add(pattern);
                }
            }

            return regions;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Check if a region is mostly padding (null or 0xFF bytes)
        /// </summary>
        private bool IsPaddingRegion(byte[] data, int offset, int length)
        {
            var nullCount = 0;
            var ffCount = 0;

            for (int i = offset; i < offset + length && i < data.Length; i++)
            {
                if (data[i] == 0x00)
                    nullCount++;
                else if (data[i] == 0xFF)
                    ffCount++;
            }

            // If more than 90% is null or 0xFF, consider it padding
            var threshold = length * 0.9;
            return nullCount > threshold || ffCount > threshold;
        }

        /// <summary>
        /// Calculate severity level based on metric value
        /// </summary>
        private int CalculateSeverity(double value, double lowThreshold, double highThreshold)
        {
            if (value < lowThreshold)
                return 1; // Low
            else if (value < highThreshold)
                return 2; // Medium
            else if (value < highThreshold * 1.5)
                return 3; // High
            else
                return 4; // Critical
        }

        #endregion
    }

    /// <summary>
    /// Represents an entropy window in rolling analysis
    /// </summary>
    public class EntropyWindow
    {
        public int Offset { get; set; }
        public int Length { get; set; }
        public double Entropy { get; set; }

        public override string ToString()
        {
            return $"Offset: 0x{Offset:X}, Length: {Length}, Entropy: {Entropy:F2}";
        }
    }
}

//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Bytes;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.Events;

namespace WpfHexEditor.Core.Services
{
    /// <summary>
    /// Service for detecting file formats and generating custom background blocks
    /// Loads format definitions from JSON files and executes them via FormatScriptInterpreter
    /// </summary>
    public class FormatDetectionService
    {
        private readonly List<FormatDefinition> _loadedFormats = new List<FormatDefinition>();
        private readonly ContentAnalyzer _contentAnalyzer = new ContentAnalyzer();

        // ── Static shared catalog (set once at app startup) ─────────────
        private static IReadOnlyList<FormatDefinition>? s_sharedFormats;
        private static readonly object s_sharedLock = new();

        /// <summary>
        /// Set the shared format catalog used by ALL FormatDetectionService instances.
        /// Called once at app startup by FormatCatalogService. Thread-safe.
        /// </summary>
        public static void SetSharedCatalog(IReadOnlyList<FormatDefinition> formats)
        {
            lock (s_sharedLock)
                s_sharedFormats = formats;
        }

        /// <summary>Static shared catalog (read-only).</summary>
        public static IReadOnlyList<FormatDefinition>? SharedCatalog
        {
            get { lock (s_sharedLock) return s_sharedFormats; }
        }

        /// <summary>
        /// All available formats: instance-level overrides + shared catalog.
        /// Instance formats take priority (searched first).
        /// </summary>
        private List<FormatDefinition> EffectiveFormats
        {
            get
            {
                var shared = s_sharedFormats;
                if (_loadedFormats.Count > 0 && shared?.Count > 0)
                {
                    var combined = new List<FormatDefinition>(_loadedFormats.Count + shared.Count);
                    combined.AddRange(_loadedFormats);
                    combined.AddRange(shared);
                    return combined;
                }
                if (_loadedFormats.Count > 0) return _loadedFormats;
                return shared != null ? new List<FormatDefinition>(shared) : _loadedFormats;
            }
        }

        #region Format Loading

        /// <summary>
        /// Load a format definition from JSON file
        /// </summary>
        /// <param name="jsonPath">Path to JSON file</param>
        /// <returns>True if loaded successfully</returns>
        public bool LoadFormatDefinition(string jsonPath)
        {
            if (string.IsNullOrWhiteSpace(jsonPath) || !File.Exists(jsonPath))
                return false;

            try
            {
                var json = File.ReadAllText(jsonPath);
                var format = ImportFromJson(json);

                if (format != null && format.IsValid())
                {
                    // Auto-detect category from file path
                    // Example: "FormatDefinitions/Archives/ZIP.json" -> Category = "Archives"
                    if (string.IsNullOrWhiteSpace(format.Category))
                    {
                        format.Category = ExtractCategoryFromPath(jsonPath);
                    }

                    return AddFormatDefinition(format);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading format definition from {jsonPath}: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Add a format definition directly (used for embedded resources)
        /// </summary>
        /// <param name="format">Format definition to add</param>
        /// <returns>True if added successfully</returns>
        public bool AddFormatDefinition(FormatDefinition format)
        {
            if (format == null || !format.IsValid())
                return false;

            try
            {
                // Check if already loaded
                var existing = _loadedFormats.FirstOrDefault(f =>
                    f.FormatName == format.FormatName && f.Version == format.Version);

                if (existing != null)
                {
                    // Replace existing (allows user formats to override built-in)
                    _loadedFormats.Remove(existing);
                }

                _loadedFormats.Add(format);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding format definition: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Load all format definitions from a directory
        /// </summary>
        /// <param name="directory">Directory containing JSON files</param>
        /// <returns>Number of formats loaded</returns>
        public int LoadFormatDefinitionsFromDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return 0;

            int count = 0;

            try
            {
                // Load all .whfmt files recursively (legacy .json also accepted)
                var jsonFiles = Directory.GetFiles(directory, "*.whfmt", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories))
                    .ToArray();

                foreach (var file in jsonFiles)
                {
                    if (LoadFormatDefinition(file))
                    {
                        count++;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading formats from directory {directory}: {ex.Message}");
            }

            return count;
        }

        /// <summary>
        /// Clear all loaded format definitions
        /// </summary>
        public void ClearFormats()
        {
            _loadedFormats.Clear();
        }

        /// <summary>
        /// Extract category from file path
        /// Examples:
        /// - "C:/FormatDefinitions/Archives/ZIP.whfmt" -> "Archives"
        /// - "FormatDefinitions/Images/PNG.whfmt" -> "Images"
        /// - "WpfHexEditor.Core.FormatDefinitions.Archives.ZIP.whfmt" (embedded) -> "Archives"
        /// </summary>
        private string ExtractCategoryFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "Other";

            try
            {
                // Normalize path separators
                path = path.Replace('\\', '/');

                // Check if it's an embedded resource name (contains dots instead of slashes)
                if (path.Contains("FormatDefinitions.") && path.Count(c => c == '.') >= 3)
                {
                    // Embedded resource format: "WpfHexEditor.Core.FormatDefinitions.Archives.ZIP.whfmt"
                    var parts = path.Split('.');
                    var formatDefsIndex = Array.IndexOf(parts, "FormatDefinitions");
                    if (formatDefsIndex >= 0 && formatDefsIndex < parts.Length - 2)
                    {
                        return parts[formatDefsIndex + 1]; // Category is next part after "FormatDefinitions"
                    }
                }
                else
                {
                    // File path format: "C:/FormatDefinitions/Archives/ZIP.whfmt"
                    var parts = path.Split('/');
                    var formatDefsIndex = Array.FindIndex(parts, p => p.Equals("FormatDefinitions", StringComparison.OrdinalIgnoreCase));
                    if (formatDefsIndex >= 0 && formatDefsIndex < parts.Length - 2)
                    {
                        return parts[formatDefsIndex + 1]; // Category is next part after "FormatDefinitions"
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting category from path {path}: {ex.Message}");
            }

            return "Other"; // Default category
        }

        #endregion

        #region Format Detection

        /// <summary>
        /// Detect format from file data using multi-tier detection with confidence scoring
        /// </summary>
        /// <param name="data">File data (at least first 1KB recommended)</param>
        /// <param name="fileName">Optional filename for extension-based hints</param>
        /// <param name="byteProvider">Optional ByteProvider for reading beyond data buffer</param>
        /// <returns>Detection result with confidence scores and multiple candidates</returns>
        public FormatDetectionResult DetectFormat(byte[] data, string fileName = null, ByteProvider byteProvider = null)
        {
            if (data == null || data.Length == 0)
            {
                return new FormatDetectionResult
                {
                    Success = false,
                    ErrorMessage = "No data provided"
                };
            }

            var sw = Stopwatch.StartNew();
            var candidates = new List<FormatMatchCandidate>();

            // Step 1: Analyze content (text vs binary)
            var contentAnalysis = _contentAnalyzer.Analyze(data);

            // Step 2: Multi-tier detection — abort after 3 s to prevent UI hangs on complex formats.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try
            {
                // TIER 1: Strong signatures (required: true, unique/strong)
                var tier1 = DetectWithStrongSignatures(data, fileName, contentAnalysis, byteProvider, cts.Token);
                candidates.AddRange(tier1);

                // Early exit if high-confidence match found
                if (tier1.Any(c => c.ConfidenceScore >= 0.9))
                {
                    return CreateResult(tier1.OrderByDescending(c => c.ConfidenceScore).First(),
                                      candidates, contentAnalysis, sw);
                }

                // TIER 2: Text format detection (if appears to be text)
                if (contentAnalysis.IsLikelyText)
                {
                    var tier2 = DetectTextFormats(data, fileName, contentAnalysis, byteProvider, cts.Token);
                    candidates.AddRange(tier2);
                }

                // TIER 3: Weak signatures (only if no strong match)
                if (!candidates.Any())
                {
                    var tier3 = DetectWithWeakSignatures(data, fileName, contentAnalysis, byteProvider, cts.Token);
                    candidates.AddRange(tier3);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[FormatDetection] Detection timed out after 3 s — returning best candidate so far.");
            }

            // Step 3: Score and rank candidates
            ScoreAndRankCandidates(candidates, fileName, contentAnalysis, data);

            // Step 4: Decision logic
            return DecideFormat(candidates, contentAnalysis, sw);
        }

        /// <summary>
        /// Try to detect a specific format.
        /// v2.0: also checks entropy hints, multi-signature rules, checksums, and assertions.
        /// </summary>
        private bool TryDetectFormat(byte[] data, FormatDefinition format,
            out List<CustomBackgroundBlock> blocks,
            out Dictionary<string, object> variables,
            ByteProvider byteProvider = null,
            List<AssertionResult> assertionResultsOut = null)
        {
            blocks    = new List<CustomBackgroundBlock>();
            variables = new Dictionary<string, object>();

            if (format == null || !format.IsValid())
                return false;

            // Check signature (legacy single + v2.0 multi)
            if (format.Detection != null && format.Detection.Required)
            {
                if (!CheckSignature(data, format.Detection))
                    return false;
            }

            // v2.0: entropy hint check (fast 512-byte sample)
            if (format.Detection?.EntropyHint != null)
            {
                double entropy = ComputeEntropy(data);
                var hint = format.Detection.EntropyHint;
                if (entropy < hint.Min) return false;
                if (entropy > hint.Max) return false;
            }

            // Generate blocks using interpreter
            try
            {
                var interpreter = new FormatScriptInterpreter(data, format.Variables, byteProvider);

                // Execute built-in functions first (populates variables)
                if (format.Functions != null && format.Functions.Count > 0)
                    interpreter.ExecuteFunctions(format.Functions);

                blocks = interpreter.ExecuteBlocks(format.Blocks);

                // Copy variables from interpreter (includes function results)
                variables = new Dictionary<string, object>(interpreter.Variables);

                // v2.0: run checksums — any error-severity failure reduces confidence (logged only here)
                if (format.Checksums != null && format.Checksums.Count > 0)
                {
                    var checksumEngine = new ChecksumEngine();
                    var csResults = checksumEngine.Execute(format.Checksums, data, variables);
                    foreach (var r in csResults)
                    {
                        if (!r.IsValid)
                            Debug.WriteLine($"[FormatDetection] Checksum '{r.Name}' failed: {r.ErrorMessage}");
                    }
                }

                // v2.0: run assertions — log failures (do not block detection)
                if (format.Assertions != null && format.Assertions.Count > 0)
                {
                    var runner = new AssertionRunner();
                    var assertResults = runner.Run(format.Assertions, variables);
                    foreach (var r in assertResults)
                    {
                        if (!r.Passed)
                            Debug.WriteLine($"[FormatDetection] Assertion '{r.Name}' failed: {r.Message}");
                    }
                    // D3 — pass assertion results out for ForensicAlerts panel
                    assertionResultsOut?.AddRange(assertResults);
                }

                return blocks.Count > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FormatDetection] Error executing format {format.FormatName}: {ex.Message}");
                return false;
            }
        }

        #region Multi-Tier Detection Methods

        /// <summary>
        /// TIER 1: Detect formats with strong signatures (required: true, unique/strong)
        /// </summary>
        private List<FormatMatchCandidate> DetectWithStrongSignatures(byte[] data, string fileName, ContentAnalysisResult contentAnalysis, ByteProvider byteProvider, CancellationToken ct = default)
        {
            var candidates = new List<FormatMatchCandidate>();

            // Only check formats with required: true AND medium+ signature strength
            var strongFormats = EffectiveFormats
                .Where(f => f.Detection?.Required == true)
                .Where(f => GetSignatureStrength(f.Detection) >= SignatureStrength.Medium)
                .OrderByDescending(f => GetSignatureStrength(f.Detection));

            // Prioritize by extension match
            var formatsByPriority = PrioritizeByExtension(strongFormats, fileName);

            foreach (var format in formatsByPriority)
            {
                ct.ThrowIfCancellationRequested();
                var assertOut1 = new List<AssertionResult>();
                if (TryDetectFormat(data, format, out var blocks, out var variables, byteProvider, assertOut1))
                {
                    var candidate = new FormatMatchCandidate
                    {
                        Format = format,
                        Blocks = blocks,
                        Variables = variables,
                        Tier = MatchTier.StrongSignature,
                        SignatureConfidence = CalculateSignatureConfidence(format.Detection),
                        AssertionResults = assertOut1
                    };

                    candidates.Add(candidate);

                    // For unique signatures, one match is usually enough
                    if (GetSignatureStrength(format.Detection) == SignatureStrength.Unique)
                        break;
                }
            }

            return candidates;
        }

        /// <summary>
        /// TIER 2: Detect text-based formats using content heuristics
        /// </summary>
        private List<FormatMatchCandidate> DetectTextFormats(byte[] data, string fileName, ContentAnalysisResult contentAnalysis, ByteProvider byteProvider, CancellationToken ct = default)
        {
            var candidates = new List<FormatMatchCandidate>();

            // Only consider formats marked as text-based
            var textFormats = EffectiveFormats.Where(f => f.Detection?.IsTextFormat == true).ToList();

            foreach (var format in textFormats)
            {
                ct.ThrowIfCancellationRequested();
                double contentScore = 0.0;

                // Check if content matches expected pattern
                var formatHint = format.FormatName.Split(' ')[0];
                if (contentAnalysis.TextFormatHints != null &&
                    contentAnalysis.TextFormatHints.Contains(formatHint))
                {
                    contentScore += 0.5;
                }

                // Check extension match
                if (MatchesExtension(format, fileName))
                {
                    contentScore += 0.3;
                }

                // Try block generation
                var assertOut2 = new List<AssertionResult>();
                if (TryDetectFormat(data, format, out var blocks, out var variables, byteProvider, assertOut2))
                {
                    contentScore += 0.2;

                    var candidate = new FormatMatchCandidate
                    {
                        Format = format,
                        Blocks = blocks,
                        Variables = variables,
                        Tier = MatchTier.ContentBased,
                        ContentConfidence = contentScore,
                        AssertionResults = assertOut2
                    };

                    candidates.Add(candidate);
                }
            }

            return candidates;
        }

        /// <summary>
        /// TIER 3: Detect formats with weak/no signatures (last resort)
        /// </summary>
        private List<FormatMatchCandidate> DetectWithWeakSignatures(byte[] data, string fileName, ContentAnalysisResult contentAnalysis, ByteProvider byteProvider, CancellationToken ct = default)
        {
            var candidates = new List<FormatMatchCandidate>();

            // Only check formats with weak/no signatures
            var weakFormats = EffectiveFormats
                .Where(f => f.Detection?.Required == false ||
                           GetSignatureStrength(f.Detection) <= SignatureStrength.Weak);

            // Heavily weight extension matching for weak signatures
            var extensionMatches = weakFormats.Where(f => MatchesExtension(f, fileName));

            foreach (var format in extensionMatches)
            {
                ct.ThrowIfCancellationRequested();
                var assertOut3 = new List<AssertionResult>();
                if (TryDetectFormat(data, format, out var blocks, out var variables, byteProvider, assertOut3))
                {
                    var candidate = new FormatMatchCandidate
                    {
                        Format = format,
                        Blocks = blocks,
                        Variables = variables,
                        Tier = MatchTier.FallbackWeak,
                        ExtensionConfidence = 0.6,  // Lower confidence for weak matches
                        AssertionResults = assertOut3
                    };

                    candidates.Add(candidate);
                }
            }

            return candidates;
        }

        /// <summary>
        /// Score and rank all candidates by confidence
        /// </summary>
        private void ScoreAndRankCandidates(List<FormatMatchCandidate> candidates, string fileName, ContentAnalysisResult contentAnalysis, byte[] data = null)
        {
            // Detect shared signatures: multiple candidates matching the same magic bytes
            bool hasSharedSignatures = candidates.Count > 1 &&
                candidates.Select(c => c.Format?.Detection?.Signature).Distinct().Count() <
                candidates.Count(c => c.Format?.Detection?.Signature != null);

            // For ZIP-based formats, analyze the first entry name to disambiguate
            string zipFirstEntryName = null;
            if (hasSharedSignatures && data != null)
            {
                zipFirstEntryName = ExtractZipFirstEntryName(data);
            }

            foreach (var candidate in candidates)
            {
                double score = 0.0;
                var factors = new List<string>();

                // Signature confidence (30% weight)
                if (candidate.SignatureConfidence > 0)
                {
                    score += candidate.SignatureConfidence * 0.30;
                    factors.Add($"Signature match ({candidate.SignatureConfidence:P0})");
                }

                // Extension match (30% weight, boosted to 40% for shared signatures)
                bool extensionMatches = MatchesExtension(candidate.Format, fileName);
                if (extensionMatches)
                {
                    candidate.ExtensionConfidence = 1.0;
                    double extensionWeight = hasSharedSignatures ? 0.40 : 0.30;
                    score += extensionWeight;
                    factors.Add(hasSharedSignatures ? "Extension match (shared sig boost)" : "Extension match");
                }

                // ZIP content analysis (25% weight) - disambiguate ZIP-based formats
                bool hasZipContentMatch = false;
                if (zipFirstEntryName != null && candidate.Format?.Detection?.Signature == "504B0304")
                {
                    double zipContentScore = ScoreZipContentMatch(candidate.Format, zipFirstEntryName);
                    if (zipContentScore > 0)
                    {
                        score += zipContentScore * 0.25;
                        hasZipContentMatch = true;
                        factors.Add($"ZIP content match ({zipFirstEntryName})");
                    }
                }

                // Content analysis (20% weight) - skip if already scored via ZIP content
                if (!hasZipContentMatch && candidate.ContentConfidence > 0)
                {
                    score += candidate.ContentConfidence * 0.2;
                    factors.Add($"Content pattern ({candidate.ContentConfidence:P0})");
                }

                // Structure validation (15% weight)
                if (candidate.Blocks != null && candidate.Blocks.Count > 0)
                {
                    candidate.StructureConfidence = Math.Min(1.0, candidate.Blocks.Count / 10.0);
                    score += candidate.StructureConfidence * 0.15;
                    factors.Add($"{candidate.Blocks.Count} blocks parsed");
                }

                // Tier penalty
                switch (candidate.Tier)
                {
                    case MatchTier.StrongSignature:
                        score *= 1.0;  // No penalty
                        break;
                    case MatchTier.ContentBased:
                        score *= 0.9;
                        factors.Add("Text format");
                        break;
                    case MatchTier.FallbackWeak:
                        score *= 0.6;  // Heavy penalty
                        factors.Add("Weak signature");
                        break;
                }

                // Extension mismatch penalty (applied LAST as final multiplier):
                // When multiple formats share the same signature (e.g., 504B0304 for ZIP/DOCX/XLSX/Keynote),
                // a format that doesn't match the file extension is very unlikely to be correct.
                // This is the most decisive disambiguation factor for shared-signature formats.
                if (hasSharedSignatures && !extensionMatches && !string.IsNullOrWhiteSpace(fileName))
                {
                    candidate.ExtensionConfidence = 0.0;
                    score *= 0.25;  // Reduce total score to 25%
                    factors.Add("Extension mismatch penalty (shared signature)");
                }

                candidate.ConfidenceScore = Math.Min(1.0, score);
                candidate.ConfidenceFactors = factors;
            }

            // Sort by confidence descending
            candidates.Sort((a, b) => b.ConfidenceScore.CompareTo(a.ConfidenceScore));
        }

        /// <summary>
        /// Decide final format based on candidates and confidence
        /// </summary>
        private FormatDetectionResult DecideFormat(List<FormatMatchCandidate> candidates, ContentAnalysisResult contentAnalysis, Stopwatch sw)
        {
            sw.Stop();

            if (candidates.Count == 0)
            {
                return new FormatDetectionResult
                {
                    Success = false,
                    ErrorMessage = "No matching format found",
                    ContentAnalysis = contentAnalysis,
                    DetectionTimeMs = sw.Elapsed.TotalMilliseconds
                };
            }

            var best = candidates[0];
            var result = new FormatDetectionResult
            {
                Format = best.Format,
                Blocks = best.Blocks,
                Variables = best.Variables,  // FIX: Copy variables from candidate
                Confidence = best.ConfidenceScore,
                Candidates = candidates,
                ContentAnalysis = contentAnalysis,
                DetectionTimeMs = sw.Elapsed.TotalMilliseconds,
                // D3 — propagate assertion results from winning candidate
                AssertionResults = best.AssertionResults ?? new List<AssertionResult>()
            };

            // Auto-select if high confidence
            if (best.ConfidenceScore >= 0.8)
            {
                result.Success = true;
                result.RequiresUserSelection = false;
            }
            // Ambiguous if multiple candidates with similar scores
            else if (candidates.Count > 1 &&
                     candidates[1].ConfidenceScore >= best.ConfidenceScore - 0.15)
            {
                result.Success = true;
                result.RequiresUserSelection = true;
                result.ErrorMessage = $"Multiple possible formats detected (confidence: {best.ConfidenceScore:P0})";
            }
            // Low confidence but only one option
            else
            {
                result.Success = true;
                result.RequiresUserSelection = false;
            }

            return result;
        }

        /// <summary>
        /// Create result from a single candidate
        /// </summary>
        private FormatDetectionResult CreateResult(FormatMatchCandidate candidate, List<FormatMatchCandidate> allCandidates,
                                                   ContentAnalysisResult contentAnalysis, Stopwatch sw)
        {
            sw.Stop();

            var result = new FormatDetectionResult
            {
                Success = true,
                Format = candidate.Format,
                Blocks = candidate.Blocks,
                Variables = candidate.Variables,
                Confidence = candidate.ConfidenceScore,
                Candidates = allCandidates,
                ContentAnalysis = contentAnalysis,
                RequiresUserSelection = false,
                DetectionTimeMs = sw.Elapsed.TotalMilliseconds
            };

            return result;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get signature strength classification
        /// </summary>
        private SignatureStrength GetSignatureStrength(DetectionRule detection)
        {
            if (detection == null)
                return SignatureStrength.None;

            // v2.0: format with only a Signatures[] array — treat as at least Medium
            if (string.IsNullOrWhiteSpace(detection.Signature))
            {
                if (detection.Signatures != null && detection.Signatures.Count > 0)
                    return detection.Strength != SignatureStrength.Medium ? detection.Strength : SignatureStrength.Medium;
                return SignatureStrength.None;
            }

            // Use configured strength if available
            if (detection.Strength != SignatureStrength.Medium)  // Medium is default
                return detection.Strength;

            // Auto-classify based on signature length and common patterns
            var sig = detection.Signature.Replace(" ", "").Replace("-", "");

            // Very weak signatures
            if (sig.Length <= 2)
                return SignatureStrength.Weak;

            // Unique/strong signatures (8+ bytes)
            if (sig.Length >= 16)
                return SignatureStrength.Unique;

            // Strong signatures (6+ bytes)
            if (sig.Length >= 12)
                return SignatureStrength.Strong;

            // Default: Medium
            return SignatureStrength.Medium;
        }

        /// <summary>
        /// Calculate signature confidence score (0.0 - 1.0)
        /// </summary>
        private double CalculateSignatureConfidence(DetectionRule detection)
        {
            var strength = GetSignatureStrength(detection);
            return (int)strength / 100.0;  // Enum values are 0, 20, 50, 80, 100
        }

        /// <summary>
        /// Check if format matches file extension
        /// </summary>
        private bool MatchesExtension(FormatDefinition format, string fileName)
        {
            if (format?.Extensions == null || format.Extensions.Count == 0)
                return false;

            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(extension))
                return false;

            return format.Extensions.Any(ext =>
                ext.Equals(extension, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Extract the first entry filename from a ZIP archive.
        /// The ZIP Local File Header is 30 bytes, followed by the filename.
        /// </summary>
        private string ExtractZipFirstEntryName(byte[] data)
        {
            try
            {
                // Verify PK signature
                if (data == null || data.Length < 31 || data[0] != 0x50 || data[1] != 0x4B ||
                    data[2] != 0x03 || data[3] != 0x04)
                    return null;

                // Filename length at offset 26 (little-endian uint16)
                int filenameLength = data[26] | (data[27] << 8);
                if (filenameLength <= 0 || filenameLength > 256 || 30 + filenameLength > data.Length)
                    return null;

                return System.Text.Encoding.UTF8.GetString(data, 30, filenameLength);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Score how well a format matches the ZIP first entry content.
        /// Returns 0.0-1.0 based on how specific the match is.
        /// </summary>
        private double ScoreZipContentMatch(FormatDefinition format, string firstEntryName)
        {
            if (string.IsNullOrEmpty(firstEntryName) || format == null)
                return 0.0;

            var name = firstEntryName.ToLowerInvariant();
            var formatName = format.FormatName?.ToLowerInvariant() ?? "";
            var category = format.Category?.ToLowerInvariant() ?? "";

            // OOXML detection: [Content_Types].xml is the definitive OOXML marker
            if (name.Contains("[content_types].xml") || name.StartsWith("word/") ||
                name.StartsWith("xl/") || name.StartsWith("ppt/") ||
                name.StartsWith("_rels/"))
            {
                // Specific OOXML format matching
                if (formatName.Contains("word") || format.Extensions?.Any(e => e == ".docx") == true)
                {
                    if (name.StartsWith("word/")) return 1.0;
                    if (name.Contains("[content_types]")) return 0.8;
                }
                if (formatName.Contains("excel") || format.Extensions?.Any(e => e == ".xlsx") == true)
                {
                    if (name.StartsWith("xl/")) return 1.0;
                    if (name.Contains("[content_types]")) return 0.8;
                }
                if (formatName.Contains("powerpoint") || format.Extensions?.Any(e => e == ".pptx") == true)
                {
                    if (name.StartsWith("ppt/")) return 1.0;
                    if (name.Contains("[content_types]")) return 0.8;
                }

                // Other known OOXML-based formats (XPS, OXPS, Visio, etc.)
                var ooxmlExtensions = new[] { ".xps", ".oxps", ".vsdx", ".docm", ".xlsm",
                    ".pptm", ".dotx", ".xltx", ".potx", ".odt", ".ods", ".odp" };
                if (format.Extensions?.Any(e => ooxmlExtensions.Contains(e.ToLowerInvariant())) == true)
                    return 0.6;

                // Content is definitively OOXML — non-OOXML formats get NO score
                // (prevents CBZ, Keynote, generic ZIP, etc. from matching OOXML files)
                return 0.0;
            }

            // JAR detection: META-INF/ directory
            if (name.StartsWith("meta-inf/"))
            {
                if (format.Extensions?.Any(e => e == ".jar") == true)
                    return 1.0;
                if (format.Extensions?.Any(e => e == ".war" || e == ".ear") == true)
                    return 0.8;
                return 0.0;  // Not a JAR-family format
            }

            // APK detection: AndroidManifest.xml or classes.dex
            if (name == "androidmanifest.xml" || name == "classes.dex")
            {
                if (format.Extensions?.Any(e => e == ".apk") == true)
                    return 1.0;
                if (format.Extensions?.Any(e => e == ".aab") == true)
                    return 0.8;
                return 0.0;  // Not an Android format
            }

            // mimetype file detection: EPUB and ODF formats use 'mimetype' as first entry
            if (name == "mimetype")
            {
                if (format.Extensions?.Any(e => e == ".epub") == true)
                    return 1.0;
                // ODF formats (ODT, ODS, ODP) also use mimetype as first entry
                if (format.Extensions?.Any(e => e == ".odt" || e == ".ods" || e == ".odp" ||
                    e == ".odg" || e == ".odf") == true)
                    return 0.8;
                return 0.0;  // Not an EPUB or ODF format
            }

            // iWork detection: Index/ directory or .iwa files (Keynote, Pages, Numbers)
            if (name.StartsWith("index/") || name.EndsWith(".iwa") || name.StartsWith("metadata/"))
            {
                if (formatName.Contains("keynote") || format.Extensions?.Any(e => e == ".key") == true)
                    return 0.9;
                if (formatName.Contains("pages") || format.Extensions?.Any(e => e == ".pages") == true)
                    return 0.9;
                if (formatName.Contains("numbers") || format.Extensions?.Any(e => e == ".numbers") == true)
                    return 0.9;
                // Generic iWork format
                return 0.0;  // Not an iWork format
            }

            // No specific content match
            return 0.0;
        }

        /// <summary>
        /// Prioritize formats by extension match
        /// </summary>
        private IEnumerable<FormatDefinition> PrioritizeByExtension(IEnumerable<FormatDefinition> formats, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return formats;

            var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(extension))
                return formats;

            var list = formats.ToList();
            var matching = list.Where(f => MatchesExtension(f, fileName)).ToList();
            var remaining = list.Where(f => !matching.Contains(f)).ToList();

            matching.AddRange(remaining);
            return matching;
        }

        #endregion

        /// <summary>
        /// Check if data matches format signature
        /// </summary>
        private bool CheckSignature(byte[] data, DetectionRule detection)
        {
            if (detection == null || !detection.IsValid())
                return false;

            // ── v2.0: multi-signature OR logic ──────────────────────────────────
            if (detection.Signatures != null && detection.Signatures.Count > 0)
            {
                string matchMode = detection.MatchMode ?? "any";
                bool anyMatched = false;

                foreach (var sig in detection.Signatures)
                {
                    if (sig == null || string.IsNullOrWhiteSpace(sig.Value))
                        continue;

                    byte[] sigBytes = ParseHexSignature(sig.Value);
                    if (sigBytes == null) continue;

                    long sigOffset = sig.Offset;
                    if (sigOffset < 0 || sigOffset + sigBytes.Length > data.Length) continue;

                    bool matched = true;
                    for (int i = 0; i < sigBytes.Length; i++)
                    {
                        if (data[sigOffset + i] != sigBytes[i]) { matched = false; break; }
                    }

                    if (matched)
                    {
                        anyMatched = true;
                        if (string.Equals(matchMode, "any", StringComparison.OrdinalIgnoreCase))
                            return true; // short-circuit on first match
                    }
                    else if (string.Equals(matchMode, "all", StringComparison.OrdinalIgnoreCase))
                    {
                        return false; // any miss fails "all" mode
                    }
                }

                return anyMatched;
            }

            // ── Legacy single-signature ──────────────────────────────────────────
            var signatureBytes = detection.GetSignatureBytes();
            if (signatureBytes == null)
                return false;

            long offset = detection.Offset;
            if (offset < 0 || offset + signatureBytes.Length > data.Length)
                return false;

            for (int i = 0; i < signatureBytes.Length; i++)
            {
                if (data[offset + i] != signatureBytes[i])
                    return false;
            }

            return true;
        }

        /// <summary>Parses a hex string (space-separated or compact) into bytes.</summary>
        private static byte[] ParseHexSignature(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return null;
            try
            {
                var clean = hex.Replace(" ", "").Replace("-", "");
                if (clean.Length % 2 != 0) return null;
                var bytes = new byte[clean.Length / 2];
                for (int i = 0; i < bytes.Length; i++)
                    bytes[i] = Convert.ToByte(clean.Substring(i * 2, 2), 16);
                return bytes;
            }
            catch { return null; }
        }

        /// <summary>
        /// Computes Shannon entropy of a byte sample (0.0–8.0 bits/byte).
        /// Uses at most <paramref name="sampleSize"/> bytes from offset 0.
        /// </summary>
        private static double ComputeEntropy(byte[] data, int sampleSize = 512)
        {
            if (data == null || data.Length == 0) return 0.0;
            int len = Math.Min(sampleSize, data.Length);
            var freq = new int[256];
            for (int i = 0; i < len; i++) freq[data[i]]++;
            double entropy = 0.0;
            for (int i = 0; i < 256; i++)
            {
                if (freq[i] == 0) continue;
                double p = (double)freq[i] / len;
                entropy -= p * Math.Log(p, 2);
            }
            return entropy;
        }

        /// <summary>
        /// Get candidate formats for detection
        /// Prioritizes formats matching the file extension
        /// </summary>
        private List<FormatDefinition> GetCandidateFormats(string fileName)
        {
            var candidates = new List<FormatDefinition>();

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(extension))
                {
                    // Add formats matching extension first
                    candidates.AddRange(EffectiveFormats.Where(f =>
                        f.Extensions != null && f.Extensions.Any(ext =>
                            ext.Equals(extension, StringComparison.OrdinalIgnoreCase))));
                }
            }

            // Add remaining formats
            candidates.AddRange(EffectiveFormats.Where(f => !candidates.Contains(f)));

            return candidates;
        }

        /// <summary>
        /// Generate blocks for a known format (skip detection)
        /// </summary>
        /// <param name="data">File data</param>
        /// <param name="format">Format to apply</param>
        /// <param name="byteProvider">Optional ByteProvider for reading beyond data buffer</param>
        /// <returns>Generated blocks</returns>
        public List<CustomBackgroundBlock> GenerateBlocks(byte[] data, FormatDefinition format, ByteProvider byteProvider = null)
        {
            if (data == null || format == null || !format.IsValid())
                return new List<CustomBackgroundBlock>();

            try
            {
                var interpreter = new FormatScriptInterpreter(data, format.Variables, byteProvider);

                // Execute built-in functions first (populates variables)
                if (format.Functions != null && format.Functions.Count > 0)
                {
                    interpreter.ExecuteFunctions(format.Functions);
                }

                return interpreter.ExecuteBlocks(format.Blocks);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error generating blocks for {format.FormatName}: {ex.Message}");
                return new List<CustomBackgroundBlock>();
            }
        }

        #endregion

        #region Import/Export

        /// <summary>
        /// Import format definition from JSON string
        /// </summary>
        /// <param name="json">JSON string</param>
        /// <returns>Format definition or null if invalid</returns>
        public FormatDefinition ImportFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var firstChar = json.AsSpan().TrimStart()[0];
            if (firstChar != '{' && firstChar != '[')
                return null;

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };

                var format = JsonSerializer.Deserialize<FormatDefinition>(json, options);
                return format?.IsValid() == true ? format : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing JSON: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Export format definition to JSON string
        /// </summary>
        /// <param name="format">Format to export</param>
        /// <param name="indented">Whether to indent JSON</param>
        /// <returns>JSON string</returns>
        public string ExportToJson(FormatDefinition format, bool indented = true)
        {
            if (format == null)
                return null;

            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = indented,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                return JsonSerializer.Serialize(format, options);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error serializing format: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// Get format by name
        /// </summary>
        /// <param name="name">Format name</param>
        /// <returns>Format definition or null</returns>
        public FormatDefinition GetFormatByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            return EffectiveFormats.FirstOrDefault(f =>
                f.FormatName.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get formats by file extension
        /// </summary>
        public List<FormatDefinition> GetFormatsByExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return new List<FormatDefinition>();

            var ext = extension.ToLowerInvariant();
            if (!ext.StartsWith("."))
                ext = "." + ext;

            return EffectiveFormats
                .Where(f => f.Extensions != null && f.Extensions.Any(e =>
                    e.Equals(ext, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        /// <summary>
        /// Get all loaded formats (instance + shared catalog)
        /// </summary>
        public List<FormatDefinition> GetAllFormats()
        {
            return EffectiveFormats.ToList();
        }

        /// <summary>
        /// Get number of available formats (instance + shared catalog)
        /// </summary>
        public int GetFormatCount() => EffectiveFormats.Count;

        /// <summary>
        /// Check if any formats are available (instance + shared catalog)
        /// </summary>
        public bool HasFormats() => EffectiveFormats.Count > 0;

        #endregion

        #region Statistics

        /// <summary>
        /// Get statistics about available formats
        /// </summary>
        public FormatStatistics GetStatistics()
        {
            var allFormats = EffectiveFormats;
            return new FormatStatistics
            {
                TotalFormats = allFormats.Count,
                TotalExtensions = allFormats.SelectMany(f => f.Extensions ?? new List<string>()).Distinct().Count(),
                FormatsByCategory = allFormats
                    .GroupBy(f => GetCategory(f.FormatName))
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }

        /// <summary>
        /// Get category from format name
        /// </summary>
        private string GetCategory(string formatName)
        {
            if (string.IsNullOrWhiteSpace(formatName))
                return "Unknown";

            var lower = formatName.ToLowerInvariant();

            // Archives
            if (lower.Contains("archive") || lower.Contains("zip") || lower.Contains("rar") ||
                lower.Contains("7z") || lower.Contains("tar") || lower.Contains("gzip") ||
                lower.Contains("bzip") || lower.Contains("xz") || lower.Contains("cab") || lower.Contains("lzh"))
                return "Archives";

            // Images
            if (lower.Contains("image") || lower.Contains("png") || lower.Contains("jpg") ||
                lower.Contains("jpeg") || lower.Contains("gif") || lower.Contains("bmp") ||
                lower.Contains("tiff") || lower.Contains("webp") || lower.Contains("ico") ||
                lower.Contains("psd") || lower.Contains("svg") || lower.Contains("tga") ||
                lower.Contains("xcf") || lower.Contains("dds") || lower.Contains("pcx"))
                return "Images";

            // Audio
            if (lower.Contains("audio") || lower.Contains("mp3") || lower.Contains("wav") ||
                lower.Contains("flac") || lower.Contains("ogg") || lower.Contains("m4a") ||
                lower.Contains("aac") || lower.Contains("aiff") || lower.Contains("midi"))
                return "Audio";

            // Video
            if (lower.Contains("video") || lower.Contains("mp4") || lower.Contains("avi") ||
                lower.Contains("mkv") || lower.Contains("webm") || lower.Contains("mov") ||
                lower.Contains("flv") || lower.Contains("wmv") || lower.Contains("3gp") || lower.Contains("vob"))
                return "Video";

            // Documents
            if (lower.Contains("document") || lower.Contains("pdf") || lower.Contains("docx") ||
                lower.Contains("xlsx") || lower.Contains("rtf") || lower.Contains("epub") ||
                lower.Contains("ps") || lower.Contains("xml") || lower.Contains("chm") ||
                lower.Contains("djvu") || lower.Contains("mobi") || lower.Contains("azw"))
                return "Documents";

            // Executables
            if (lower.Contains("executable") || lower.Contains("exe") || lower.Contains("elf") ||
                lower.Contains("mach-o") || lower.Contains("dll") || lower.Contains("com"))
                return "Executables";

            // 3D
            if (lower.Contains("3d") || lower.Contains("stl") || lower.Contains("obj") ||
                lower.Contains("3ds") || lower.Contains("fbx") || lower.Contains("model"))
                return "3D";

            // Database
            if (lower.Contains("database") || lower.Contains("sqlite") || lower.Contains("db"))
                return "Database";

            // Fonts
            if (lower.Contains("font") || lower.Contains("ttf") || lower.Contains("otf") ||
                lower.Contains("woff"))
                return "Fonts";

            // Disk Images
            if (lower.Contains("disk") || lower.Contains("iso") || lower.Contains("vhd") ||
                lower.Contains("vmdk") || lower.Contains("vdi"))
                return "Disk";

            // Network
            if (lower.Contains("network") || lower.Contains("pcap") || lower.Contains("packet"))
                return "Network";

            // Programming
            if (lower.Contains("java") || lower.Contains("class") || lower.Contains("dex") ||
                lower.Contains("bytecode") || lower.Contains("wasm") || lower.Contains("lua") ||
                lower.Contains("python") || lower.Contains("script"))
                return "Programming";

            // Game
            if (lower.Contains("game") || lower.Contains("unity") || lower.Contains("unreal") ||
                lower.Contains("rom") || lower.Contains("pak") || lower.Contains("bsp") ||
                lower.Contains("wad") || lower.Contains("minecraft"))
                return "Game";

            // CAD
            if (lower.Contains("cad") || lower.Contains("dwg") || lower.Contains("dxf") ||
                lower.Contains("step") || lower.Contains("iges") || lower.Contains("stl"))
                return "CAD";

            // Medical
            if (lower.Contains("medical") || lower.Contains("dicom") || lower.Contains("nifti") ||
                lower.Contains("imaging"))
                return "Medical";

            // Science
            if (lower.Contains("science") || lower.Contains("fits") || lower.Contains("hdf") ||
                lower.Contains("netcdf") || lower.Contains("matlab") || lower.Contains("scientific"))
                return "Science";

            // Certificates
            if (lower.Contains("certificate") || lower.Contains("der") || lower.Contains("p12") ||
                lower.Contains("pfx"))
                return "Certificates";

            // System
            if (lower.Contains("system") || lower.Contains("dmp") || lower.Contains("reg") ||
                lower.Contains("dump") || lower.Contains("registry") || lower.Contains("evt"))
                return "System";

            // Crypto
            if (lower.Contains("crypto") || lower.Contains("pgp") || lower.Contains("gpg") ||
                lower.Contains("encryption"))
                return "Crypto";

            // Data
            if (lower.Contains("json") || lower.Contains("data") || lower.Contains("yaml") ||
                lower.Contains("toml") || lower.Contains("csv"))
                return "Data";

            return "Other";
        }

        #endregion

        // ============================================================================
        // ISSUE #111: False Positive Detection of Text Files
        // ============================================================================
        // This multi-tier detection system was implemented to fix a critical issue
        // where plain text files without signatures were incorrectly detected as
        // various binary formats (BSON, YAML, CBOR, MSGPACK, etc.) in a non-deterministic
        // manner.
        //
        // ROOT CAUSE:
        // - 87 out of 426 formats had required: false (signature check skipped)
        // - 48 formats used weak "00" signature (null byte) that matches almost any file
        // - Old "first match wins" algorithm was non-deterministic
        // - No content analysis to distinguish text vs binary files
        //
        // SOLUTION:
        // - Tier 1: Strong signature matching (PNG, ZIP, etc.) with early exit
        // - Tier 2: Content analysis for text files (YAML, JSON, XML, CSV detection)
        // - Tier 3: Weak signature matching as last resort with extension filtering
        // - Confidence scoring system (signature 40% + extension 25% + content 20% + structure 15%)
        // - Ambiguity detection for user selection when multiple candidates are similar
        //
        // RESULT:
        // - Plain text files are no longer misdetected as binary formats
        // - Deterministic detection based on content analysis
        // - High-confidence matches (≥ 90%) are auto-selected immediately
        // - Low-confidence or ambiguous cases can prompt user selection
        // ============================================================================
    }

    /// <summary>
    /// Statistics about loaded formats
    /// </summary>
    public class FormatStatistics
    {
        public int TotalFormats { get; set; }
        public int TotalExtensions { get; set; }
        public Dictionary<string, int> FormatsByCategory { get; set; } = new Dictionary<string, int>();

        public override string ToString()
        {
            return $"{TotalFormats} formats, {TotalExtensions} extensions";
        }
    }
}

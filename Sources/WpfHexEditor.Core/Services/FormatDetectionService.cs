//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
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
                // Load all .json files recursively
                var jsonFiles = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories);

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
        /// - "C:/FormatDefinitions/Archives/ZIP.json" -> "Archives"
        /// - "FormatDefinitions/Images/PNG.json" -> "Images"
        /// - "WpfHexEditor.Core.FormatDefinitions.Archives.ZIP.json" (embedded) -> "Archives"
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
                    // Embedded resource format: "WpfHexEditor.Core.FormatDefinitions.Archives.ZIP.json"
                    var parts = path.Split('.');
                    var formatDefsIndex = Array.IndexOf(parts, "FormatDefinitions");
                    if (formatDefsIndex >= 0 && formatDefsIndex < parts.Length - 2)
                    {
                        return parts[formatDefsIndex + 1]; // Category is next part after "FormatDefinitions"
                    }
                }
                else
                {
                    // File path format: "C:/FormatDefinitions/Archives/ZIP.json"
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

            // Step 2: Multi-tier detection

            // TIER 1: Strong signatures (required: true, unique/strong)
            var tier1 = DetectWithStrongSignatures(data, fileName, contentAnalysis, byteProvider);
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
                var tier2 = DetectTextFormats(data, fileName, contentAnalysis, byteProvider);
                candidates.AddRange(tier2);
            }

            // TIER 3: Weak signatures (only if no strong match)
            if (!candidates.Any())
            {
                var tier3 = DetectWithWeakSignatures(data, fileName, contentAnalysis, byteProvider);
                candidates.AddRange(tier3);
            }

            // Step 3: Score and rank candidates
            ScoreAndRankCandidates(candidates, fileName, contentAnalysis, data);

            // Step 4: Decision logic
            return DecideFormat(candidates, contentAnalysis, sw);
        }

        /// <summary>
        /// Try to detect a specific format
        /// </summary>
        private bool TryDetectFormat(byte[] data, FormatDefinition format, out List<CustomBackgroundBlock> blocks, out Dictionary<string, object> variables, ByteProvider byteProvider = null)
        {
            blocks = new List<CustomBackgroundBlock>();
            variables = new Dictionary<string, object>();

            if (format == null || !format.IsValid())
                return false;

            // Check signature
            if (format.Detection != null && format.Detection.Required)
            {
                if (!CheckSignature(data, format.Detection))
                    return false;
            }

            // Generate blocks using interpreter
            try
            {
                var interpreter = new FormatScriptInterpreter(data, format.Variables, byteProvider);

                // Execute built-in functions first (populates variables)
                if (format.Functions != null && format.Functions.Count > 0)
                {
                    interpreter.ExecuteFunctions(format.Functions);
                }

                blocks = interpreter.ExecuteBlocks(format.Blocks);

                // Copy variables from interpreter (includes function results)
                variables = new Dictionary<string, object>(interpreter.Variables);

                return blocks.Count > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FormatDetection] Error executing format {format.FormatName}: {ex.Message}");
                return false;
            }
        }

        #region Multi-Tier Detection Methods

        /// <summary>
        /// TIER 1: Detect formats with strong signatures (required: true, unique/strong)
        /// </summary>
        private List<FormatMatchCandidate> DetectWithStrongSignatures(byte[] data, string fileName, ContentAnalysisResult contentAnalysis, ByteProvider byteProvider)
        {
            var candidates = new List<FormatMatchCandidate>();

            // Only check formats with required: true AND medium+ signature strength
            var strongFormats = _loadedFormats
                .Where(f => f.Detection?.Required == true)
                .Where(f => GetSignatureStrength(f.Detection) >= SignatureStrength.Medium)
                .OrderByDescending(f => GetSignatureStrength(f.Detection));

            // Prioritize by extension match
            var formatsByPriority = PrioritizeByExtension(strongFormats, fileName);

            foreach (var format in formatsByPriority)
            {
                if (TryDetectFormat(data, format, out var blocks, out var variables, byteProvider))
                {
                    var candidate = new FormatMatchCandidate
                    {
                        Format = format,
                        Blocks = blocks,
                        Variables = variables,
                        Tier = MatchTier.StrongSignature,
                        SignatureConfidence = CalculateSignatureConfidence(format.Detection)
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
        private List<FormatMatchCandidate> DetectTextFormats(byte[] data, string fileName, ContentAnalysisResult contentAnalysis, ByteProvider byteProvider)
        {
            var candidates = new List<FormatMatchCandidate>();

            // Only consider formats marked as text-based
            var textFormats = _loadedFormats.Where(f => f.Detection?.IsTextFormat == true).ToList();

            foreach (var format in textFormats)
            {
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
                if (TryDetectFormat(data, format, out var blocks, out var variables, byteProvider))
                {
                    contentScore += 0.2;

                    var candidate = new FormatMatchCandidate
                    {
                        Format = format,
                        Blocks = blocks,
                        Variables = variables,
                        Tier = MatchTier.ContentBased,
                        ContentConfidence = contentScore
                    };

                    candidates.Add(candidate);
                }
            }

            return candidates;
        }

        /// <summary>
        /// TIER 3: Detect formats with weak/no signatures (last resort)
        /// </summary>
        private List<FormatMatchCandidate> DetectWithWeakSignatures(byte[] data, string fileName, ContentAnalysisResult contentAnalysis, ByteProvider byteProvider)
        {
            var candidates = new List<FormatMatchCandidate>();

            // Only check formats with weak/no signatures
            var weakFormats = _loadedFormats
                .Where(f => f.Detection?.Required == false ||
                           GetSignatureStrength(f.Detection) <= SignatureStrength.Weak);

            // Heavily weight extension matching for weak signatures
            var extensionMatches = weakFormats.Where(f => MatchesExtension(f, fileName));

            foreach (var format in extensionMatches)
            {
                if (TryDetectFormat(data, format, out var blocks, out var variables, byteProvider))
                {
                    var candidate = new FormatMatchCandidate
                    {
                        Format = format,
                        Blocks = blocks,
                        Variables = variables,
                        Tier = MatchTier.FallbackWeak,
                        ExtensionConfidence = 0.6  // Lower confidence for weak matches
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
                DetectionTimeMs = sw.Elapsed.TotalMilliseconds
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
            if (detection == null || string.IsNullOrWhiteSpace(detection.Signature))
                return SignatureStrength.None;

            // Use configured strength if available
            if (detection.Strength != SignatureStrength.Medium)  // Medium is default
                return detection.Strength;

            // Auto-classify based on signature length and common patterns
            var sig = detection.Signature;

            // Very weak signatures
            if (sig == "00" || sig == "FF" || sig.Length == 2)
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

            var signatureBytes = detection.GetSignatureBytes();
            if (signatureBytes == null)
                return false;

            long offset = detection.Offset;
            if (offset < 0 || offset + signatureBytes.Length > data.Length)
                return false;

            // Compare bytes
            for (int i = 0; i < signatureBytes.Length; i++)
            {
                if (data[offset + i] != signatureBytes[i])
                    return false;
            }

            return true;
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
                    candidates.AddRange(_loadedFormats.Where(f =>
                        f.Extensions != null && f.Extensions.Any(ext =>
                            ext.Equals(extension, StringComparison.OrdinalIgnoreCase))));
                }
            }

            // Add remaining formats
            candidates.AddRange(_loadedFormats.Where(f => !candidates.Contains(f)));

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

            return _loadedFormats.FirstOrDefault(f =>
                f.FormatName.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get formats by file extension
        /// </summary>
        /// <param name="extension">File extension (e.g., ".zip", ".png")</param>
        /// <returns>List of matching formats</returns>
        public List<FormatDefinition> GetFormatsByExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return new List<FormatDefinition>();

            var ext = extension.ToLowerInvariant();
            if (!ext.StartsWith("."))
                ext = "." + ext;

            return _loadedFormats
                .Where(f => f.Extensions != null && f.Extensions.Any(e =>
                    e.Equals(ext, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        /// <summary>
        /// Get all loaded formats
        /// </summary>
        /// <returns>List of all formats</returns>
        public List<FormatDefinition> GetAllFormats()
        {
            return _loadedFormats.ToList();
        }

        /// <summary>
        /// Get number of loaded formats
        /// </summary>
        public int GetFormatCount() => _loadedFormats.Count;

        /// <summary>
        /// Check if any formats are loaded
        /// </summary>
        public bool HasFormats() => _loadedFormats.Count > 0;

        #endregion

        #region Statistics

        /// <summary>
        /// Get statistics about loaded formats
        /// </summary>
        public FormatStatistics GetStatistics()
        {
            return new FormatStatistics
            {
                TotalFormats = _loadedFormats.Count,
                TotalExtensions = _loadedFormats.SelectMany(f => f.Extensions ?? new List<string>()).Distinct().Count(),
                FormatsByCategory = _loadedFormats
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

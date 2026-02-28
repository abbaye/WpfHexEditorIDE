//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using WpfHexEditor.Core.FormatDetection;

namespace WpfHexEditor.Core.Tools
{
    /// <summary>
    /// Validator for format definition JSON files
    /// Ensures all required sections are present and valid
    /// </summary>
    public class FormatDefinitionValidator
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        /// <summary>
        /// Validates a format definition file
        /// </summary>
        /// <param name="filePath">Path to JSON file</param>
        /// <returns>Validation result</returns>
        public ValidationResult ValidateFile(string filePath)
        {
            var result = new ValidationResult
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath)
            };

            try
            {
                if (!File.Exists(filePath))
                {
                    result.IsValid = false;
                    result.Errors.Add("File does not exist");
                    return result;
                }

                string jsonContent = File.ReadAllText(filePath);
                return ValidateJson(jsonContent, filePath);
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Exception: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Validates JSON content
        /// </summary>
        /// <param name="jsonContent">JSON string</param>
        /// <param name="filePath">Optional file path for context</param>
        /// <returns>Validation result</returns>
        public ValidationResult ValidateJson(string jsonContent, string filePath = null)
        {
            var result = new ValidationResult
            {
                FilePath = filePath,
                FileName = filePath != null ? Path.GetFileName(filePath) : "N/A"
            };

            try
            {
                // Parse JSON
                using (JsonDocument doc = JsonDocument.Parse(jsonContent, new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                }))
                {
                    JsonElement root = doc.RootElement;

                    // Validate all required top-level sections
                    ValidateRequiredString(root, "formatName", result);
                    ValidateRequiredString(root, "version", result);
                    ValidateRequiredArray(root, "extensions", result);
                    ValidateRequiredString(root, "description", result);
                    ValidateRequiredString(root, "category", result);
                    ValidateRequiredString(root, "author", result);
                    ValidateDetectionSection(root, result);
                    ValidateVariablesSection(root, result);
                    ValidateBlocksSection(root, result);

                    // Try to deserialize to FormatDefinition
                    try
                    {
                        var format = JsonSerializer.Deserialize<FormatDefinition>(jsonContent, JsonOptions);
                        if (format != null)
                        {
                            if (!format.IsValid())
                            {
                                result.Warnings.Add("FormatDefinition.IsValid() returned false");
                            }
                        }
                        else
                        {
                            result.Errors.Add("Failed to deserialize to FormatDefinition");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Deserialization error: {ex.Message}");
                    }
                }

                result.IsValid = result.Errors.Count == 0;
            }
            catch (JsonException ex)
            {
                result.IsValid = false;
                result.Errors.Add($"JSON parsing error: {ex.Message}");
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Unexpected error: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Validates all format definition files in a directory
        /// </summary>
        /// <param name="directory">Directory path</param>
        /// <param name="recursive">Search recursively</param>
        /// <returns>List of validation results</returns>
        public List<ValidationResult> ValidateDirectory(string directory, bool recursive = true)
        {
            var results = new List<ValidationResult>();

            if (!Directory.Exists(directory))
            {
                var errorResult = new ValidationResult
                {
                    FilePath = directory,
                    FileName = directory,
                    IsValid = false
                };
                errorResult.Errors.Add("Directory does not exist");
                results.Add(errorResult);
                return results;
            }

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var jsonFiles = Directory.GetFiles(directory, "*.json", searchOption);

            foreach (var file in jsonFiles)
            {
                results.Add(ValidateFile(file));
            }

            return results;
        }

        /// <summary>
        /// Validates all format definitions in the FormatDefinitions directory
        /// </summary>
        /// <returns>List of validation results</returns>
        public List<ValidationResult> ValidateAllDefinitions(string formatDefinitionsPath = null)
        {
            // Default path relative to project
            if (string.IsNullOrEmpty(formatDefinitionsPath))
            {
                formatDefinitionsPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "FormatDefinitions"
                );
            }

            return ValidateDirectory(formatDefinitionsPath, recursive: true);
        }

        #region Private Validation Methods

        private void ValidateRequiredString(JsonElement root, string propertyName, ValidationResult result)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement property))
            {
                result.Errors.Add($"Missing required property: {propertyName}");
                return;
            }

            if (property.ValueKind != JsonValueKind.String)
            {
                result.Errors.Add($"Property '{propertyName}' must be a string");
                return;
            }

            string value = property.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                result.Errors.Add($"Property '{propertyName}' cannot be empty");
            }
        }

        private void ValidateRequiredArray(JsonElement root, string propertyName, ValidationResult result)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement property))
            {
                result.Errors.Add($"Missing required property: {propertyName}");
                return;
            }

            if (property.ValueKind != JsonValueKind.Array)
            {
                result.Errors.Add($"Property '{propertyName}' must be an array");
                return;
            }

            if (property.GetArrayLength() == 0)
            {
                result.Errors.Add($"Property '{propertyName}' array cannot be empty");
            }
        }

        private void ValidateDetectionSection(JsonElement root, ValidationResult result)
        {
            if (!root.TryGetProperty("detection", out JsonElement detection))
            {
                result.Errors.Add("Missing required property: detection");
                return;
            }

            if (detection.ValueKind != JsonValueKind.Object)
            {
                result.Errors.Add("Property 'detection' must be an object");
                return;
            }

            // Validate signature
            if (!detection.TryGetProperty("signature", out JsonElement signature))
            {
                result.Errors.Add("Detection section missing required property: signature");
            }
            else if (signature.ValueKind != JsonValueKind.String)
            {
                result.Errors.Add("Detection.signature must be a string");
            }
            else
            {
                string sig = signature.GetString();
                if (string.IsNullOrWhiteSpace(sig))
                {
                    result.Errors.Add("Detection.signature cannot be empty");
                }
                else
                {
                    // Validate hex format
                    if (sig.Length % 2 != 0)
                    {
                        result.Errors.Add("Detection.signature must have even number of hex digits");
                    }
                    else
                    {
                        foreach (char c in sig)
                        {
                            if (!Uri.IsHexDigit(c))
                            {
                                result.Errors.Add($"Detection.signature contains invalid hex character: {c}");
                                break;
                            }
                        }
                    }
                }
            }

            // Validate offset
            if (!detection.TryGetProperty("offset", out JsonElement offset))
            {
                result.Errors.Add("Detection section missing required property: offset");
            }
            else if (offset.ValueKind != JsonValueKind.Number)
            {
                result.Errors.Add("Detection.offset must be a number");
            }
            else if (offset.TryGetInt64(out long offsetValue) && offsetValue < 0)
            {
                result.Errors.Add("Detection.offset cannot be negative");
            }

            // Validate required (optional, but if present must be boolean)
            if (detection.TryGetProperty("required", out JsonElement required))
            {
                if (required.ValueKind != JsonValueKind.True && required.ValueKind != JsonValueKind.False)
                {
                    result.Errors.Add("Detection.required must be a boolean");
                }
            }
        }

        private void ValidateVariablesSection(JsonElement root, ValidationResult result)
        {
            if (!root.TryGetProperty("variables", out JsonElement variables))
            {
                result.Errors.Add("Missing required property: variables");
                return;
            }

            if (variables.ValueKind != JsonValueKind.Object)
            {
                result.Errors.Add("Property 'variables' must be an object");
                return;
            }

            // Variables can be empty {}, so no additional validation needed
        }

        private void ValidateBlocksSection(JsonElement root, ValidationResult result)
        {
            if (!root.TryGetProperty("blocks", out JsonElement blocks))
            {
                result.Errors.Add("Missing required property: blocks");
                return;
            }

            if (blocks.ValueKind != JsonValueKind.Array)
            {
                result.Errors.Add("Property 'blocks' must be an array");
                return;
            }

            if (blocks.GetArrayLength() == 0)
            {
                result.Errors.Add("Property 'blocks' array cannot be empty");
                return;
            }

            // Validate each block
            int blockIndex = 0;
            foreach (JsonElement block in blocks.EnumerateArray())
            {
                ValidateBlock(block, blockIndex, result);
                blockIndex++;
            }
        }

        private void ValidateBlock(JsonElement block, int index, ValidationResult result)
        {
            string blockPrefix = $"Block[{index}]";

            if (block.ValueKind != JsonValueKind.Object)
            {
                result.Errors.Add($"{blockPrefix}: Must be an object");
                return;
            }

            // Validate type
            if (!block.TryGetProperty("type", out JsonElement type))
            {
                result.Errors.Add($"{blockPrefix}: Missing required property 'type'");
            }
            else if (type.ValueKind != JsonValueKind.String)
            {
                result.Errors.Add($"{blockPrefix}: Property 'type' must be a string");
            }
            else
            {
                string typeValue = type.GetString();
                var validTypes = new[] { "signature", "field", "conditional", "loop", "action" };
                if (!validTypes.Contains(typeValue))
                {
                    result.Warnings.Add($"{blockPrefix}: Unknown block type '{typeValue}'. Valid types: {string.Join(", ", validTypes)}");
                }
            }

            // Validate common properties (for signature and field blocks)
            if (block.TryGetProperty("type", out JsonElement blockType))
            {
                string typeStr = blockType.GetString();
                if (typeStr == "signature" || typeStr == "field")
                {
                    // These require name, offset, length, color, description
                    ValidateBlockProperty(block, "name", JsonValueKind.String, blockPrefix, result);
                    ValidateBlockProperty(block, "description", JsonValueKind.String, blockPrefix, result);
                    ValidateBlockProperty(block, "color", JsonValueKind.String, blockPrefix, result);

                    // offset and length can be int or string
                    if (!block.TryGetProperty("offset", out _))
                    {
                        result.Errors.Add($"{blockPrefix}: Missing required property 'offset'");
                    }

                    if (!block.TryGetProperty("length", out _))
                    {
                        result.Errors.Add($"{blockPrefix}: Missing required property 'length'");
                    }

                    // Validate color format if present
                    if (block.TryGetProperty("color", out JsonElement color) && color.ValueKind == JsonValueKind.String)
                    {
                        string colorValue = color.GetString();
                        if (!string.IsNullOrEmpty(colorValue) && !colorValue.StartsWith("#"))
                        {
                            result.Warnings.Add($"{blockPrefix}: Color '{colorValue}' should start with '#' for hex format");
                        }
                    }
                }
            }
        }

        private void ValidateBlockProperty(JsonElement block, string propertyName, JsonValueKind expectedKind, string blockPrefix, ValidationResult result)
        {
            if (!block.TryGetProperty(propertyName, out JsonElement property))
            {
                result.Warnings.Add($"{blockPrefix}: Missing recommended property '{propertyName}'");
            }
            else if (property.ValueKind != expectedKind && !(expectedKind == JsonValueKind.String && property.ValueKind == JsonValueKind.Null))
            {
                result.Errors.Add($"{blockPrefix}: Property '{propertyName}' has wrong type. Expected {expectedKind}, got {property.ValueKind}");
            }
        }

        #endregion

        /// <summary>
        /// Generates a summary report from validation results
        /// </summary>
        public static ValidationSummary GenerateSummary(List<ValidationResult> results)
        {
            var summary = new ValidationSummary
            {
                TotalFiles = results.Count,
                ValidFiles = results.Count(r => r.IsValid),
                InvalidFiles = results.Count(r => !r.IsValid),
                TotalErrors = results.Sum(r => r.Errors.Count),
                TotalWarnings = results.Sum(r => r.Warnings.Count)
            };

            summary.InvalidFilesList = results
                .Where(r => !r.IsValid)
                .Select(r => r.FilePath ?? r.FileName)
                .ToList();

            return summary;
        }
    }

    /// <summary>
    /// Result of validating a single format definition file
    /// </summary>
    public class ValidationResult
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();

        public override string ToString()
        {
            string status = IsValid ? "✓ VALID" : "✗ INVALID";
            string details = "";

            if (Errors.Count > 0)
                details += $" ({Errors.Count} error{(Errors.Count > 1 ? "s" : "")})";
            if (Warnings.Count > 0)
                details += $" ({Warnings.Count} warning{(Warnings.Count > 1 ? "s" : "")})";

            return $"{status} - {FileName}{details}";
        }
    }

    /// <summary>
    /// Summary of validation results for multiple files
    /// </summary>
    public class ValidationSummary
    {
        public int TotalFiles { get; set; }
        public int ValidFiles { get; set; }
        public int InvalidFiles { get; set; }
        public int TotalErrors { get; set; }
        public int TotalWarnings { get; set; }
        public List<string> InvalidFilesList { get; set; } = new List<string>();

        public override string ToString()
        {
            return $@"
=== Validation Summary ===
Total Files:    {TotalFiles}
Valid Files:    {ValidFiles}
Invalid Files:  {InvalidFiles}
Total Errors:   {TotalErrors}
Total Warnings: {TotalWarnings}
Success Rate:   {(TotalFiles > 0 ? (ValidFiles * 100.0 / TotalFiles).ToString("F1") : "0")}%
";
        }
    }
}

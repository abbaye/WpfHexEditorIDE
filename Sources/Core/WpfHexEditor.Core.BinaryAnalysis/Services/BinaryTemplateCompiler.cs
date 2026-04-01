//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Binary Template Compiler - Converts C-like templates to format definitions
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com), Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using WpfHexEditor.Core.BinaryAnalysis.Models.BinaryTemplates;

namespace WpfHexEditor.Core.BinaryAnalysis.Services
{
    /// <summary>
    /// Service for compiling binary templates (C-like syntax) to format definitions (JSON)
    /// Provides compatibility with 010 Editor-style templates
    /// </summary>
    public class BinaryTemplateCompiler
    {
        #region Regular Expressions

        private static readonly Regex StructRegex = new Regex(
            @"struct\s+(\w+)\s*\{([^}]+)\}",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        private static readonly Regex FieldRegex = new Regex(
            @"(?://(.*))?[\r\n]*\s*(\w+)\s+(\w+)(\[\d*\])?\s*;",
            RegexOptions.Multiline);

        private static readonly Regex TypedefRegex = new Regex(
            @"typedef\s+(\w+)\s+(\w+)\s*;",
            RegexOptions.Multiline);

        #endregion

        #region Type Mappings

        private static readonly Dictionary<string, string> TypeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Standard C types
            { "char", "int8" },
            { "byte", "uint8" },
            { "uchar", "uint8" },
            { "short", "int16" },
            { "ushort", "uint16" },
            { "int", "int32" },
            { "uint", "uint32" },
            { "long", "int32" },
            { "ulong", "uint32" },
            { "int64", "int64" },
            { "uint64", "uint64" },
            { "float", "float" },
            { "double", "double" },

            // Windows types (BYTE covered by "byte" via OrdinalIgnoreCase)
            { "WORD", "uint16" },
            { "DWORD", "uint32" },
            { "QWORD", "uint64" },

            // Explicitly sized types
            { "int8", "int8" },
            { "int16", "int16" },
            { "int32", "int32" },
            { "uint8", "uint8" },
            { "uint16", "uint16" },
            { "uint32", "uint32" }
        };

        #endregion

        #region Public Methods

        /// <summary>
        /// Compile binary template script to format definition JSON
        /// </summary>
        public JsonObject CompileTemplate(string templateScript, string formatName = "Generated Format")
        {
            if (string.IsNullOrWhiteSpace(templateScript))
                throw new ArgumentException("Template script cannot be empty");

            try
            {
                var format = new JsonObject
                {
                    ["formatName"] = JsonValue.Create(formatName),
                    ["version"] = JsonValue.Create("1.0"),
                    ["category"] = JsonValue.Create("Custom"),
                    ["description"] = JsonValue.Create("Generated from binary template"),
                    ["blocks"] = new JsonArray()
                };

                var blocks = format["blocks"]!.AsArray();

                // Parse structs
                var structMatches = StructRegex.Matches(templateScript);
                foreach (Match structMatch in structMatches)
                {
                    var structName = structMatch.Groups[1].Value;
                    var structBody = structMatch.Groups[2].Value;

                    var block = ParseStruct(structName, structBody);
                    if (block != null)
                    {
                        blocks.Add(block);
                    }
                }

                // If no structs found, parse as flat fields
                if (blocks.Count == 0)
                {
                    var block = ParseFlatFields(templateScript);
                    if (block != null)
                    {
                        blocks.Add(block);
                    }
                }

                return format;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Template compilation failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Parse template structure to model
        /// </summary>
        public TemplateStructure ParseTemplateToModel(string templateScript)
        {
            var structure = new TemplateStructure
            {
                Script = templateScript
            };

            // Extract name from comments or default
            var nameMatch = Regex.Match(templateScript, @"//\s*Template:\s*(.+)");
            structure.Name = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : "Unnamed Template";

            // Extract description
            var descMatch = Regex.Match(templateScript, @"//\s*Description:\s*(.+)");
            structure.Description = descMatch.Success ? descMatch.Groups[1].Value.Trim() : "";

            // Parse fields
            var fieldMatches = FieldRegex.Matches(templateScript);
            foreach (Match match in fieldMatches)
            {
                var comment = match.Groups[1].Success ? match.Groups[1].Value.Trim() : "";
                var type = match.Groups[2].Value;
                var name = match.Groups[3].Value;
                var arraySize = match.Groups[4].Success ? match.Groups[4].Value : "";

                structure.Fields.Add(new TemplateField
                {
                    Name = name,
                    Type = type,
                    ArraySize = arraySize,
                    Comment = comment
                });
            }

            return structure;
        }

        /// <summary>
        /// Generate template script from format definition
        /// </summary>
        public string GenerateTemplateFromFormat(JsonObject formatDefinition)
        {
            var template = new StringBuilder();

            // Header comments
            template.AppendLine($"// Template: {formatDefinition["formatName"]}");
            template.AppendLine($"// Version: {formatDefinition["version"]}");
            template.AppendLine($"// Description: {formatDefinition["description"]}");
            template.AppendLine();

            // Parse blocks
            var blocks = formatDefinition["blocks"]?.AsArray();
            if (blocks != null)
            {
                foreach (var block in blocks)
                {
                    var blockObj = block as JsonObject;
                    var blockType = blockObj?["type"]?.ToString();
                    if (blockType == "field")
                    {
                        var fields = blockObj?["fields"]?.AsArray();
                        if (fields != null)
                        {
                            template.AppendLine("struct FileFormat {");
                            foreach (var field in fields)
                            {
                                var fieldObj = field as JsonObject;
                                var fieldName = fieldObj?["name"]?.ToString();
                                var fieldType = fieldObj?["type"]?.ToString();
                                var description = fieldObj?["description"]?.ToString();

                                if (!string.IsNullOrEmpty(description))
                                {
                                    template.AppendLine($"    // {description}");
                                }

                                var cType = MapJsonTypeToC(fieldType);
                                template.AppendLine($"    {cType} {fieldName};");
                            }
                            template.AppendLine("};");
                        }
                    }
                }
            }

            return template.ToString();
        }

        #endregion

        #region Private Methods

        private JsonObject? ParseStruct(string structName, string structBody)
        {
            var block = new JsonObject
            {
                ["type"] = JsonValue.Create("field"),
                ["name"] = JsonValue.Create(structName),
                ["fields"] = new JsonArray()
            };

            var fields = block["fields"]!.AsArray();
            var fieldMatches = FieldRegex.Matches(structBody);

            foreach (Match match in fieldMatches)
            {
                var comment = match.Groups[1].Success ? match.Groups[1].Value.Trim() : "";
                var type = match.Groups[2].Value;
                var name = match.Groups[3].Value;
                var arraySize = match.Groups[4].Success ? match.Groups[4].Value.Trim('[', ']') : "";

                var field = new JsonObject
                {
                    ["name"] = JsonValue.Create(name),
                    ["type"] = JsonValue.Create(MapCTypeToJson(type))
                };

                if (!string.IsNullOrEmpty(arraySize))
                {
                    field["length"] = JsonValue.Create(int.TryParse(arraySize, out int len) ? len : 0);
                }

                if (!string.IsNullOrEmpty(comment))
                {
                    field["description"] = JsonValue.Create(comment);
                }

                fields.Add(field);
            }

            return fields.Count > 0 ? block : null;
        }

        private JsonObject? ParseFlatFields(string templateScript)
        {
            var block = new JsonObject
            {
                ["type"] = JsonValue.Create("field"),
                ["fields"] = new JsonArray()
            };

            var fields = block["fields"]!.AsArray();
            var fieldMatches = FieldRegex.Matches(templateScript);

            foreach (Match match in fieldMatches)
            {
                var comment = match.Groups[1].Success ? match.Groups[1].Value.Trim() : "";
                var type = match.Groups[2].Value;
                var name = match.Groups[3].Value;

                var field = new JsonObject
                {
                    ["name"] = JsonValue.Create(name),
                    ["type"] = JsonValue.Create(MapCTypeToJson(type))
                };

                if (!string.IsNullOrEmpty(comment))
                {
                    field["description"] = JsonValue.Create(comment);
                }

                fields.Add(field);
            }

            return fields.Count > 0 ? block : null;
        }

        private string MapCTypeToJson(string cType)
        {
            return TypeMap.ContainsKey(cType) ? TypeMap[cType] : "bytes";
        }

        private string MapJsonTypeToC(string? jsonType)
        {
            if (jsonType is null) return "byte";
            foreach (var kvp in TypeMap)
            {
                if (kvp.Value == jsonType)
                    return kvp.Key;
            }
            return "byte";
        }

        #endregion
    }
}

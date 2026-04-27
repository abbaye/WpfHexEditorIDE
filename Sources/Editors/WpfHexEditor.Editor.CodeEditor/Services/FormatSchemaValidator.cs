//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Custom CodeEditor - Format Schema Validator (Phase 6)
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com), Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using WpfHexEditor.Editor.CodeEditor.Models;

namespace WpfHexEditor.Editor.CodeEditor.Services
{
    /// <summary>
    /// Validates format definition JSON with 4 layers of validation:
    /// Layer 1: JSON syntax validation
    /// Layer 2: Schema validation (required properties)
    /// Layer 3: Format rules validation (data types, values, etc.)
    /// Layer 4: Semantic validation (variable references, calc expressions)
    /// </summary>
    public class FormatSchemaValidator
    {
        #region Required Properties

        private static readonly string[] RootRequiredProperties = { "formatName", "version", "blocks" };
        private static readonly string[] BlockRequiredProperties = { "type" };
        private static readonly string[] FieldRequiredProperties = { "type", "name" };

        private static readonly string[] ValidBlockTypes =
        {
            "signature", "field", "conditional", "loop", "action",
            "computeFromVariables", "metadata", "data", "header",
            "pointer", "repeating",
            "group", "union", "nested"
        };
        private static readonly string[] ValidFieldTypes =
        {
            "uint8", "uint16", "uint32", "uint64",
            "int8", "int16", "int32", "int64",
            "float", "double",
            "string", "ascii", "utf8", "utf16", "bytes",
            "bool", "char", "uuid", "padding"
        };

        #endregion

        #region Public Methods

        /// <summary>
        /// Validate format definition JSON and return list of errors
        /// </summary>
        public List<ValidationError> Validate(string jsonText)
        {
            var errors = new List<ValidationError>();

            if (string.IsNullOrWhiteSpace(jsonText))
            {
                errors.Add(new ValidationError(0, 0, "Document is empty", ValidationSeverity.Error)
                {
                    ErrorCode = "EMPTY_DOCUMENT",
                    Layer = ValidationLayer.JsonSyntax
                });
                return errors;
            }

            // Layer 1: JSON Syntax Validation
            JsonObject root;
            try
            {
                root = JsonNode.Parse(jsonText)?.AsObject()
                    ?? throw new JsonException("Root element is not a JSON object.");
            }
            catch (JsonException ex)
            {
                errors.Add(new ValidationError(
                    (int)(ex.LineNumber ?? 1) - 1,
                    (int)(ex.BytePositionInLine ?? 1) - 1,
                    $"JSON syntax error: {ex.Message}", ValidationSeverity.Error)
                {
                    ErrorCode = "JSON_SYNTAX",
                    Layer = ValidationLayer.JsonSyntax
                });
                return errors; // Cannot continue without valid JSON
            }
            catch (Exception ex)
            {
                errors.Add(new ValidationError(0, 0, $"Failed to parse JSON: {ex.Message}", ValidationSeverity.Error)
                {
                    ErrorCode = "PARSE_ERROR",
                    Layer = ValidationLayer.JsonSyntax
                });
                return errors;
            }

            // Layer 2: Schema Validation
            ValidateSchema(root, errors, jsonText);

            // Layer 3: Format Rules Validation
            ValidateFormatRules(root, errors, jsonText);

            // Layer 4: Semantic Validation
            ValidateSemantics(root, errors, jsonText);

            return errors;
        }

        #endregion

        #region Layer 2: Schema Validation

        private void ValidateSchema(JsonObject root, List<ValidationError> errors, string jsonText)
        {
            // Check required root properties
            foreach (var propName in RootRequiredProperties)
            {
                if (!root.ContainsKey(propName))
                {
                    errors.Add(new ValidationError(0, 0,
                        $"Missing required property '{propName}'", ValidationSeverity.Error)
                    {
                        ErrorCode = "MISSING_REQUIRED_PROP",
                        Layer = ValidationLayer.Schema
                    });
                }
            }

            // Validate formatName
            if (root.ContainsKey("formatName"))
            {
                var formatName = root["formatName"]?.ToString();
                if (string.IsNullOrWhiteSpace(formatName))
                {
                    var location = FindPropertyLocation(jsonText, "formatName");
                    errors.Add(new ValidationError(location.Line, location.Column,
                        "formatName cannot be empty", ValidationSeverity.Error)
                    {
                        ErrorCode = "EMPTY_FORMAT_NAME",
                        Layer = ValidationLayer.Schema,
                        Length = "formatName".Length
                    });
                }
            }

            // Validate blocks array
            if (root.ContainsKey("blocks"))
            {
                var blocks = root["blocks"] as JsonArray;
                if (blocks == null)
                {
                    var location = FindPropertyLocation(jsonText, "blocks");
                    errors.Add(new ValidationError(location.Line, location.Column,
                        "blocks must be an array", ValidationSeverity.Error)
                    {
                        ErrorCode = "INVALID_BLOCKS_TYPE",
                        Layer = ValidationLayer.Schema
                    });
                }
                else if (blocks.Count == 0)
                {
                    var location = FindPropertyLocation(jsonText, "blocks");
                    errors.Add(new ValidationError(location.Line, location.Column,
                        "blocks array cannot be empty", ValidationSeverity.Warning)
                    {
                        ErrorCode = "EMPTY_BLOCKS",
                        Layer = ValidationLayer.Schema
                    });
                }
                else
                {
                    ValidateBlocks(blocks, errors, jsonText);
                }
            }
        }

        private void ValidateBlocks(JsonArray blocks, List<ValidationError> errors, string jsonText)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i] as JsonObject;
                if (block == null)
                {
                    errors.Add(new ValidationError(0, 0,
                        $"Block {i} must be an object", ValidationSeverity.Error)
                    {
                        ErrorCode = "INVALID_BLOCK_TYPE",
                        Layer = ValidationLayer.Schema
                    });
                    continue;
                }

                // Check required block properties
                foreach (var propName in BlockRequiredProperties)
                {
                    if (!block.ContainsKey(propName))
                    {
                        errors.Add(new ValidationError(0, 0,
                            $"Block {i} missing required property '{propName}'", ValidationSeverity.Error)
                        {
                            ErrorCode = "MISSING_BLOCK_PROP",
                            Layer = ValidationLayer.Schema
                        });
                    }
                }

                // Validate fields array if present
                if (block.ContainsKey("fields"))
                {
                    var fields = block["fields"] as JsonArray;
                    if (fields == null)
                    {
                        errors.Add(new ValidationError(0, 0,
                            $"Block {i} fields must be an array", ValidationSeverity.Error)
                        {
                            ErrorCode = "INVALID_FIELDS_TYPE",
                            Layer = ValidationLayer.Schema
                        });
                    }
                    else
                    {
                        ValidateFields(fields, i, errors, jsonText);
                    }
                }
            }
        }

        private void ValidateFields(JsonArray fields, int blockIndex, List<ValidationError> errors, string jsonText)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                var field = fields[i] as JsonObject;
                if (field == null)
                {
                    errors.Add(new ValidationError(0, 0,
                        $"Block {blockIndex}, Field {i} must be an object", ValidationSeverity.Error)
                    {
                        ErrorCode = "INVALID_FIELD_TYPE",
                        Layer = ValidationLayer.Schema
                    });
                    continue;
                }

                // Check required field properties
                foreach (var propName in FieldRequiredProperties)
                {
                    if (!field.ContainsKey(propName))
                    {
                        errors.Add(new ValidationError(0, 0,
                            $"Block {blockIndex}, Field {i} missing required property '{propName}'", ValidationSeverity.Error)
                        {
                            ErrorCode = "MISSING_FIELD_PROP",
                            Layer = ValidationLayer.Schema
                        });
                    }
                }
            }
        }

        #endregion

        #region Layer 3: Format Rules Validation

        private void ValidateFormatRules(JsonObject root, List<ValidationError> errors, string jsonText)
        {
            // Validate blocks
            if (root.ContainsKey("blocks") && root["blocks"] is JsonArray blocks)
            {
                for (int i = 0; i < blocks.Count; i++)
                {
                    var block = blocks[i] as JsonObject;
                    if (block == null) continue;

                    ValidateBlockRules(block, i, errors, jsonText);
                }
            }
        }

        private void ValidateBlockRules(JsonObject block, int blockIndex, List<ValidationError> errors, string jsonText)
        {
            // Validate block type
            if (block.ContainsKey("type"))
            {
                var blockType = block["type"]?.ToString();
                if (!string.IsNullOrEmpty(blockType) && !ValidBlockTypes.Contains(blockType))
                {
                    errors.Add(new ValidationError(0, 0,
                        $"Block {blockIndex} has invalid type '{blockType}'. Valid types: {string.Join(", ", ValidBlockTypes)}",
                        ValidationSeverity.Error)
                    {
                        ErrorCode = "INVALID_BLOCK_TYPE_VALUE",
                        Layer = ValidationLayer.FormatRules
                    });
                }
            }

            // Validate fields if present
            if (block.ContainsKey("fields") && block["fields"] is JsonArray fields)
            {
                for (int i = 0; i < fields.Count; i++)
                {
                    var field = fields[i] as JsonObject;
                    if (field == null) continue;

                    ValidateFieldRules(field, blockIndex, i, errors, jsonText);
                }
            }

            // Validate conditional-specific rules
            if (block["type"]?.ToString() == "conditional")
            {
                if (!block.ContainsKey("condition"))
                {
                    errors.Add(new ValidationError(0, 0,
                        $"Block {blockIndex} is conditional but missing 'condition' property",
                        ValidationSeverity.Error)
                    {
                        ErrorCode = "MISSING_CONDITION",
                        Layer = ValidationLayer.FormatRules
                    });
                }
            }

            // Validate loop-specific rules
            if (block["type"]?.ToString() == "loop" || block["type"]?.ToString() == "repeating")
            {
                if (!block.ContainsKey("count"))
                {
                    errors.Add(new ValidationError(0, 0,
                        $"Block {blockIndex} is loop but missing 'count' property",
                        ValidationSeverity.Error)
                    {
                        ErrorCode = "MISSING_COUNT",
                        Layer = ValidationLayer.FormatRules
                    });
                }
            }

            // Validate union-specific rules
            if (block["type"]?.ToString() == "union")
            {
                if (!block.ContainsKey("unionCondition") && !block.ContainsKey("variants"))
                {
                    errors.Add(new ValidationError(0, 0,
                        $"Block {blockIndex} is union but missing 'unionCondition' or 'variants' property",
                        ValidationSeverity.Warning)
                    {
                        ErrorCode = "MISSING_UNION_STRUCTURE",
                        Layer = ValidationLayer.FormatRules
                    });
                }
            }

            // Validate color format (#RRGGBB) if present
            if (block.ContainsKey("color"))
            {
                var color = block["color"]?.ToString();
                if (!string.IsNullOrEmpty(color) && !Regex.IsMatch(color, @"^#[0-9A-Fa-f]{6}$"))
                {
                    errors.Add(new ValidationError(0, 0,
                        $"Block {blockIndex} has invalid color '{color}'. Must be #RRGGBB hex format (e.g., \"#FF6B6B\")",
                        ValidationSeverity.Warning)
                    {
                        ErrorCode = "INVALID_COLOR_FORMAT",
                        Layer = ValidationLayer.FormatRules
                    });
                }
            }

            // Validate opacity range [0.0 - 1.0] if present
            if (block.ContainsKey("opacity"))
            {
                if (double.TryParse(block["opacity"]?.ToString(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var opacity)
                    && (opacity < 0.0 || opacity > 1.0))
                {
                    errors.Add(new ValidationError(0, 0,
                        $"Block {blockIndex} has invalid opacity '{opacity}'. Must be in range [0.0, 1.0]",
                        ValidationSeverity.Warning)
                    {
                        ErrorCode = "INVALID_OPACITY_RANGE",
                        Layer = ValidationLayer.FormatRules
                    });
                }
            }
        }

        private void ValidateFieldRules(JsonObject field, int blockIndex, int fieldIndex, List<ValidationError> errors, string jsonText)
        {
            // Validate field type
            if (field.ContainsKey("type"))
            {
                var fieldType = field["type"]?.ToString();
                if (!string.IsNullOrEmpty(fieldType) && !ValidFieldTypes.Contains(fieldType))
                {
                    errors.Add(new ValidationError(0, 0,
                        $"Block {blockIndex}, Field {fieldIndex} has invalid type '{fieldType}'. Valid types: {string.Join(", ", ValidFieldTypes)}",
                        ValidationSeverity.Error)
                    {
                        ErrorCode = "INVALID_FIELD_TYPE_VALUE",
                        Layer = ValidationLayer.FormatRules
                    });
                }

                // String/bytes types should have length
                if ((fieldType == "string" || fieldType == "bytes" || fieldType == "ascii" || fieldType == "utf8" || fieldType == "utf16")
                    && !field.ContainsKey("length"))
                {
                    errors.Add(new ValidationError(0, 0,
                        $"Block {blockIndex}, Field {fieldIndex} type '{fieldType}' requires 'length' property",
                        ValidationSeverity.Warning)
                    {
                        ErrorCode = "MISSING_LENGTH",
                        Layer = ValidationLayer.FormatRules
                    });
                }
            }

            // Validate endianness if present
            if (field.ContainsKey("endianness"))
            {
                var endianness = field["endianness"]?.ToString();
                if (endianness != "little" && endianness != "big")
                {
                    errors.Add(new ValidationError(0, 0,
                        $"Block {blockIndex}, Field {fieldIndex} has invalid endianness '{endianness}'. Must be 'little' or 'big'",
                        ValidationSeverity.Error)
                    {
                        ErrorCode = "INVALID_ENDIANNESS",
                        Layer = ValidationLayer.FormatRules
                    });
                }
            }

            // Validate color format (#RRGGBB) if present
            if (field.ContainsKey("color"))
            {
                var color = field["color"]?.ToString();
                if (!string.IsNullOrEmpty(color) && !Regex.IsMatch(color, @"^#[0-9A-Fa-f]{6}$"))
                {
                    errors.Add(new ValidationError(0, 0,
                        $"Block {blockIndex}, Field {fieldIndex} has invalid color '{color}'. Must be #RRGGBB hex format",
                        ValidationSeverity.Warning)
                    {
                        ErrorCode = "INVALID_COLOR_FORMAT",
                        Layer = ValidationLayer.FormatRules
                    });
                }
            }

            // Validate opacity range [0.0 - 1.0] if present
            if (field.ContainsKey("opacity"))
            {
                if (double.TryParse(field["opacity"]?.ToString(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var opacity)
                    && (opacity < 0.0 || opacity > 1.0))
                {
                    errors.Add(new ValidationError(0, 0,
                        $"Block {blockIndex}, Field {fieldIndex} has invalid opacity '{opacity}'. Must be in range [0.0, 1.0]",
                        ValidationSeverity.Warning)
                    {
                        ErrorCode = "INVALID_OPACITY_RANGE",
                        Layer = ValidationLayer.FormatRules
                    });
                }
            }

            // Validate signature value is a valid hex string
            if (field.ContainsKey("value"))
            {
                var sigValue = field["value"]?.ToString();
                if (!string.IsNullOrEmpty(sigValue) && !Regex.IsMatch(sigValue, @"^[0-9A-Fa-f\s]+$"))
                {
                    errors.Add(new ValidationError(0, 0,
                        $"Block {blockIndex}, Field {fieldIndex} signature 'value' is not a valid hex string (found '{sigValue}'). Use uppercase hex pairs (e.g., \"504B0304\")",
                        ValidationSeverity.Warning)
                    {
                        ErrorCode = "INVALID_SIGNATURE_HEX",
                        Layer = ValidationLayer.FormatRules
                    });
                }
            }
        }

        #endregion

        #region Layer 4: Semantic Validation

        private void ValidateSemantics(JsonObject root, List<ValidationError> errors, string jsonText)
        {
            // Collect all variable names (varName properties)
            var declaredVariables = new HashSet<string>();
            CollectVariableNames(root, declaredVariables);

            // Collect variables declared in the top-level "variables" object
            if (root["variables"] is JsonObject vars)
            {
                foreach (var kv in vars)
                    declaredVariables.Add(kv.Key);
            }

            // Validate variable references (var:...)
            ValidateVariableReferences(root, declaredVariables, errors, jsonText);

            // Validate calc expressions (calc:...)
            ValidateCalcExpressions(root, declaredVariables, errors, jsonText);

            // Validate whfmt block variable cross-references (storeAs/offsetFrom/targetVar/actionVariable/indexVar)
            ValidateBlockVariableCrossReferences(root, declaredVariables, errors);
        }

        private static readonly string[] VariableRefFields = { "storeAs", "offsetFrom", "targetVar", "actionVariable", "indexVar", "mappedValueStoreAs" };

        private void ValidateBlockVariableCrossReferences(JsonObject root, HashSet<string> declared, List<ValidationError> errors)
        {
            if (root["blocks"] is not JsonArray blocks) return;

            foreach (var node in blocks)
            {
                if (node is not JsonObject block) continue;
                var blockName = block["name"]?.ToString() ?? "(unnamed)";

                foreach (var field in VariableRefFields)
                {
                    var value = block[field]?.ToString();
                    if (string.IsNullOrEmpty(value)) continue;
                    if (!declared.Contains(value))
                    {
                        errors.Add(new ValidationError(0, 0,
                            $"Variable '{value}' referenced in block '{blockName}' ({field}) is not declared in variables.",
                            ValidationSeverity.Warning)
                        {
                            ErrorCode = "UNDEFINED_VAR_REF",
                            Layer     = ValidationLayer.Semantic
                        });
                    }
                }
            }
        }

        private void CollectVariableNames(JsonNode token, HashSet<string> variables)
        {
            if (token is JsonObject obj)
            {
                // varName and storeAs both declare a variable for later var: references
                foreach (var declaringProp in new[] { "varName", "storeAs", "mappedValueStoreAs", "indexVar" })
                {
                    var varName = obj[declaringProp]?.ToString();
                    if (!string.IsNullOrEmpty(varName))
                        variables.Add(varName);
                }

                foreach (var prop in obj)
                {
                    if (prop.Value != null)
                        CollectVariableNames(prop.Value, variables);
                }
            }
            else if (token is JsonArray array)
            {
                foreach (var item in array)
                {
                    if (item != null)
                        CollectVariableNames(item, variables);
                }
            }
        }

        private void ValidateVariableReferences(JsonNode token, HashSet<string> declaredVariables, List<ValidationError> errors, string jsonText)
        {
            if (token is JsonObject obj)
            {
                foreach (var prop in obj)
                {
                    var value = prop.Value?.ToString();
                    if (!string.IsNullOrEmpty(value) && value.StartsWith("var:"))
                    {
                        var varName = value.Substring(4).Trim();
                        if (!declaredVariables.Contains(varName))
                        {
                            errors.Add(new ValidationError(0, 0,
                                $"Variable reference 'var:{varName}' not found. Variable must be declared with varName property before use.",
                                ValidationSeverity.Error)
                            {
                                ErrorCode = "UNDEFINED_VARIABLE",
                                Layer = ValidationLayer.Semantic
                            });
                        }
                    }

                    if (prop.Value != null)
                        ValidateVariableReferences(prop.Value, declaredVariables, errors, jsonText);
                }
            }
            else if (token is JsonArray array)
            {
                foreach (var item in array)
                {
                    if (item != null)
                        ValidateVariableReferences(item, declaredVariables, errors, jsonText);
                }
            }
        }

        private void ValidateCalcExpressions(JsonNode token, HashSet<string> declaredVariables, List<ValidationError> errors, string jsonText)
        {
            if (token is JsonObject obj)
            {
                foreach (var prop in obj)
                {
                    var value = prop.Value?.ToString();
                    if (!string.IsNullOrEmpty(value) && value.StartsWith("calc:"))
                    {
                        var expression = value.Substring(5).Trim();
                        ValidateSingleCalcExpression(expression, declaredVariables, errors);
                    }

                    if (prop.Value != null)
                        ValidateCalcExpressions(prop.Value, declaredVariables, errors, jsonText);
                }
            }
            else if (token is JsonArray array)
            {
                foreach (var item in array)
                {
                    if (item != null)
                        ValidateCalcExpressions(item, declaredVariables, errors, jsonText);
                }
            }
        }

        private void ValidateSingleCalcExpression(string expression, HashSet<string> declaredVariables, List<ValidationError> errors)
        {
            // Find variable references in calc expression
            var varMatches = Regex.Matches(expression, @"\b([a-zA-Z_][a-zA-Z0-9_]*)\b");
            foreach (Match match in varMatches)
            {
                var varName = match.Groups[1].Value;

                // Skip operators and numbers
                if (varName == "and" || varName == "or" || varName == "not" ||
                    varName == "if" || varName == "else" || varName == "then")
                    continue;

                // Check if it's a declared variable
                if (!declaredVariables.Contains(varName) && !char.IsDigit(varName[0]))
                {
                    errors.Add(new ValidationError(0, 0,
                        $"calc expression references undefined variable '{varName}'",
                        ValidationSeverity.Warning)
                    {
                        ErrorCode = "CALC_UNDEFINED_VAR",
                        Layer = ValidationLayer.Semantic
                    });
                }
            }

            // Basic syntax check for calc expressions
            if (expression.Contains("//") || expression.Contains("/*"))
            {
                errors.Add(new ValidationError(0, 0,
                    "calc expression contains invalid comment syntax",
                    ValidationSeverity.Error)
                {
                    ErrorCode = "CALC_SYNTAX_ERROR",
                    Layer = ValidationLayer.Semantic
                });
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Find line and column of a property in JSON text (approximate)
        /// </summary>
        private (int Line, int Column) FindPropertyLocation(string jsonText, string propertyName)
        {
            var lines = jsonText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                var index = lines[i].IndexOf($"\"{propertyName}\"");
                if (index >= 0)
                {
                    return (i, index);
                }
            }

            return (0, 0);
        }

        #endregion
    }
}

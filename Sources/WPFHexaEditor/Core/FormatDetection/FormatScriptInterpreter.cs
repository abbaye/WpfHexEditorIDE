//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using WpfHexaEditor.Core.FormatDetection;
using WpfHexaEditor.Core.Bytes;

namespace WpfHexaEditor.Core.FormatDetection
{
    /// <summary>
    /// Interpreter for executing format detection scripts
    /// Processes BlockDefinition lists and generates CustomBackgroundBlocks
    /// </summary>
    public class FormatScriptInterpreter
    {
        private readonly byte[] _data;
        private readonly Dictionary<string, object> _variables;
        private readonly List<CustomBackgroundBlock> _generatedBlocks;
        private readonly ByteProvider _byteProvider;
        private int _executionDepth = 0;
        private const int MaxExecutionDepth = 50; // Prevent infinite recursion

        /// <summary>
        /// Get the variables dictionary (includes values set by functions)
        /// </summary>
        public Dictionary<string, object> Variables => _variables;

        /// <summary>
        /// Initialize interpreter with file data
        /// </summary>
        public FormatScriptInterpreter(byte[] data, Dictionary<string, object> initialVariables = null, ByteProvider byteProvider = null)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _variables = initialVariables != null
                ? new Dictionary<string, object>(initialVariables)
                : new Dictionary<string, object>();
            _generatedBlocks = new List<CustomBackgroundBlock>();
            _byteProvider = byteProvider; // Optional - for reading beyond sample buffer

            // Initialize common variables if not provided
            if (!_variables.ContainsKey("currentOffset"))
                _variables["currentOffset"] = 0L;
            if (!_variables.ContainsKey("fileCount"))
                _variables["fileCount"] = 0;
            if (!_variables.ContainsKey("fileSize"))
                _variables["fileSize"] = byteProvider != null && byteProvider.IsOpen ? byteProvider.VirtualLength : (long)_data.Length;
        }

        /// <summary>
        /// Execute built-in functions from format definition.
        /// Functions can analyze file content and populate variables for use in blocks.
        /// </summary>
        public void ExecuteFunctions(Dictionary<string, string> functions)
        {
            if (functions == null || functions.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[ExecuteFunctions] No functions to execute");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[ExecuteFunctions] Executing {functions.Count} functions");

            var builtInFunctions = new BuiltInFunctions(_data, _variables, _byteProvider);

            foreach (var function in functions)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"  Executing function: {function.Key}");

                    // Parse function name and parameters
                    // Supports: "functionName" or "functionName(arg1, arg2, ...)"
                    string functionName;
                    object[] args = Array.Empty<object>();

                    int parenIndex = function.Key.IndexOf('(');
                    if (parenIndex > 0 && function.Key.EndsWith(")"))
                    {
                        // Has parameters
                        functionName = function.Key.Substring(0, parenIndex).Trim();
                        string paramsStr = function.Key.Substring(parenIndex + 1, function.Key.Length - parenIndex - 2);

                        if (!string.IsNullOrWhiteSpace(paramsStr))
                        {
                            var paramTokens = paramsStr.Split(',');
                            var argsList = new List<object>();

                            foreach (var token in paramTokens)
                            {
                                string param = token.Trim();

                                // Variable reference: "var:name"
                                if (param.StartsWith("var:"))
                                {
                                    string varName = param.Substring(4);
                                    if (_variables.TryGetValue(varName, out var value))
                                    {
                                        argsList.Add(value);
                                    }
                                    else
                                    {
                                        argsList.Add(0);
                                    }
                                }
                                // Literal number
                                else if (long.TryParse(param, out long numValue))
                                {
                                    argsList.Add(numValue);
                                }
                                // Hex literal: "0x1234"
                                else if (param.StartsWith("0x") && long.TryParse(param.Substring(2),
                                    System.Globalization.NumberStyles.HexNumber, null, out long hexValue))
                                {
                                    argsList.Add(hexValue);
                                }
                                else
                                {
                                    argsList.Add(param); // String literal
                                }
                            }

                            args = argsList.ToArray();
                        }
                    }
                    else
                    {
                        // No parameters
                        functionName = function.Key.Trim();
                    }

                    builtInFunctions.Execute(functionName, args);
                    System.Diagnostics.Debug.WriteLine($"  Function {functionName} completed");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error executing function '{function.Key}': {ex.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"[ExecuteFunctions] Variables after execution: {_variables.Count}");
            foreach (var v in _variables)
            {
                System.Diagnostics.Debug.WriteLine($"  {v.Key} = {v.Value}");
            }
        }

        /// <summary>
        /// Execute block definitions and return generated blocks
        /// </summary>
        public List<CustomBackgroundBlock> ExecuteBlocks(List<BlockDefinition> blocks)
        {
            if (blocks == null || blocks.Count == 0)
                return _generatedBlocks;

            _executionDepth++;
            if (_executionDepth > MaxExecutionDepth)
            {
                throw new InvalidOperationException($"Maximum execution depth ({MaxExecutionDepth}) exceeded. Possible infinite loop in format definition.");
            }

            try
            {
                foreach (var block in blocks)
                {
                    ExecuteBlock(block);
                }
            }
            finally
            {
                _executionDepth--;
            }

            return _generatedBlocks;
        }

        #region Block Execution

        /// <summary>
        /// Execute a single block definition
        /// </summary>
        private void ExecuteBlock(BlockDefinition block)
        {
            if (block == null || !block.IsValid())
                return;

            switch (block.Type.ToLowerInvariant())
            {
                case "signature":
                case "field":
                    ExecuteFieldBlock(block);
                    break;

                case "metadata":
                    ExecuteMetadataBlock(block);
                    break;

                case "conditional":
                    ExecuteConditionalBlock(block);
                    break;

                case "loop":
                    ExecuteLoopBlock(block);
                    break;

                case "action":
                    ExecuteActionBlock(block);
                    break;

                case "computefromvariables":
                    ExecuteComputeFromVariablesBlock(block);
                    break;

                default:
                    // Unknown block type, skip
                    break;
            }
        }

        /// <summary>
        /// Execute a field/signature block (creates a CustomBackgroundBlock)
        /// Also reads and stores field value when StoreAs and ValueType are defined.
        /// </summary>
        private void ExecuteFieldBlock(BlockDefinition block)
        {
            // Resolve offset
            var offset = EvaluateOffset(block.Offset);
            if (offset == null || offset < 0 || offset >= _data.Length)
                return; // Invalid offset

            // Resolve length
            var length = EvaluateLength(block.Length);
            if (length == null || length <= 0)
                return; // Invalid length

            // Extract and store value if StoreAs is defined
            if (!string.IsNullOrWhiteSpace(block.StoreAs) && !string.IsNullOrWhiteSpace(block.ValueType))
            {
                var extractedValue = ReadFieldValue(offset.Value, length.Value, block.ValueType, block.Endianness);
                if (extractedValue != null)
                {
                    _variables[block.StoreAs] = extractedValue;

                    // Process bitfield extractions
                    if (block.Bitfields != null)
                    {
                        try
                        {
                            long rawVal = Convert.ToInt64(extractedValue);
                            foreach (var bf in block.Bitfields)
                            {
                                long bfValue = bf.ExtractValue(rawVal);
                                if (!string.IsNullOrWhiteSpace(bf.StoreAs))
                                    _variables[bf.StoreAs] = bfValue;

                                // Apply bitfield-level valueMap
                                if (bf.ValueMap != null && bf.ValueMap.TryGetValue(bfValue.ToString(), out string bfMapped))
                                {
                                    if (!string.IsNullOrWhiteSpace(bf.StoreAs))
                                        _variables[bf.StoreAs + "Name"] = bfMapped;
                                }
                            }
                        }
                        catch { /* Bitfield extraction failed, continue */ }
                    }

                    // Apply field-level valueMap
                    if (block.ValueMap != null)
                    {
                        string key = extractedValue.ToString();
                        if (block.ValueMap.TryGetValue(key, out string mappedName))
                        {
                            if (!string.IsNullOrWhiteSpace(block.MappedValueStoreAs))
                                _variables[block.MappedValueStoreAs] = mappedName;
                        }
                    }
                }
            }

            // Parse color
            var brush = ParseColor(block.Color);
            if (brush == null)
                brush = Brushes.Gray; // Fallback

            // Build description - include extracted value when available
            string description = block.Description ?? block.Name ?? "Unknown";
            if (!string.IsNullOrWhiteSpace(block.StoreAs) && _variables.TryGetValue(block.StoreAs, out var storedVal))
            {
                description = $"{block.Name}: {storedVal}";
                if (!string.IsNullOrWhiteSpace(block.Description))
                    description += $" ({block.Description})";
            }

            // Create block
            var customBlock = new CustomBackgroundBlock(
                offset.Value,
                length.Value,
                brush,
                description,
                block.Opacity);

            _generatedBlocks.Add(customBlock);
        }

        /// <summary>
        /// Read a typed value from file data at the given offset.
        /// Supports: uint8, uint16, uint32, uint64, int8, int16, int32, int64, ascii, utf8, bytes
        /// Endianness: "big" or "little" (default: little)
        /// </summary>
        private object ReadFieldValue(long offset, int length, string valueType, string endianness)
        {
            if (offset < 0 || offset >= _data.Length)
                return null;

            bool bigEndian = string.Equals(endianness, "big", System.StringComparison.OrdinalIgnoreCase);

            try
            {
                switch (valueType.ToLowerInvariant())
                {
                    case "uint8":
                    case "byte":
                        if (offset < _data.Length)
                            return (int)_data[offset];
                        return 0;

                    case "uint16":
                        if (offset + 2 > _data.Length) return 0;
                        if (bigEndian)
                            return (int)((_data[offset] << 8) | _data[offset + 1]);
                        return (int)(_data[offset] | (_data[offset + 1] << 8));

                    case "uint32":
                        if (offset + 4 > _data.Length) return 0L;
                        if (bigEndian)
                            return (long)(((uint)_data[offset] << 24) | ((uint)_data[offset + 1] << 16) |
                                         ((uint)_data[offset + 2] << 8) | _data[offset + 3]);
                        return (long)((uint)_data[offset] | ((uint)_data[offset + 1] << 8) |
                                     ((uint)_data[offset + 2] << 16) | ((uint)_data[offset + 3] << 24));

                    case "uint64":
                        if (offset + 8 > _data.Length) return 0L;
                        if (bigEndian)
                        {
                            return (long)(((ulong)_data[offset] << 56) | ((ulong)_data[offset + 1] << 48) |
                                         ((ulong)_data[offset + 2] << 40) | ((ulong)_data[offset + 3] << 32) |
                                         ((ulong)_data[offset + 4] << 24) | ((ulong)_data[offset + 5] << 16) |
                                         ((ulong)_data[offset + 6] << 8) | (ulong)_data[offset + 7]);
                        }
                        return (long)((ulong)_data[offset] | ((ulong)_data[offset + 1] << 8) |
                                     ((ulong)_data[offset + 2] << 16) | ((ulong)_data[offset + 3] << 24) |
                                     ((ulong)_data[offset + 4] << 32) | ((ulong)_data[offset + 5] << 40) |
                                     ((ulong)_data[offset + 6] << 48) | ((ulong)_data[offset + 7] << 56));

                    case "int8":
                    case "sbyte":
                        if (offset < _data.Length)
                            return (int)(sbyte)_data[offset];
                        return 0;

                    case "int16":
                        if (offset + 2 > _data.Length) return 0;
                        if (bigEndian)
                            return (int)(short)((_data[offset] << 8) | _data[offset + 1]);
                        return (int)(short)(_data[offset] | (_data[offset + 1] << 8));

                    case "int32":
                        if (offset + 4 > _data.Length) return 0;
                        if (bigEndian)
                            return (int)((_data[offset] << 24) | (_data[offset + 1] << 16) |
                                        (_data[offset + 2] << 8) | _data[offset + 3]);
                        return (int)(_data[offset] | (_data[offset + 1] << 8) |
                                    (_data[offset + 2] << 16) | (_data[offset + 3] << 24));

                    case "ascii":
                    case "string":
                    {
                        int actualLen = (int)System.Math.Min(length, _data.Length - offset);
                        if (actualLen <= 0) return string.Empty;
                        // Read ASCII, stop at null terminator
                        var sb = new System.Text.StringBuilder(actualLen);
                        for (int i = 0; i < actualLen; i++)
                        {
                            byte b = _data[offset + i];
                            if (b == 0) break;
                            sb.Append(b >= 32 && b <= 126 ? (char)b : '?');
                        }
                        return sb.ToString().TrimEnd();
                    }

                    case "utf8":
                    {
                        int actualLen = (int)System.Math.Min(length, _data.Length - offset);
                        if (actualLen <= 0) return string.Empty;
                        // Find null terminator
                        int strLen = actualLen;
                        for (int i = 0; i < actualLen; i++)
                        {
                            if (_data[offset + i] == 0) { strLen = i; break; }
                        }
                        byte[] buffer = new byte[strLen];
                        System.Array.Copy(_data, offset, buffer, 0, strLen);
                        return System.Text.Encoding.UTF8.GetString(buffer).TrimEnd();
                    }

                    case "bytes":
                    case "raw":
                    {
                        int actualLen = (int)System.Math.Min(length, _data.Length - offset);
                        if (actualLen <= 0) return string.Empty;
                        byte[] bytes = new byte[actualLen];
                        System.Array.Copy(_data, offset, bytes, 0, actualLen);
                        return System.BitConverter.ToString(bytes).Replace("-", " ");
                    }

                    default:
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Execute a metadata block (displays variable value without highlighting bytes)
        /// </summary>
        private void ExecuteMetadataBlock(BlockDefinition block)
        {
            // Get variable value
            if (string.IsNullOrWhiteSpace(block.Variable))
                return;

            if (!_variables.TryGetValue(block.Variable, out var value))
                return; // Variable doesn't exist

            // Create a block with minimal footprint (offset=0, length=1)
            // The key info is in the Description which includes the variable value
            var description = $"{block.Name}: {value}";
            if (!string.IsNullOrWhiteSpace(block.Description))
                description += $" ({block.Description})";

            var customBlock = new CustomBackgroundBlock(
                0, // Metadata doesn't correspond to a specific offset
                1, // Minimal length to pass validation
                System.Windows.Media.Brushes.Transparent, // Invisible in hex editor
                description,
                0.0); // Fully transparent

            _generatedBlocks.Add(customBlock);
        }

        /// <summary>
        /// Execute a conditional block
        /// </summary>
        private void ExecuteConditionalBlock(BlockDefinition block)
        {
            if (block.Condition == null)
                return;

            bool conditionMet = EvaluateCondition(block.Condition);

            if (conditionMet && block.Then != null)
            {
                ExecuteBlocks(block.Then);
            }
            else if (!conditionMet && block.Else != null)
            {
                ExecuteBlocks(block.Else);
            }
        }

        /// <summary>
        /// Execute a loop block
        /// </summary>
        private void ExecuteLoopBlock(BlockDefinition block)
        {
            if (block.Condition == null || block.Body == null)
                return;

            int iteration = 0;
            int maxIterations = Math.Min(block.MaxIterations, 100000); // Safety limit

            // Store original variable for $iteration$ replacement
            var originalIteration = _variables.ContainsKey("iteration") ? _variables["iteration"] : null;

            while (iteration < maxIterations)
            {
                // Check condition
                bool continueLoop = EvaluateCondition(block.Condition);
                if (!continueLoop)
                    break;

                // Set iteration variable
                _variables["iteration"] = iteration;

                // Execute body
                ExecuteBlocks(block.Body);

                iteration++;
            }

            // Restore original iteration variable
            if (originalIteration != null)
                _variables["iteration"] = originalIteration;
            else
                _variables.Remove("iteration");
        }

        /// <summary>
        /// Execute an action block (variable manipulation)
        /// </summary>
        private void ExecuteActionBlock(BlockDefinition block)
        {
            if (string.IsNullOrWhiteSpace(block.Action) || string.IsNullOrWhiteSpace(block.Variable))
                return;

            switch (block.Action.ToLowerInvariant())
            {
                case "increment":
                    if (_variables.TryGetValue(block.Variable, out var incValue))
                    {
                        _variables[block.Variable] = Convert.ToInt64(incValue) + 1;
                    }
                    break;

                case "decrement":
                    if (_variables.TryGetValue(block.Variable, out var decValue))
                    {
                        _variables[block.Variable] = Convert.ToInt64(decValue) - 1;
                    }
                    break;

                case "setvariable":
                    var value = EvaluateValue(block.Value);
                    if (value != null)
                    {
                        _variables[block.Variable] = value;
                    }
                    break;
            }
        }

        /// <summary>
        /// Execute a computeFromVariables block - evaluates an expression and stores the result
        /// </summary>
        private void ExecuteComputeFromVariablesBlock(BlockDefinition block)
        {
            if (string.IsNullOrWhiteSpace(block.Expression) || string.IsNullOrWhiteSpace(block.StoreAs))
                return;

            try
            {
                var result = EvaluateExpression(block.Expression);
                _variables[block.StoreAs] = result;
            }
            catch
            {
                // Expression evaluation failed, set default value
                _variables[block.StoreAs] = 0L;
            }
        }

        #endregion

        #region Evaluation Methods

        /// <summary>
        /// Evaluate offset expression (supports int, var:name, calc:expression)
        /// </summary>
        private long? EvaluateOffset(object offsetExpr)
        {
            if (offsetExpr == null)
                return null;

            // Handle JsonElement (from System.Text.Json deserialization)
            if (offsetExpr is System.Text.Json.JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    if (jsonElement.TryGetInt64(out long jsonValue))
                        return jsonValue;
                }
                else if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    // Convert to string and process as string expression
                    offsetExpr = jsonElement.GetString();
                }
            }

            // Direct integer
            if (offsetExpr is int intOffset)
                return intOffset;

            if (offsetExpr is long longOffset)
                return longOffset;

            // String expression
            if (offsetExpr is string strOffset)
            {
                if (strOffset.StartsWith("var:"))
                {
                    var varName = strOffset.Substring(4);
                    if (_variables.TryGetValue(varName, out var value))
                    {
                        return Convert.ToInt64(value);
                    }
                }
                else if (strOffset.StartsWith("calc:"))
                {
                    var expression = strOffset.Substring(5);
                    return EvaluateExpression(expression);
                }
                else
                {
                    // Try parse as number
                    if (long.TryParse(strOffset, out var parsed))
                        return parsed;
                }
            }

            return null;
        }

        /// <summary>
        /// Evaluate length expression
        /// </summary>
        private int? EvaluateLength(object lengthExpr)
        {
            var offset = EvaluateOffset(lengthExpr);
            return offset.HasValue ? (int)offset.Value : null;
        }

        /// <summary>
        /// Evaluate generic value (for action blocks)
        /// </summary>
        private object EvaluateValue(object valueExpr)
        {
            if (valueExpr == null)
                return null;

            // Direct value
            if (valueExpr is int || valueExpr is long || valueExpr is double)
                return valueExpr;

            // String expression
            if (valueExpr is string strValue)
            {
                if (strValue.StartsWith("var:"))
                {
                    var varName = strValue.Substring(4);
                    if (_variables.TryGetValue(varName, out var value))
                    {
                        return value;
                    }
                }
                else if (strValue.StartsWith("calc:"))
                {
                    var expression = strValue.Substring(5);
                    return EvaluateExpression(expression);
                }
                else
                {
                    // Try parse as number
                    if (long.TryParse(strValue, out var parsed))
                        return parsed;
                }
            }

            return valueExpr;
        }

        /// <summary>
        /// Evaluate condition
        /// </summary>
        private bool EvaluateCondition(ConditionDefinition condition)
        {
            if (condition == null)
                return false;

            // Handle expression-based conditions (from string format like "(var & 1) == 1")
            if (condition.Operator == "expression" && condition.Field?.StartsWith("expr:") == true)
            {
                return EvaluateBooleanExpression(condition.Value);
            }

            // Get field value
            long? fieldValue = null;

            if (condition.Field.StartsWith("offset:"))
            {
                var offsetStr = condition.Field.Substring(7);
                if (long.TryParse(offsetStr, out var offset))
                {
                    fieldValue = ReadInteger(offset, condition.Length);
                }
            }
            else if (condition.Field.StartsWith("var:"))
            {
                var varName = condition.Field.Substring(4);
                if (_variables.TryGetValue(varName, out var value))
                {
                    fieldValue = Convert.ToInt64(value);
                }
            }

            if (!fieldValue.HasValue)
                return false;

            // Get comparison value
            long compareValue = 0;
            if (condition.Value.StartsWith("0x") || condition.Value.StartsWith("0X"))
            {
                // Hex value
                if (long.TryParse(condition.Value.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out var hexValue))
                {
                    compareValue = hexValue;
                }
            }
            else
            {
                // Decimal value
                if (long.TryParse(condition.Value, out var decValue))
                {
                    compareValue = decValue;
                }
            }

            // Compare
            switch (condition.Operator?.ToLowerInvariant())
            {
                case "equals":
                    return fieldValue.Value == compareValue;
                case "notequals":
                    return fieldValue.Value != compareValue;
                case "greaterthan":
                    return fieldValue.Value > compareValue;
                case "lessthan":
                    return fieldValue.Value < compareValue;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Evaluate arithmetic expression using the proper recursive descent parser
        /// with correct operator precedence (* / before + -) and parentheses support.
        /// Delegates to ExpressionEvaluator.EvaluateStatic.
        /// </summary>
        private long EvaluateExpression(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return 0;

            return ExpressionEvaluator.EvaluateStatic(expression, _variables);
        }

        /// <summary>
        /// Evaluate a boolean expression like "(generalPurposeFlags &amp; 1) == 1"
        /// Supports comparison operators: ==, !=, &gt;, &lt;, &gt;=, &lt;=
        /// Each side of the comparison is evaluated as an arithmetic expression.
        /// </summary>
        private bool EvaluateBooleanExpression(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return false;

            try
            {
                // Try to find comparison operator
                string[] compOps = { "==", "!=", ">=", "<=", ">", "<" };
                foreach (var op in compOps)
                {
                    int idx = expression.IndexOf(op);
                    if (idx > 0)
                    {
                        var left = expression.Substring(0, idx).Trim();
                        var right = expression.Substring(idx + op.Length).Trim();

                        long leftVal = ExpressionEvaluator.EvaluateStatic(left, _variables);
                        long rightVal = ExpressionEvaluator.EvaluateStatic(right, _variables);

                        switch (op)
                        {
                            case "==": return leftVal == rightVal;
                            case "!=": return leftVal != rightVal;
                            case ">=": return leftVal >= rightVal;
                            case "<=": return leftVal <= rightVal;
                            case ">": return leftVal > rightVal;
                            case "<": return leftVal < rightVal;
                        }
                    }
                }

                // No comparison operator found - evaluate as numeric (non-zero = true)
                return ExpressionEvaluator.EvaluateStatic(expression, _variables) != 0;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Data Reading Methods

        /// <summary>
        /// Read integer from data (little-endian)
        /// </summary>
        private long ReadInteger(long offset, int length)
        {
            if (offset < 0 || offset >= _data.Length)
                return 0;

            long value = 0;
            int bytesToRead = Math.Min(length, (int)(_data.Length - offset));
            bytesToRead = Math.Min(bytesToRead, 8); // Max 8 bytes for long

            for (int i = 0; i < bytesToRead; i++)
            {
                value |= ((long)_data[offset + i]) << (i * 8);
            }

            return value;
        }

        /// <summary>
        /// Read uint16 (2 bytes, little-endian)
        /// </summary>
        public ushort ReadUInt16(long offset)
        {
            if (offset < 0 || offset + 1 >= _data.Length)
                return 0;

            return (ushort)(_data[offset] | (_data[offset + 1] << 8));
        }

        /// <summary>
        /// Read uint32 (4 bytes, little-endian)
        /// </summary>
        public uint ReadUInt32(long offset)
        {
            if (offset < 0 || offset + 3 >= _data.Length)
                return 0;

            return (uint)(_data[offset] |
                          (_data[offset + 1] << 8) |
                          (_data[offset + 2] << 16) |
                          (_data[offset + 3] << 24));
        }

        /// <summary>
        /// Read string (ASCII)
        /// </summary>
        public string ReadString(long offset, int length)
        {
            if (offset < 0 || offset >= _data.Length)
                return "";

            int bytesToRead = Math.Min(length, (int)(_data.Length - offset));
            var bytes = new byte[bytesToRead];
            Array.Copy(_data, offset, bytes, 0, bytesToRead);

            return System.Text.Encoding.ASCII.GetString(bytes);
        }

        /// <summary>
        /// Check signature at offset
        /// </summary>
        public bool CheckSignature(long offset, string hexSignature)
        {
            if (string.IsNullOrWhiteSpace(hexSignature))
                return false;

            var bytes = HexStringToBytes(hexSignature);
            if (bytes == null || offset + bytes.Length > _data.Length)
                return false;

            for (int i = 0; i < bytes.Length; i++)
            {
                if (_data[offset + i] != bytes[i])
                    return false;
            }

            return true;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Parse color from hex string (#RRGGBB)
        /// </summary>
        private SolidColorBrush ParseColor(string colorStr)
        {
            if (string.IsNullOrWhiteSpace(colorStr))
                return null;

            try
            {
                var color = (Color)ColorConverter.ConvertFromString(colorStr);
                return new SolidColorBrush(color);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Convert hex string to byte array
        /// </summary>
        private byte[] HexStringToBytes(string hex)
        {
            if (hex.Length % 2 != 0)
                return null;

            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }

        #endregion
    }
}

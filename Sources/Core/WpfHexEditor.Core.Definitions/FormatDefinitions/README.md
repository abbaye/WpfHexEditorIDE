# Format Definitions - Template-Based Data Structure Visualization

This directory contains JSON-based format definitions for automatic file structure parsing and visualization in WPFHexaEditor. The system provides 010 Editor-style template functionality with dynamic parsing, variables, conditionals, and loops.

## 📋 Table of Contents

- [Quick Start](#quick-start)
- [Format Definition Structure](#format-definition-structure)
- [Block Types](#block-types)
- [Advanced Features](#advanced-features)
- [Examples](#examples)
- [Best Practices](#best-practices)

## 🚀 Quick Start

### Basic Format Definition

```json
{
  "formatName": "Simple Binary Format",
  "version": "1.0",
  "extensions": [".bin"],
  "description": "Example binary format",

  "detection": {
    "signature": "42494E00",
    "offset": 0,
    "required": true
  },

  "blocks": [
    {
      "type": "signature",
      "name": "Magic",
      "offset": 0,
      "length": 4,
      "color": "#FF6B6B",
      "description": "File signature"
    },
    {
      "type": "field",
      "name": "Version",
      "offset": 4,
      "length": 2,
      "valueType": "uint16",
      "color": "#4ECDC4",
      "description": "Format version"
    }
  ]
}
```

## 📐 Format Definition Structure

### Root Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `formatName` | string | ✅ | Display name of the format |
| `version` | string | ✅ | Format definition version |
| `extensions` | string[] | ✅ | File extensions (e.g., `[".png", ".jpg"]`) |
| `description` | string | ❌ | Format description |
| `author` | string | ❌ | Author name |
| `detection` | object | ✅ | Detection rules |
| `variables` | object | ❌ | Pre-defined variables |
| `blocks` | array | ✅ | Field/structure definitions |

### Detection Object

```json
"detection": {
  "signature": "89504E470D0A1A0A",  // Magic bytes (hex)
  "offset": 0,                       // Offset in file
  "required": true                   // Must match?
}
```

## 🧱 Block Types

### 1. Field Block

Standard data field with type information.

```json
{
  "type": "field",
  "name": "Image Width",
  "offset": 16,
  "length": 4,
  "valueType": "uint32",
  "color": "#FFE66D",
  "opacity": 0.3,
  "description": "Image width in pixels",
  "storeAs": "width",              // Save as variable
  "hidden": false,                  // Show in UI?
  "validationRules": {              // Optional validation
    "minValue": 1,
    "maxValue": 65535
  }
}
```

**Value Types:**
- Integers: `uint8`, `uint16`, `uint32`, `uint64`, `int8`, `int16`, `int32`, `int64`
- Strings: `string`, `ascii`, `utf8`, `utf16`
- Raw: `bytes`

### 2. Signature Block

Special field for file signatures/magic bytes.

```json
{
  "type": "signature",
  "name": "PNG Signature",
  "offset": 0,
  "length": 8,
  "color": "#FF6B6B",
  "description": "PNG magic number"
}
```

### 3. Conditional Block

Execute blocks based on conditions.

```json
{
  "type": "conditional",
  "condition": {
    "field": "offset:6",           // Read from offset 6
    "operator": "equals",           // equals, notEquals, greaterThan, lessThan
    "value": "0x0008",
    "length": 2
  },
  "then": [
    { /* blocks if true */ }
  ],
  "else": [
    { /* blocks if false */ }
  ]
}
```

**Simple String Conditions:**
```json
{
  "type": "conditional",
  "condition": "width > 0",        // Simple expression
  "then": [ /* ... */ ]
}
```

### 4. Loop Block

Repeat blocks multiple times.

```json
{
  "type": "loop",
  "count": 10,                      // Can be int, "var:count", or "calc:expr"
  "body": [
    {
      "type": "field",
      "name": "Entry",
      "offset": "calc:base + (i * 16)",  // Use 'i' or 'index'
      "length": 16,
      "valueType": "bytes",
      "color": "#95E1D3"
    }
  ]
}
```

### 5. Action Block

Perform variable operations.

```json
{
  "type": "action",
  "action": "setVariable",          // setVariable, increment, decrement
  "variable": "currentOffset",
  "value": "calc:currentOffset + size"
}
```

## 🔧 Advanced Features

### Variables

#### Defining Variables

```json
{
  "variables": {
    "headerSize": 64,
    "version": 1
  }
}
```

#### Storing Field Values

```json
{
  "type": "field",
  "name": "Chunk Count",
  "offset": 8,
  "length": 4,
  "valueType": "uint32",
  "storeAs": "chunkCount",          // Store for later use
  "color": "#4ECDC4"
}
```

#### Using Variables

- **Offset**: `"offset": "var:headerSize"`
- **Length**: `"length": "var:chunkSize"`
- **Loop Count**: `"count": "var:chunkCount"`
- **Calculations**: `"offset": "calc:headerSize + 16"`

### Expressions

The `calc:` prefix enables arithmetic expressions.

**Supported Operators:** `+`, `-`, `*`, `/`, `()`

**Examples:**
```json
"offset": "calc:headerSize + (index * 16)"
"length": "calc:(width * height * 3)"
"count": "calc:fileSize / recordSize"
```

**Variable References in Expressions:**
```json
"calc:currentOffset + recordSize"
"calc:(width * height) / 8"
```

### Validation Rules

```json
{
  "type": "field",
  "name": "Image Width",
  "validationRules": {
    "minValue": 1,
    "maxValue": 65535,
    "errorMessage": "Width must be 1-65535"
  }
}
```

**Validation Types:**
- **Range**: `minValue`, `maxValue`
- **Enum**: `allowedValues: [1, 2, 4, 8]`
- **Pattern**: `pattern: "^[A-Z]{4}$"` (regex for strings)

### Hidden Fields

Fields parsed but not displayed (useful for metadata).

```json
{
  "type": "field",
  "name": "Internal Flag",
  "offset": 12,
  "length": 4,
  "valueType": "uint32",
  "storeAs": "flags",
  "hidden": true                    // Parse but don't show
}
```

## 📝 Examples

### Example 1: Simple Header

```json
{
  "formatName": "Custom Binary",
  "version": "1.0",
  "extensions": [".cb"],
  "description": "Custom binary format",

  "detection": {
    "signature": "43425346",        // "CBSF" in hex
    "offset": 0
  },

  "blocks": [
    {
      "type": "signature",
      "name": "Signature",
      "offset": 0,
      "length": 4,
      "color": "#FF6B6B"
    },
    {
      "type": "field",
      "name": "Version",
      "offset": 4,
      "length": 2,
      "valueType": "uint16",
      "color": "#4ECDC4",
      "storeAs": "version"
    },
    {
      "type": "field",
      "name": "Record Count",
      "offset": 6,
      "length": 4,
      "valueType": "uint32",
      "color": "#FFE66D",
      "storeAs": "recordCount"
    }
  ]
}
```

### Example 2: Dynamic Array

```json
{
  "blocks": [
    {
      "type": "field",
      "name": "Entry Count",
      "offset": 0,
      "length": 4,
      "valueType": "uint32",
      "storeAs": "count",
      "color": "#4ECDC4"
    },
    {
      "type": "loop",
      "count": "var:count",
      "body": [
        {
          "type": "field",
          "name": "Entry ID",
          "offset": "calc:4 + (i * 8)",
          "length": 4,
          "valueType": "uint32",
          "color": "#FFE66D"
        },
        {
          "type": "field",
          "name": "Entry Value",
          "offset": "calc:8 + (i * 8)",
          "length": 4,
          "valueType": "uint32",
          "color": "#95E1D3"
        }
      ]
    }
  ]
}
```

### Example 3: Conditional Parsing

```json
{
  "blocks": [
    {
      "type": "field",
      "name": "Format Type",
      "offset": 0,
      "length": 1,
      "valueType": "uint8",
      "storeAs": "formatType",
      "color": "#FF6B6B"
    },
    {
      "type": "conditional",
      "condition": "formatType == 1",
      "then": [
        {
          "type": "field",
          "name": "RGB Data",
          "offset": 1,
          "length": "var:dataSize",
          "valueType": "bytes",
          "color": "#FFE66D"
        }
      ],
      "else": [
        {
          "type": "field",
          "name": "Indexed Data",
          "offset": 1,
          "length": "var:dataSize",
          "valueType": "bytes",
          "color": "#95E1D3"
        }
      ]
    }
  ]
}
```

## 💡 Best Practices

### 1. Use Descriptive Names
```json
"name": "Image Width (pixels)"      // ✅ Clear
"name": "Field 1"                   // ❌ Unclear
```

### 2. Add Descriptions
```json
"description": "Width in pixels, must be multiple of 8"
```

### 3. Choose Appropriate Colors
- **Red (#FF6B6B)**: Signatures, critical fields
- **Blue (#4ECDC4)**: Headers, metadata
- **Yellow (#FFE66D)**: Data fields
- **Green (#95E1D3)**: Variable-length data

### 4. Use Validation
```json
"validationRules": {
  "minValue": 0,
  "maxValue": 255,
  "errorMessage": "Value must be 0-255"
}
```

### 5. Store Important Values
```json
"storeAs": "chunkCount"            // Reuse in loops/calculations
```

### 6. Hide Internal Fields
```json
"hidden": true                      // Parse but don't clutter UI
```

### 7. Limit Recursion
- Maximum depth: 10 levels
- Maximum fields: 10,000
- Use loops instead of deep nesting

## 🎯 Performance Considerations

- **Loop Limits**: Loops capped at 1000 iterations (safety)
- **Field Limits**: 10,000 fields max per format
- **Depth Limits**: 10 levels of nesting
- **Virtualization**: UI automatically virtualizes for large lists

## 🔍 Debugging

Enable debug output in Visual Studio Output window:
```csharp
System.Diagnostics.Debug.WriteLine("Field parsing info");
```

Common issues:
- **No fields shown**: Check file matches signature
- **Wrong values**: Verify offset calculations and endianness
- **Slow parsing**: Check for infinite loops or excessive fields

## 📚 Additional Resources

- **Format Examples**: See subdirectories (Images/, Archives/, etc.)
- **API Documentation**: See source code XMLDoc comments
- **Issue Tracking**: GitHub Issue #111

---

Created for WPFHexaEditor - GNU Affero General Public License v3.0 License - 2026

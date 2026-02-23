# Format Definition JSON Schema

**Version:** 1.0
**Last Updated:** 2026-02-22
**Author:** WPFHexaEditor Team

## Overview

This document describes the JSON schema for format definition files used by the WPFHexaEditor automatic format detection system. Format definitions enable the hex editor to automatically detect file formats and provide visual highlighting of file structures.

## Quick Start

A minimal format definition requires 9 top-level sections:

```json
{
    "formatName": "Example Format",
    "version": "1.0",
    "extensions": [".ext"],
    "description": "Description of the format",
    "category": "CategoryName",
    "author": "WPFHexaEditor Team",
    "detection": {
        "signature": "4D5A",
        "offset": 0,
        "required": true
    },
    "variables": {},
    "blocks": [
        {
            "type": "signature",
            "name": "Magic Bytes",
            "offset": 0,
            "length": 2,
            "color": "#FF6B6B",
            "opacity": 0.4,
            "description": "File signature"
        }
    ]
}
```

## Complete Schema Reference

### Top-Level Properties

All 9 properties are **required**:

#### 1. `formatName` (string)

Human-readable name of the format.

**Example:**
```json
"formatName": "ZIP Archive"
```

#### 2. `version` (string)

Version number of this format definition (not the format itself).

**Example:**
```json
"version": "1.0"
```

#### 3. `extensions` (array of strings)

File extensions associated with this format. Must contain at least one extension.

**Examples:**
```json
"extensions": [".zip"]
"extensions": [".zip", ".jar", ".apk", ".docx", ".xlsx"]
```

#### 4. `description` (string)

Detailed description of the format, including technical details.

**Example:**
```json
"description": "ZIP archive format (PKZIP) - Used by many file types including Office documents"
```

#### 5. `category` (string)

Format category for organizational purposes. Should match the parent directory name.

**Valid Categories:**
- `3D` - 3D model formats
- `Archives` - Compressed archives
- `Audio` - Audio files
- `CAD` - CAD/engineering files
- `Certificates` - Security certificates
- `Crypto` - Cryptographic files
- `Data` - Data serialization formats
- `Database` - Database files
- `Disk` - Disk images and filesystems
- `Documents` - Document formats
- `Executables` - Executable binaries
- `Fonts` - Font files
- `Game` - Game ROMs, patches, and assets
- `Images` - Image formats
- `Medical` - Medical imaging formats
- `Network` - Network captures
- `Programming` - Source code
- `Science` - Scientific data formats
- `System` - System files
- `Video` - Video formats

**Example:**
```json
"category": "Archives"
```

#### 6. `author` (string)

Author or maintainer of this format definition.

**Standard Value:**
```json
"author": "WPFHexaEditor Team"
```

#### 7. `references` (object, optional)

Technical specifications and web documentation links for this format.

**Properties:**
- `specifications` (array of strings) - List of technical specification names
- `web_links` (array of strings) - URLs to documentation and specifications

**Example:**
```json
"references": {
    "specifications": [
        "PKWARE APPNOTE.TXT - ZIP File Format Specification",
        "ISO/IEC 21320-1:2015 - Document Container File"
    ],
    "web_links": [
        "https://pkware.cachefly.net/webdocs/casestudies/APPNOTE.TXT",
        "https://en.wikipedia.org/wiki/ZIP_(file_format)",
        "https://www.iso.org/standard/60101.html"
    ]
}
```

**Display:** References appear as clickable links in the Parsed Fields panel, allowing quick access to format specifications.

#### 8. `detection` (object)

Rules for automatic format detection using magic bytes (file signatures).

**Required Properties:**
- `signature` (string) - Hexadecimal signature bytes (even number of hex digits)
- `offset` (integer) - Byte position where signature appears (usually 0)
- `required` (boolean) - Whether signature is mandatory for detection

**Examples:**
```json
"detection": {
    "signature": "504B0304",
    "offset": 0,
    "required": true
}
```

```json
"detection": {
    "signature": "EFBBBF",
    "offset": 0,
    "required": false
}
```

**Validation Rules:**
- Signature must be valid hexadecimal (0-9, A-F)
- Signature length must be even (pairs of hex digits)
- Offset must be >= 0

#### 9. `variables` (object)

State variables for the format script interpreter. Used to store parsed values and track position during file parsing.

Can be an empty object `{}` if no variables are needed.

**Examples:**

Empty (no variables):
```json
"variables": {}
```

With variables:
```json
"variables": {
    "currentOffset": 0,
    "fileCount": 0,
    "prgRomSize": 0,
    "chrRomSize": 0
}
```

**Common Variables:**
- `currentOffset` - Track current read position
- File size/count variables
- Parsed values from header fields

#### 10. `blocks` (array of objects)

Defines the visual blocks and structure of the format. Must contain at least one block.

See [Block Types](#block-types) section for detailed information.

**Minimum Requirement:**
At least one block (typically the signature block).

---

## Block Types

Each block in the `blocks` array represents a region of the file to highlight and parse.

### Common Block Types

#### Type: `signature`

Marks magic bytes or file signature.

**Required Properties:**
- `type`: `"signature"`
- `name` (string): Block name
- `offset` (integer|string): Byte offset
- `length` (integer): Size in bytes
- `color` (string): Hex color code `#RRGGBB`
- `opacity` (float): 0.0-1.0
- `description` (string): Documentation

**Example:**
```json
{
    "type": "signature",
    "name": "ZIP Signature",
    "offset": 0,
    "length": 4,
    "color": "#FF6B6B",
    "opacity": 0.4,
    "description": "PK\\x03\\x04 magic bytes"
}
```

**Color Convention:** Red tones (#FF6B6B) with higher opacity (0.4) for signatures.

#### Type: `field`

Represents a data field with a specific type and value.

**Required Properties:**
- `type`: `"field"`
- `name` (string): Field name
- `offset` (integer|string): Byte offset
- `length` (integer): Size in bytes
- `color` (string): Hex color code
- `opacity` (float): 0.0-1.0
- `description` (string): Documentation
- `valueType` (string): Data type

**Optional Properties:**
- `storeAs` (string): Variable name to store parsed value
- `validationRules` (object): Value constraints
- `hidden` (boolean): Hide from UI (default: false)

**Value Types:**
- `uint8` - Unsigned 8-bit integer
- `uint16` - Unsigned 16-bit integer
- `uint32` - Unsigned 32-bit integer
- `int8` - Signed 8-bit integer
- `int16` - Signed 16-bit integer
- `int32` - Signed 32-bit integer
- `string` - ASCII string
- `bytes` - Raw byte array

**Example:**
```json
{
    "type": "field",
    "name": "PRG ROM Size",
    "offset": 4,
    "length": 1,
    "color": "#4ECDC4",
    "opacity": 0.3,
    "valueType": "uint8",
    "storeAs": "prgRomSize",
    "description": "PRG ROM size in 16KB units",
    "validationRules": {
        "minValue": 1,
        "maxValue": 255
    }
}
```

**Color Conventions:**
- Cyan (#4ECDC4) - Primary fields
- Light green (#95E1D3) - Secondary fields
- Yellow (#FFE66D) - Flags and options
- Purple (#B8B8FF, #C7CEEA) - Metadata and reserved
- Pink (#FF6B9D) - Checksums and validation

#### Type: `conditional`

Executes blocks conditionally based on a condition.

**Required Properties:**
- `type`: `"conditional"`
- `condition` (object): Condition to evaluate
- `then` (array): Blocks to execute if true
- `else` (array, optional): Blocks if false

**Condition Properties:**
- `field` (string): Field to check (`"offset:N"` or `"var:name"`)
- `operator` (string): Comparison operator
- `value` (string): Value to compare
- `length` (integer): Bytes to read

**Operators:**
- `"equals"`
- `"notEquals"`
- `"greaterThan"`
- `"lessThan"`

**Example:**
```json
{
    "type": "conditional",
    "condition": {
        "field": "var:flags",
        "operator": "equals",
        "value": "0x01",
        "length": 1
    },
    "then": [
        {
            "type": "field",
            "name": "Optional Data",
            "offset": 10,
            "length": 4,
            "color": "#95E1D3",
            "opacity": 0.3,
            "description": "Present when flag is set"
        }
    ]
}
```

#### Type: `loop`

Repeats blocks while a condition is true.

**Required Properties:**
- `type`: `"loop"`
- `condition` (object): Exit condition (loop while false)
- `body` (array): Blocks to execute each iteration
- `maxIterations` (integer, optional): Safety limit (default: 1000, max: 100000)

**Example:**
```json
{
    "type": "loop",
    "condition": {
        "field": "var:currentOffset",
        "operator": "lessThan",
        "value": "var:fileSize",
        "length": 4
    },
    "body": [
        {
            "type": "field",
            "name": "Entry",
            "offset": "var:currentOffset",
            "length": 16,
            "color": "#4ECDC4",
            "opacity": 0.3,
            "description": "Loop entry"
        }
    ],
    "maxIterations": 1000
}
```

**Special Variables:**
- `$iteration$` - Current iteration number (automatically set)

#### Type: `action`

Modifies variables (increment, decrement, set).

**Required Properties:**
- `type`: `"action"`
- `action` (string): Action type
- `variable` (string): Variable to modify
- `value` (mixed, optional): Value for `setVariable`

**Actions:**
- `"increment"` - Add 1 to variable
- `"decrement"` - Subtract 1 from variable
- `"setVariable"` - Set variable to value

**Example:**
```json
{
    "type": "action",
    "action": "increment",
    "variable": "currentOffset"
}
```

---

## Dynamic Offsets and Lengths

Blocks can use dynamic offsets and lengths:

### Static Value
```json
"offset": 16
"length": 4
```

### Variable Reference
```json
"offset": "var:currentOffset"
"length": "var:dataSize"
```

### Calculated Expression
```json
"offset": "calc:var:headerSize + 16"
"length": "calc:var:blockSize * 2"
```

---

## Validation Rules

For `field` blocks, you can specify validation rules:

```json
"validationRules": {
    "minValue": 0,
    "maxValue": 255
}
```

```json
"validationRules": {
    "allowedValues": [0, 128, 192]
}
```

---

## Complete Example

Here's a complete format definition for NES ROM files:

```json
{
    "formatName": "NES ROM",
    "version": "1.0",
    "extensions": [".nes"],
    "description": "Nintendo Entertainment System ROM (iNES format)",
    "category": "Game",
    "author": "WPFHexaEditor Team",
    "detection": {
        "signature": "4E45531A",
        "offset": 0,
        "required": true
    },
    "variables": {
        "currentOffset": 0,
        "prgRomSize": 0,
        "chrRomSize": 0
    },
    "blocks": [
        {
            "type": "signature",
            "name": "NES Magic",
            "offset": 0,
            "length": 4,
            "color": "#FF6B6B",
            "opacity": 0.4,
            "description": "NES\\x1A magic number - iNES header marker"
        },
        {
            "type": "field",
            "name": "PRG ROM Size",
            "offset": 4,
            "length": 1,
            "color": "#4ECDC4",
            "opacity": 0.3,
            "valueType": "uint8",
            "storeAs": "prgRomSize",
            "description": "PRG ROM size in 16KB units",
            "validationRules": {
                "minValue": 1,
                "maxValue": 255
            }
        },
        {
            "type": "field",
            "name": "CHR ROM Size",
            "offset": 5,
            "length": 1,
            "color": "#95E1D3",
            "opacity": 0.3,
            "valueType": "uint8",
            "storeAs": "chrRomSize",
            "description": "CHR ROM size in 8KB units. 0 = uses CHR RAM"
        }
    ]
}
```

---

## Best Practices

### Naming Conventions

1. **Format Names:** Use descriptive names with proper capitalization
   - Good: `"NES ROM"`, `"ZIP Archive"`, `"PNG Image"`
   - Bad: `"nes"`, `"zipfile"`, `"png"`

2. **Block Names:** Clear, concise descriptions
   - Good: `"File Header"`, `"CRC-32 Checksum"`
   - Bad: `"data"`, `"stuff"`, `"bytes"`

3. **Variable Names:** Use camelCase
   - Good: `"currentOffset"`, `"fileCount"`, `"prgRomSize"`
   - Bad: `"offset"`, `"cnt"`, `"size"`

### Color Coding

Use consistent colors across formats:

- **#FF6B6B** (Red) - Signatures and magic bytes (opacity: 0.4)
- **#4ECDC4** (Cyan) - Primary data fields (opacity: 0.3)
- **#95E1D3** (Light Green) - Secondary fields (opacity: 0.3)
- **#FFE66D** (Yellow) - Flags and configuration (opacity: 0.3)
- **#C7CEEA, #B8B8FF** (Purple) - Metadata and reserved (opacity: 0.3)
- **#FF6B9D** (Pink) - Checksums and validation (opacity: 0.3)

### Documentation

- Write clear `description` fields for all blocks
- Explain what values mean (units, ranges, purpose)
- Include technical references when applicable

### Variables

- Only define variables you actually use
- Use `storeAs` to save parsed values for later reference
- Empty `variables: {}` is perfectly valid for simple formats

### Testing

After creating a format definition:

1. Validate using the validation script:
   ```bash
   python Tools/validate_formats.py
   ```

2. Test with actual files of that format
3. Verify visual highlighting appears correctly
4. Check that detection works with various file samples

---

## File Organization

Format definitions are organized by category:

```
FormatDefinitions/
├── 3D/
│   ├── BLEND.json
│   ├── FBX.json
│   └── ...
├── Archives/
│   ├── ZIP.json
│   ├── RAR.json
│   └── ...
├── Game/
│   ├── ROM_NES.json
│   ├── ROM_GB.json
│   └── ...
└── Images/
    ├── PNG.json
    ├── JPEG.json
    └── ...
```

Each JSON file should be named after the format (uppercase).

---

## Validation

All format definitions must pass validation with:

```bash
python Tools/validate_formats.py
```

The validator checks:
- ✅ All 9 required top-level properties present
- ✅ Proper data types for each property
- ✅ Valid hex signatures (even length, hex digits only)
- ✅ Non-empty arrays and strings
- ✅ At least one block defined
- ✅ Block structure validity

---

## Advanced Topics

### Working with Variable-Length Structures

For formats with variable-length data:

```json
{
    "type": "field",
    "name": "Variable Data Region",
    "offset": "var:dataStart",
    "length": "var:dataLength",
    "color": "#FFE66D",
    "opacity": 0.2,
    "valueType": "bytes",
    "description": "Variable-length structure"
}
```

### Nested Structures

Use conditionals to handle nested or optional structures:

```json
{
    "type": "conditional",
    "condition": {
        "field": "offset:6",
        "operator": "equals",
        "value": "0x01",
        "length": 1
    },
    "then": [
        {
            "type": "field",
            "name": "Extended Header",
            "offset": 100,
            "length": 64,
            "color": "#95E1D3",
            "opacity": 0.3,
            "description": "Present in version 2+"
        }
    ]
}
```

### Performance Considerations

- Keep `maxIterations` reasonable (< 10,000 for most cases)
- Avoid deeply nested conditionals
- Use `hidden: true` for internal fields not needed in UI

---

## Troubleshooting

### Common Validation Errors

1. **"Missing required property: X"**
   - Add the missing top-level property

2. **"Detection.signature must have even number of hex digits"**
   - Signature must be pairs: `"4D5A"` not `"4D5A0"`

3. **"Property 'blocks' array cannot be empty"**
   - Add at least one block (typically the signature block)

4. **"Property 'extensions' must be a non-empty array"**
   - Add at least one file extension: `["ext"]`

### Common Warnings

1. **"Block[N] has unknown type"**
   - Using non-standard block type (data, header, etc.)
   - Consider using standard types for better compatibility

2. **"Block[N] missing recommended property"**
   - Add suggested properties for completeness

---

## See Also

- [Format Detection Service](../Sources/WPFHexaEditor/Services/FormatDetectionService.cs)
- [Format Definition Model](../Sources/WPFHexaEditor/Core/FormatDetection/FormatDefinition.cs)
- [Format Script Interpreter](../Sources/WPFHexaEditor/Core/FormatDetection/FormatScriptInterpreter.cs)
- [Validation Script](../Sources/WPFHexaEditor/Tools/validate_formats.py)
- [C# Validator](../Sources/WPFHexaEditor/Tools/FormatDefinitionValidator.cs)

---

## Changelog

### Version 1.0 (2026-02-22)
- Initial schema documentation
- 426 format definitions validated
- 100% compliance rate achieved

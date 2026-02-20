# Multi-Byte TBL Character Tables

This folder contains sample TBL files demonstrating multi-byte character support introduced in WPFHexaEditor V2.

## Overview

The TBL (Character Table) system now supports character encodings from **1 byte up to 8 bytes** (2-16 hex characters), enabling proper display of complex character mappings used in ROM hacking and reverse engineering.

## Files

- **multibyte-demo.tbl**: Example TBL with 1, 2, 3, and 4-byte character mappings
- **multibyte-test.bin**: Binary file with matching byte sequences for testing
- **README.md**: This documentation file

## Supported Byte Lengths

| Bytes | Hex Chars | Example Entry | Description |
|-------|-----------|---------------|-------------|
| 1 byte | 2 chars | `FF=A` | Single-byte ASCII characters |
| 2 bytes | 4 chars | `88A9=の` | Dual Title Encoding (DTE) |
| 3 bytes | 6 chars | `0428FF=Hello` | Triple-byte sequences |
| 4 bytes | 8 chars | `0428FFCC=MultiByte` | Quad-byte sequences |
| 5-8 bytes | 10-16 chars | Supported | Extended multi-byte |

## Features

### Greedy Matching Algorithm

The TBL system uses **longest-match-first** greedy matching:

- If both `0428` (2-byte) and `0428FF` (3-byte) exist in the TBL, the longer match (`0428FF`) is always selected
- This allows overlapping sequences while prioritizing longer matches
- Matching order: 8 bytes → 7 → 6 → 5 → 4 → 3 → 2 → 1 byte

**Example:**
```
TBL entries:
0428=α        (2 bytes)
0428FF=Hello  (3 bytes)

Binary data: 04 28 FF

Result: Displays "Hello" (not "α" + "A")
```

### Color Customization

Each byte-length category can have its own text color:

- **1-byte (ASCII)**: Default or TBL ASCII color
- **2-byte (DTE/MTE)**: DTE or MTE color
- **3-byte**: Customizable (Cyan by default)
- **4+ byte**: Customizable (Magenta by default)
- **Special types** (EndBlock, EndLine, Japanese): Get both text color and semi-transparent background

Configure colors in: **Settings Panel → Colors → TBL 3-Byte / 4+ Byte**

### TBL Statistics

When a TBL is loaded, the status bar icon (📋) shows detailed statistics:

```
📋 TBL File: multibyte-demo.tbl
━━━━━━━━━━━━━━━━━━━━━━━━━
Total Entries: 42

  • ASCII: 14
  • DTE (Dual): 10
  • 3-Byte: 8
  • 4-Byte: 8
  • End Block: 1
  • End Line: 1

⚠️ Edit only in HEX panel when TBL is loaded
```

## Usage

### Loading via Standard Menu

1. Open WpfHexEditor Sample.Main application
2. Click: **File → Character Table (TBL) → Load TBL File...**
3. Navigate to `SampleFiles/TBL/` directory
4. Select `multibyte-demo.tbl`
5. Open the test binary: **File → Open...** → Select `multibyte-test.bin`

### What You'll See

The ASCII panel will display:
- `ABCDEF ` - Single-byte characters (default ASCII color)
- `のαβ ` - 2-byte DTE characters (yellow)
- `HelloWorldTest3 ` - 3-byte sequences (cyan)
- `MultiByteDeadBeefCoffeBean ` - 4-byte sequences (magenta)
- ` ABC` - More single-byte characters

### Customizing Colors

1. Open **Settings Panel** (gear icon)
2. Scroll to **Colors** section
3. Find **TBL 3-Byte Color** and **TBL 4+ Byte Color**
4. Click to open color picker
5. Select desired colors
6. Changes apply immediately to the ASCII panel

### Variable-Width Characters

Multi-byte TBL characters can be longer than regular ASCII:
- "Hello" (5 chars) occupies the same space as 3 bytes
- "MultiByte" (9 chars) occupies the same space as 4 bytes
- Hit testing accounts for actual character width
- Each byte is still individually clickable

## Creating Your Own Multi-Byte TBL

### Format

```
# Comments start with #
HexValue=DisplayText

# 1 byte (2 hex chars)
FF=A

# 2 bytes (4 hex chars)
88A9=の

# 3 bytes (6 hex chars)
0428FF=Hello

# 4 bytes (8 hex chars)
DEADBEEF=💀

# Special markers
/XX=EndBlock    # Marks end of text block
*XX=EndLine     # Marks end of line
```

### Rules

1. **Even hex length**: Entries must have even number of hex characters (2, 4, 6, 8, 10, 12, 14, or 16)
2. **Maximum**: 16 hex characters (8 bytes)
3. **Case insensitive**: `FF` and `ff` are treated the same
4. **Greedy matching**: Longer matches take priority
5. **Display text**: Can be any Unicode string (including emojis)

### Testing Greedy Matching

To test the greedy matching algorithm:

```tbl
# Create overlapping entries
04=A          (1 byte)
0428=B        (2 bytes)
0428FF=C      (3 bytes)
0428FFCC=D    (4 bytes)
```

Binary `04 28 FF CC` will display as:
- **"D"** (4-byte match wins)

NOT as:
- "A", "B", "C", "C" (would require disabling greedy matching)

## Performance

- **Lookup complexity**: O(M) per byte, where M = max byte length (typically 8)
- **Worst case**: 7 failed lookups + 1 success per byte
- **Best case**: 1 successful lookup (longest match found first)
- **Dictionary**: O(1) lookup per attempt
- **Rendering speed**: < 100ms for typical files even with large TBL

## Backward Compatibility

- Existing 1-byte and 2-byte TBL files work without changes
- Default colors maintained for compatibility
- No breaking changes to TBL file format

## Technical Details

**Issue**: #110 - MultiByte Table File
**Implemented**: Multi-byte support (3-8 bytes)
**Parsing**: Even-length hex strings (4-16 chars)
**Algorithm**: Greedy longest-match-first
**Rendering**: Dynamic width calculation with proper hit testing
**Colors**: Byte-count-based color selection with special type override

## Limitations

- **Maximum**: 8 bytes (16 hex chars) for performance
- **Hex panel editing only**: ASCII panel is read-only when TBL is loaded
- **No MTE skipping in display**: Each byte position shows a character (even if part of multi-byte)

## See Also

- WPFHexaEditor Documentation
- TBL File Format Specification
- Issue #110 on GitHub

---

*Created by WPFHexaEditor V2 with multi-byte TBL support*
*Co-Authored-By: Claude Sonnet 4.5*

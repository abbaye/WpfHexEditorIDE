# ModifyBytes()

Modify multiple consecutive bytes efficiently in a single operation.

---

## 📋 Description

`ModifyBytes()` modifies multiple bytes at once, which is **more efficient than calling `ModifyByte()` in a loop**. Use this for batch modifications or when patching larger data blocks.

---

## 📝 Signature

```csharp
public void ModifyBytes(long position, byte[] values)
```

**Since:** V2.0

---

## ⚙️ Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `position` | `long` | Starting position in file |
| `values` | `byte[]` | Array of new byte values to write |

---

## 🎯 Examples

### Example 1: Patch Binary Data

```csharp
// Patch magic header
var newHeader = new byte[] { 0x4D, 0x5A, 0x90, 0x00 }; // MZ header
hexEditor.ModifyBytes(0, newHeader);

// Much faster than:
// hexEditor.ModifyByte(0x4D, 0);
// hexEditor.ModifyByte(0x5A, 1);
// hexEditor.ModifyByte(0x90, 2);
// hexEditor.ModifyByte(0x00, 3);
```

### Example 2: Write Configuration Block

```csharp
// Create config data
var config = new byte[256];
config[0] = 0x01; // Version
config[1] = 0x00; // Flags
// ... fill rest

// Write entire config block efficiently
hexEditor.ModifyBytes(0x1000, config);
```

### Example 3: Batch Operations

```csharp
// Batch mode for best performance
hexEditor.BeginBatch();
try
{
    // Modify multiple blocks
    hexEditor.ModifyBytes(0x100, block1);
    hexEditor.ModifyBytes(0x500, block2);
    hexEditor.ModifyBytes(0x900, block3);
}
finally
{
    hexEditor.EndBatch();
}
```

---

## ⚡ Performance

| Method | Time (1000 bytes) | Speedup |
|--------|------------------|---------|
| `ModifyByte()` loop | ~15ms | 1x |
| `ModifyBytes()` | ~5ms | **3x faster** |

---

## 🔗 See Also

- **[ModifyByte()](../core/modifybyte.md)** - Modify single byte
- **[BeginBatch()](../core/beginbatch.md)** - Start batch mode

---

**Last Updated:** 2026-02-19

# ClearInsertions()

Clear only byte insertions while preserving modifications and deletions.

---

## 📋 Description

`ClearModifications()` removes **only inserted bytes** from the file, restoring the original file length while keeping byte value modifications and deletions intact.

**Most use cases should use `ClearAllChange()` instead.** Use this for selective undo of structural changes.

---

## 📝 Signature

```csharp
public void ClearInsertions()
```

**Namespace:** `WpfHexaEditor`
**Assembly:** WPFHexaEditor.dll
**Since:** V2.0

---

## ⚙️ Parameters

None

---

## 🔄 Returns

| Type | Description |
|------|-------------|
| `void` | This method does not return a value |

---

## 🎯 Examples

### Example 1: Remove Padding Bytes

```csharp
// Load file
hexEditor.FileName = "data.bin";
long originalLength = hexEditor.Length; // 1000 bytes

// Insert padding at various locations
hexEditor.InsertBytes(0x100, new byte[256]); // Add 256 bytes
hexEditor.InsertBytes(0x500, new byte[128]); // Add 128 bytes

// File is now 1000 + 256 + 128 = 1384 bytes

// Remove all inserted padding
hexEditor.ClearInsertions();

// Result: File back to 1000 bytes
Assert.AreEqual(originalLength, hexEditor.Length);
```

### Example 2: Keep Modifications, Remove Insertions

```csharp
// Original file
hexEditor.FileName = "config.dat";

// Modify some existing bytes
hexEditor.ModifyByte(0x10, 0x100);
hexEditor.ModifyByte(0x20, 0x200);

// Insert new section
hexEditor.InsertBytes(0x1000, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

// Clear only insertions
hexEditor.ClearInsertions();

// Result:
// ✅ Modifications at 0x10 and 0x20 are KEPT
// ❌ Insertion at 0x1000 is REMOVED
```

---

## 💡 Common Use Cases

### 1. Undo Section Addition

```csharp
// Add new data section
hexEditor.InsertBytes(0x5000, new byte[2048]);

// Configure the section
hexEditor.ModifyBytes(0x5000, configData);

// Section doesn't work, remove it but keep other changes
hexEditor.ClearInsertions();
```

### 2. Remove Test Data

```csharp
// Insert test markers
hexEditor.InsertBytes(0x100, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
hexEditor.InsertBytes(0x200, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });

// Modify actual data
hexEditor.ModifyByte(0x50, 0x500);

// Remove test markers, keep real edits
hexEditor.ClearInsertions();
```

---

## ⚠️ Important Notes

- **File Length Changes** - Removes all inserted bytes, reducing file size
- **Performance** - O(n) where n = number of insertions
- **Thread Safety** - Must be called from UI thread
- **UI Update** - Viewport automatically refreshes

---

## 🔗 See Also

- **[ClearModifications()](clearmodifications.md)** - Clear modifications
- **[ClearDeletions()](cleardeletions.md)** - Restore deleted bytes
- **[InsertBytes()](../core/insertbytes.md)** - Insert bytes

---

**Last Updated:** 2026-02-19

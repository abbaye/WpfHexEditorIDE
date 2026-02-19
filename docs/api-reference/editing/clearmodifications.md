# ClearModifications()

Clear only byte value modifications while preserving structural changes (insertions and deletions).

---

## 📋 Description

`ClearModifications()` provides granular control over clearing edits by removing **only byte value modifications** while keeping insertions and deletions intact. This is useful when you want to reset data values but maintain the file structure.

**Most use cases should use `ClearAllChange()` instead.** This method is for advanced scenarios requiring selective undo.

---

## 📝 Signature

```csharp
public void ClearModifications()
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

### Example 1: Basic Usage

```csharp
using WpfHexaEditor;

// Create editor and load file
var hexEditor = new HexEditor();
hexEditor.FileName = "data.bin";

// User modifies some bytes
hexEditor.ModifyByte(0x42, 0x10);
hexEditor.ModifyByte(0xFF, 0x20);
hexEditor.ModifyByte(0xAA, 0x30);

// Later, user wants to reset byte values
hexEditor.ClearModifications();

// Result: All modified bytes are restored to original values
// File length unchanged
```

### Example 2: Preserve Structural Changes

```csharp
// Load file
hexEditor.FileName = "config.dat";

// User makes both structural and value changes
hexEditor.InsertBytes(0x100, new byte[] { 0xAA, 0xBB }); // Add 2 bytes
hexEditor.ModifyByte(0x05, 0x200);                      // Change value
hexEditor.ModifyByte(0x10, 0x300);                      // Change value
hexEditor.DeleteBytes(0x50, 5);                         // Remove 5 bytes

// Clear only the value modifications, keep structure
hexEditor.ClearModifications();

// Result:
// ✅ Insertions at 0x100 are KEPT
// ✅ Deletions at 0x50 are KEPT
// ❌ Modifications at 0x05 and 0x10 are CLEARED (restored to original)
```

### Example 3: Undo Value Changes Only

```csharp
// Scenario: User edited hex values but wants to keep file structure changes

// Apply some modifications
hexEditor.ModifyByte(0x10, 0x100);
hexEditor.ModifyByte(0x20, 0x200);
hexEditor.ModifyByte(0x30, 0x300);

// Insert some data
hexEditor.InsertBytes(0x40, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

// User realizes byte values are wrong but structure is correct
hexEditor.ClearModifications();

// Result:
// - Byte values at 0x10, 0x20, 0x30 restored to original
// - Inserted data at 0x40 remains
// - File length unchanged
```

---

## 💡 Common Use Cases

### 1. Reset Value Edits While Keeping Structure

When patching files, you may want to keep size changes but reset value modifications:

```csharp
// Add padding bytes
hexEditor.InsertBytes(0x1000, new byte[256]);

// Try different byte values
hexEditor.ModifyByte(0x05, 0x50);
hexEditor.ModifyByte(0x10, 0x100);

// Values didn't work, reset them but keep padding
hexEditor.ClearModifications();
```

### 2. Experimental Value Changes

Testing different values without affecting structure:

```csharp
// Insert header
hexEditor.InsertBytes(0, new byte[] { 0x4D, 0x5A }); // MZ header

// Experiment with configuration bytes
hexEditor.ModifyByte(0x10, 0x100);
hexEditor.ModifyByte(0x20, 0x200);

// Experiments failed, reset values but keep header
hexEditor.ClearModifications();
```

### 3. Partial Undo Workflow

Complex editing workflow with selective undo:

```csharp
// Phase 1: Add new section
hexEditor.InsertBytes(0x5000, new byte[1024]);

// Phase 2: Configure section
hexEditor.ModifyByte(0x5000, 0x1000);
hexEditor.ModifyByte(0x5010, 0x2000);

// Phase 3: Remove old section
hexEditor.DeleteBytes(0x1000, 512);

// User wants to keep structural changes but re-configure section
hexEditor.ClearModifications();
// Now can re-apply different configuration values
```

---

## ⚠️ Important Notes

### When to Use This vs ClearAllChange()

| Scenario | Use `ClearModifications()` | Use `ClearAllChange()` |
|----------|----------------------------|------------------------|
| Reset all changes | ❌ No | ✅ **Yes** (recommended) |
| Keep insertions/deletions | ✅ **Yes** | ❌ No |
| Selective undo | ✅ **Yes** | ❌ No |
| File size must stay same | ✅ **Yes** | ❌ No (size resets) |

### Performance

- ⚡ **Fast** - O(m) where m = number of modifications
- 💾 **No allocations** - Clears in-place
- 🔄 **UI Update** - Automatically refreshes viewport

### Thread Safety

- ⚠️ **Not thread-safe** - Call from UI thread only
- ✅ **Async safe** - Can be called from async/await context

---

## 🔗 See Also

### Related APIs
- **[ClearInsertions()](clearinsertions.md)** - Clear only insertions
- **[ClearDeletions()](cleardeletions.md)** - Clear only deletions
- **[ClearAllChange()](../core/clearallchange.md)** - Clear all edits (recommended for most cases)

### Other References
- **[Undo()](../core/undo.md)** - Undo last operation
- **[Redo()](../core/redo.md)** - Redo last undone operation
- **[ModifyByte()](../core/modifybyte.md)** - Modify single byte
- **[ModifyBytes()](modifybytes.md)** - Modify multiple bytes

---

**API Added:** V2.0
**Last Updated:** 2026-02-19

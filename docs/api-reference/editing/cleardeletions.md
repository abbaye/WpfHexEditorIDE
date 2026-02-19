# ClearDeletions()

Clear only byte deletions (restore deleted bytes) while preserving modifications and insertions.

---

## 📋 Description

`ClearDeletions()` restores all deleted bytes to their original state, increasing file length back while keeping byte value modifications and insertions intact.

---

## 📝 Signature

```csharp
public void ClearDeletions()
```

**Since:** V2.0

---

## 🎯 Example

```csharp
// Original file: 1000 bytes
hexEditor.FileName = "data.bin";

// Delete some bytes
hexEditor.DeleteBytes(0x100, 50);  // Remove 50 bytes
hexEditor.DeleteBytes(0x500, 100); // Remove 100 bytes
// File now: 850 bytes

// Restore all deleted bytes
hexEditor.ClearDeletions();

// Result: File back to 1000 bytes
```

---

## 💡 Use Case

```csharp
// Remove section temporarily
hexEditor.DeleteBytes(0x2000, 512);

// Make other changes
hexEditor.ModifyByte(0x10, 0x100);

// Restore deleted section
hexEditor.ClearDeletions();
```

---

**Last Updated:** 2026-02-19

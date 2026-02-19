# 📖 HexEditor V2 - API Reference

Complete API documentation for **WPF HexEditor V2**

---

## 📚 Categories

### ⚙️ Editing Operations

| API | Description | Added |
|-----|-------------|-------|
| **[ClearModifications](editing/clearmodifications.md)** | Clear only byte modifications | V2.0 |
| **[ClearInsertions](editing/clearinsertions.md)** | Clear only byte insertions | V2.0 |
| **[ClearDeletions](editing/cleardeletions.md)** | Clear only byte deletions | V2.0 |
| **[ModifyBytes](editing/modifybytes.md)** | Modify multiple bytes at once | V2.0 |

### 🔍 Search Operations

| API | Description | Added |
|-----|-------------|-------|
| **[CountOccurrences](search/countoccurrences.md)** | Count pattern occurrences (memory efficient) | V2.0 |

---

## 🆕 What's New in V2

### Granular Clear Operations
Fine-grained control over clearing edits:
- **ClearModifications()** - Reset byte values while keeping structure
- **ClearInsertions()** - Remove insertions while keeping modifications
- **ClearDeletions()** - Restore deleted bytes

### Batch Modifications
- **ModifyBytes()** - More efficient than looping ModifyByte()

### Optimized Search
- **CountOccurrences()** - Memory-efficient pattern counting

---

## 📖 Documentation Format

Each API page includes:
- ✅ **Description** - Clear explanation
- ✅ **Signature** - Complete method signature
- ✅ **Parameters** - Detailed parameter docs
- ✅ **Returns** - Return value description
- ✅ **Examples** - Basic and advanced code samples
- ✅ **Use Cases** - Common scenarios
- ✅ **Notes** - Important considerations

---

## 🔗 See Also

- **[Getting Started Guide](../../GETTING_STARTED.md)** - Tutorial
- **[Feature List](../../FEATURES.md)** - All features
- **[Architecture](../architecture/)** - System design

---

**Last Updated:** 2026-02-19

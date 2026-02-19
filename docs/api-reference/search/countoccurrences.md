# CountOccurrences()

Count pattern occurrences without storing positions (memory efficient).

---

## 📋 Description

`CountOccurrences()` counts how many times a byte pattern appears in the file **without storing positions**, making it much more memory-efficient than `FindAll().Count()` for large files.

---

## 📝 Signature

```csharp
public int CountOccurrences(byte[] pattern, long startPosition = 0)
```

**Since:** V2.0

---

## ⚙️ Parameters

| Parameter | Type | Description | Default |
|-----------|------|-------------|---------|
| `pattern` | `byte[]` | Byte pattern to count | - |
| `startPosition` | `long` | Position to start from | `0` |

---

## 🔄 Returns

| Type | Description |
|------|-------------|
| `int` | Number of occurrences found |

---

## 🎯 Examples

### Example 1: Count Pattern

```csharp
// Count how many times 0xFF appears
var pattern = new byte[] { 0xFF };
int count = hexEditor.CountOccurrences(pattern);
Console.WriteLine($"Found {count} occurrences of 0xFF");
```

### Example 2: Count Multi-Byte Pattern

```csharp
// Count null-terminated strings
var nullTerm = new byte[] { 0x00 };
int stringCount = hexEditor.CountOccurrences(nullTerm);

// Count specific signature
var signature = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
int signatureCount = hexEditor.CountOccurrences(signature);
```

### Example 3: Count From Position

```csharp
// Count occurrences after position 0x1000
var pattern = new byte[] { 0xAA, 0xBB };
int count = hexEditor.CountOccurrences(pattern, startPosition: 0x1000);
```

---

## ⚡ Performance vs FindAll()

### Memory Usage Comparison

| File Size | Matches | `FindAll().Count()` Memory | `CountOccurrences()` Memory |
|-----------|---------|---------------------------|----------------------------|
| 10 MB | 1,000 | ~8 KB | ~0 KB |
| 100 MB | 10,000 | ~80 KB | ~0 KB |
| 1 GB | 100,000 | **~800 KB** | ~0 KB |

### When to Use What

| Scenario | Use `CountOccurrences()` | Use `FindAll()` |
|----------|-------------------------|----------------|
| Only need count | ✅ **Yes** | ❌ No |
| Need positions | ❌ No | ✅ **Yes** |
| Large file | ✅ **Yes** | ⚠️ May use lots of memory |
| Many matches | ✅ **Yes** | ⚠️ High memory usage |

---

## 💡 Use Cases

### 1. Statistics

```csharp
// Analyze byte frequency
int nullBytes = hexEditor.CountOccurrences(new byte[] { 0x00 });
int ffBytes = hexEditor.CountOccurrences(new byte[] { 0xFF });
Console.WriteLine($"File contains {nullBytes} null bytes and {ffBytes} FF bytes");
```

### 2. Validation

```csharp
// Check if pattern exists before processing
var pattern = new byte[] { 0x4D, 0x5A }; // MZ header
int count = hexEditor.CountOccurrences(pattern);

if (count == 0)
{
    Console.WriteLine("No PE executables found");
}
else
{
    Console.WriteLine($"Found {count} potential PE executables");
}
```

### 3. Progress Reporting

```csharp
// Count total before processing
var pattern = new byte[] { 0xAA, 0xBB };
int total = hexEditor.CountOccurrences(pattern);

// Now process each (using FindAll for positions)
int processed = 0;
foreach (var pos in hexEditor.FindAll(pattern))
{
    // Process...
    processed++;
    Console.WriteLine($"Progress: {processed}/{total}");
}
```

---

## 🔗 See Also

- **[FindAll()](../search/findall.md)** - Find all positions (stores in memory)
- **[FindFirst()](../search/findfirst.md)** - Find first occurrence
- **[ReplaceAll()](../search/replaceall.md)** - Replace all occurrences

---

**Last Updated:** 2026-02-19

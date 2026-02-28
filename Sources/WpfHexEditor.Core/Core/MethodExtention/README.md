# Core/MethodExtention

Extension methods for common types used throughout the hex editor.

## 📁 Contents

- **[ByteArrayExtention.cs](ByteArrayExtention.cs)** - byte[] extension methods
  - `ToHexString()` - Convert byte array to hex string
  - `FindPattern()` - Search for byte pattern
  - `Compare()` - Compare two byte arrays
  - `CopyTo()` - Safe copy operations

- **[StringExtension.cs](StringExtension.cs)** - string extension methods
  - `ToByteArray()` - Parse hex string to bytes
  - `IsHexString()` - Validate hex format
  - `RemoveWhiteSpace()` - Clean hex strings
  - `IsValidFileName()` - Validate file names

- **[DoubleExtension.cs](DoubleExtension.cs)** - double extension methods
  - `ToPrecision()` - Format with specific precision
  - `ToPercentage()` - Convert to percentage string
  - `ToFileSizeString()` - Format as file size (KB, MB, GB)

- **[ApplicationExtention.cs](ApplicationExtention.cs)** - Application extension methods
  - `TryEnqueue()` - Safe dispatcher invoke
  - `TryFindResource()` - Safe resource lookup
  - Async UI thread operations

- **[TrackExtention.cs](TrackExtention.cs)** - Track (progress bar) extensions
  - `SetValueSafe()` - Thread-safe value updates
  - `AnimateValue()` - Smooth value transitions

- **[WithMethodExtention.cs](WithMethodExtention.cs)** - "With" pattern methods
  - Fluent API helpers
  - Immutable object updates
  - Builder pattern support

## 🎯 Purpose

Extension methods add functionality to existing types without modifying them. These extensions provide convenient, reusable utilities used throughout the hex editor codebase.

## 🎓 Usage Examples

### ByteArrayExtention:

```csharp
// Convert to hex string
byte[] data = new byte[] { 0xFF, 0xAA, 0x55 };
string hex = data.ToHexString();
// Returns: "FF AA 55"

// Find pattern
byte[] file = ReadFile("data.bin");
byte[] pattern = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
long position = file.FindPattern(pattern);
// Returns: Position of first match or -1

// Compare arrays
byte[] file1 = ReadFile("v1.bin");
byte[] file2 = ReadFile("v2.bin");
bool identical = file1.Compare(file2);
```

### StringExtension:

```csharp
// Parse hex string
string hex = "FF AA 55 00 11";
byte[] bytes = hex.ToByteArray();
// Returns: [0xFF, 0xAA, 0x55, 0x00, 0x11]

// Validate hex format
string input = "FFAA55";
bool isHex = input.IsHexString();
// Returns: true

// Clean whitespace
string messy = "FF AA  55\t00\n11";
string clean = messy.RemoveWhiteSpace();
// Returns: "FFAA5500 11"
```

### DoubleExtension:

```csharp
// Format file size
double bytes = 1536000;
string size = bytes.ToFileSizeString();
// Returns: "1.46 MB"

// Format percentage
double progress = 0.6543;
string percent = progress.ToPercentage();
// Returns: "65.43%"

// Precision control
double value = 3.14159265;
string formatted = value.ToPrecision(2);
// Returns: "3.14"
```

### ApplicationExtention:

```csharp
// Safe resource lookup
var color = Application.Current.TryFindResource("PrimaryColor") as Color?;
if (color.HasValue)
{
    // Use color
}

// Safe dispatcher invoke
Application.Current.TryEnqueue(() =>
{
    // Update UI from background thread
    StatusText.Text = "Processing...";
});
```

## ✨ Extension Method Benefits

- **Readability**: Fluent, method-chaining syntax
- **Discoverability**: IntelliSense shows extensions on types
- **No Inheritance**: Add methods without subclassing
- **Reusability**: Used across entire codebase
- **Testing**: Easy to unit test extension methods

## 🔍 Common Patterns

### Null-Safe Extensions:
```csharp
public static string SafeToString(this object obj) =>
    obj?.ToString() ?? string.Empty;
```

### Validation Extensions:
```csharp
public static bool IsValidHex(this string str) =>
    !string.IsNullOrWhiteSpace(str) &&
    str.All(c => "0123456789ABCDEFabcdef".Contains(c));
```

### Conversion Extensions:
```csharp
public static byte[] ToBytes(this string hex) =>
    Enumerable.Range(0, hex.Length / 2)
        .Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16))
        .ToArray();
```

## 📍 Usage Locations

These extensions are used throughout:
- **[HexEditor.xaml.cs](../../HexEditor.xaml.cs)** - Main control
- **[ByteProvider.cs](../Bytes/ByteProvider.cs)** - Data access
- **[FindReplaceService.cs](../../Services/FindReplaceService.cs)** - Search operations
- **[Dialog windows](../../Dialog/)** - UI interactions
- **[Converters](../Converters/)** - Data conversion

## 🎨 Creating Custom Extensions

```csharp
public static class MyExtensions
{
    // Always: static class, static methods
    public static string ToUpperHex(this byte value) =>
        $"0x{value:X2}";

    // Extension with parameters
    public static byte[] Slice(this byte[] arr, int start, int length) =>
        arr.Skip(start).Take(length).ToArray();

    // Generic extension
    public static bool IsIn<T>(this T value, params T[] collection) =>
        collection.Contains(value);
}

// Usage:
byte b = 255;
string hex = b.ToUpperHex(); // "0xFF"

byte[] data = GetData();
byte[] slice = data.Slice(10, 20);

int num = 5;
bool inSet = num.IsIn(1, 3, 5, 7, 9); // true
```

## 📚 Related Components

- **[ByteConverters.cs](../Bytes/ByteConverters.cs)** - Static conversion methods
- **[ConstantReadOnly.cs](../../ConstantReadOnly.cs)** - Constants used by extensions
- **[All Services](../../Services/)** - Use these extensions

## ⚡ Performance Notes

- Extensions are compiled to static method calls (no overhead)
- Use `IEnumerable<T>` carefully (avoid multiple enumeration)
- Cache results of expensive extensions
- Prefer extensions over reflection for performance

---

✨ Convenient extension methods for common operations throughout the hex editor

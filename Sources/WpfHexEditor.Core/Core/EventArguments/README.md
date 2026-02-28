# Core/EventArguments

Custom event argument classes for hex editor events.

## 📁 Contents

- **[ByteEventArgs.cs](ByteEventArgs.cs)** - Byte modification event data
  - Carries: Modified byte, position, action type
  - Used by: ByteModified event
  - Enables: Undo/redo tracking
  - Contains: Old value, new value, byte position

- **[ByteDifferenceEventArgs.cs](ByteDifferenceEventArgs.cs)** - File comparison event data
  - Carries: List of byte differences between two files
  - Used by: File comparison features
  - Contains: Position list, difference count, color info
  - Enables: Highlighting of differences in UI

- **[CustomBackgroundBlockEventArgs.cs](CustomBackgroundBlockEventArgs.cs)** - Highlighting event data
  - Carries: Custom background block information
  - Used by: Syntax highlighting, search results
  - Contains: Start position, length, color
  - Enables: Visual markers and annotations

## 🎯 Purpose

These custom EventArgs classes provide strongly-typed event data for hex editor operations. They follow the standard .NET event pattern: `EventHandler<TEventArgs>`.

## 🔗 Event Flow

```
User Action
    ↓
HexEditor Operation
    ↓
Event Raised with EventArgs
    ↓
Subscribers Notified
    ↓
UI Update / Service Processing
```

## 🎓 Usage Example

### ByteEventArgs:

```csharp
// In HexEditor.xaml.cs
public event EventHandler<ByteEventArgs> ByteModified;

private void ModifyByte(long position, byte newValue, byte oldValue)
{
    // Perform modification
    _provider.AddByteModified(position, newValue, oldValue);

    // Raise event with custom args
    ByteModified?.Invoke(this, new ByteEventArgs
    {
        Position = position,
        NewValue = newValue,
        OldValue = oldValue,
        Action = ByteAction.Modified
    });
}

// Subscriber
hexEditor.ByteModified += (sender, e) =>
{
    Console.WriteLine($"Byte at {e.Position:X} changed: {e.OldValue:X2} → {e.NewValue:X2}");
};
```

### ByteDifferenceEventArgs:

```csharp
// After comparing two files
public event EventHandler<ByteDifferenceEventArgs> DifferencesFound;

private void CompareFiles(byte[] file1, byte[] file2)
{
    var differences = new List<long>();

    for (long i = 0; i < Math.Min(file1.Length, file2.Length); i++)
    {
        if (file1[i] != file2[i])
            differences.Add(i);
    }

    // Raise event with differences
    DifferencesFound?.Invoke(this, new ByteDifferenceEventArgs
    {
        Differences = differences,
        TotalCount = differences.Count
    });
}
```

### CustomBackgroundBlockEventArgs:

```csharp
// Highlight search results
public event EventHandler<CustomBackgroundBlockEventArgs> AddCustomBackgroundBlock;

private void HighlightSearchResults(long position, long length)
{
    AddCustomBackgroundBlock?.Invoke(this, new CustomBackgroundBlockEventArgs
    {
        StartPosition = position,
        Length = length,
        Color = Colors.Yellow
    });
}
```

## 📋 Event Patterns

All event args follow .NET conventions:
- Inherit from `EventArgs`
- Contain read-only properties
- Immutable after construction
- Used with `EventHandler<T>` delegate

## 🔔 Events Using These Args

**ByteEventArgs:**
- `ByteModified` - When a byte is changed
- `ByteInserted` - When bytes are inserted
- `ByteDeleted` - When bytes are deleted

**ByteDifferenceEventArgs:**
- `DifferencesFound` - After file comparison
- `ComparisonComplete` - Comparison finished

**CustomBackgroundBlockEventArgs:**
- `AddCustomBackgroundBlock` - Add visual marker
- `RemoveCustomBackgroundBlock` - Remove marker
- `ClearAllCustomBackgroundBlocks` - Clear all markers

## ✨ Features

- **Type Safety**: Strongly-typed event data
- **Immutability**: Properties set at construction
- **Extensibility**: Easy to add new properties
- **Standard Pattern**: Follows .NET conventions
- **IntelliSense Friendly**: Full code completion support

## 📚 Related Components

- **[HexEditor.xaml.cs](../../HexEditor.xaml.cs)** - Raises these events
- **[ByteModified.cs](../Bytes/ByteModified.cs)** - Data model for byte changes
- **[ByteDifference.cs](../Bytes/ByteDifference.cs)** - Data model for differences
- **[CustomBackgroundService](../../Services/CustomBackgroundService.cs)** - Uses CustomBackgroundBlockEventArgs

## 🎨 Design Pattern

```csharp
// Standard event pattern
public class SomeEventArgs : EventArgs
{
    public SomeType SomeProperty { get; init; }
    public OtherType OtherProperty { get; init; }
}

// Usage
public event EventHandler<SomeEventArgs> SomeEvent;

protected virtual void OnSomeEvent(SomeEventArgs e) =>
    SomeEvent?.Invoke(this, e);
```

---

✨ Strongly-typed event argument classes for hex editor events

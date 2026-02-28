# Core/Interfaces

Interface definitions for core hex editor components.

## 📁 Contents

- **[IByte.cs](IByte.cs)** - Byte control interface
  - Defines: Common byte display properties
  - Implemented by: Byte_8bit, Byte_16bit, Byte_32bit controls
  - Properties: Byte value, action type, position, read-only state
  - Events: ByteModified, MouseSelection, RightClick

- **[IByteControl.cs](IByteControl.cs)** - Byte manipulation interface
  - Defines: Byte editing operations
  - Methods: UpdateByte, Clear, GetByte, SetByte
  - Properties: IsEditable, IsSelected, IsFocused
  - Used by: All byte display controls

- **[IByteModified.cs](IByteModified.cs)** - Modification tracking interface
  - Defines: Change tracking contract
  - Properties: Original value, modified value, action type
  - Implemented by: ByteModified class
  - Used by: Undo/redo system

## 🎯 Purpose

These interfaces provide contracts for core hex editor functionality, enabling:
- **Polymorphism**: Different byte controls with common interface
- **Testability**: Mock implementations for unit tests
- **Extensibility**: Create custom byte controls
- **Type Safety**: Compile-time checking of implementations

## 🔗 Implementation Hierarchy

```
IByte + IByteControl
    ├── Implemented by: Byte_8bit
    ├── Implemented by: Byte_16bit
    └── Implemented by: Byte_32bit

IByteModified
    └── Implemented by: ByteModified
```

## 🎓 Usage Example

### IByte Interface:

```csharp
public interface IByte
{
    byte? Byte { get; set; }
    long Position { get; set; }
    ByteAction Action { get; set; }
    bool IsSelected { get; set; }
    bool ReadOnlyMode { get; set; }

    event EventHandler ByteModified;
    event EventHandler MouseSelection;
    event EventHandler RightClick;
}

// Implementation
public class Byte_8bit : UserControl, IByte, IByteControl
{
    public byte? Byte { get; set; }
    public long Position { get; set; }
    // ... other properties and methods
}
```

### IByteModified Interface:

```csharp
public interface IByteModified
{
    byte OriginalByte { get; }
    byte ModifiedByte { get; set; }
    ByteAction Action { get; set; }
    long Position { get; }
}

// Implementation
public class ByteModified : IByteModified
{
    public byte OriginalByte { get; }
    public byte ModifiedByte { get; set; }
    public ByteAction Action { get; set; }
    public long Position { get; }
}
```

### Polymorphic Usage:

```csharp
// Work with any byte control
void ProcessByteControl(IByte byteControl)
{
    if (byteControl.Byte.HasValue)
    {
        Console.WriteLine($"Position {byteControl.Position}: 0x{byteControl.Byte:X2}");
    }
}

// Use with different control types
ProcessByteControl(new Byte_8bit());
ProcessByteControl(new Byte_16bit());
ProcessByteControl(new Byte_32bit());
```

## 📐 Interface Design Principles

### IByte - Display Contract:
- **Purpose**: Define what a byte control must display
- **Properties**: Value, position, state, appearance
- **Events**: User interaction notifications
- **Pattern**: View-oriented interface

### IByteControl - Behavior Contract:
- **Purpose**: Define how to manipulate a byte control
- **Methods**: CRUD operations for byte values
- **Pattern**: Controller-oriented interface

### IByteModified - Data Contract:
- **Purpose**: Track change history
- **Properties**: Before/after values, action type
- **Pattern**: Data-oriented interface

## ✨ Benefits

- **Decoupling**: UI separated from data model
- **Flexibility**: Swap implementations without breaking code
- **Testing**: Easy to create mocks and stubs
- **Documentation**: Interfaces serve as API contracts
- **IntelliSense**: Full IDE support for implementers

## 🧪 Testing with Interfaces

```csharp
// Mock implementation for testing
public class MockByteControl : IByte, IByteControl
{
    public byte? Byte { get; set; }
    public long Position { get; set; }
    public bool ReadOnlyMode { get; set; }
    // ... minimal implementation for tests

    public event EventHandler ByteModified;
    public void TriggerByteModified() => ByteModified?.Invoke(this, EventArgs.Empty);
}

// Test with mock
[Fact]
public void TestByteModification()
{
    var mock = new MockByteControl { Byte = 0xFF };
    bool eventRaised = false;
    mock.ByteModified += (s, e) => eventRaised = true;

    mock.TriggerByteModified();

    Assert.True(eventRaised);
}
```

## 📚 Related Components

- **[Byte_8bit.cs](../Bytes/Byte_8bit.cs)** - Implements IByte, IByteControl
- **[ByteModified.cs](../Bytes/ByteModified.cs)** - Implements IByteModified
- **[HexEditor.xaml.cs](../../HexEditor.xaml.cs)** - Uses these interfaces
- **[ByteProvider.cs](../Bytes/ByteProvider.cs)** - Uses IByteModified

## 🎨 Extension Point

Want to create a custom byte control?
```csharp
public class MyCustomByteControl : UserControl, IByte, IByteControl
{
    // Implement interface members
    // Add custom visualization or behavior
}
```

---

✨ Interface contracts for extensible and testable hex editor components

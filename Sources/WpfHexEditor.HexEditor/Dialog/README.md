# Dialog

User interface dialogs for find and replace operations in the hex editor.

## 📁 Contents

This folder contains WPF dialog windows that provide interactive UI for search and replace functionality:

### 🔍 Find Dialogs

- **[FindWindow.xaml.cs](FindWindow.xaml.cs)** - Basic find dialog
  - Search for byte sequences
  - Find First, Next, Last, All operations
  - Uses embedded HexEditor control for input
  - Binds to parent HexEditor for search results

- **[FindReplaceWindow.xaml.cs](FindReplaceWindow.xaml.cs)** - Combined find and replace dialog
  - Search for byte patterns
  - Replace found sequences with new bytes
  - Replace First, Next, All operations
  - Two embedded HexEditor controls (find/replace input)

### 🔄 Replace Dialogs

- **[ReplaceByteWindow.xaml.cs](ReplaceByteWindow.xaml.cs)** - Simple byte replacement dialog
  - Replace single byte value with another
  - Text input for byte values
  - Direct integration with HexEditor's ReplaceByte method

- **[GiveByteWindow.xaml.cs](GiveByteWindow.xaml.cs)** - Byte input dialog
  - Generic byte value input window
  - Used for fill operations and byte insertion
  - Validates hexadecimal input

## 🎯 Purpose

These dialogs provide a user-friendly interface for complex search and replace operations that would be difficult to perform directly in the hex view.

## 🔗 Integration

All dialogs:
- Accept a `HexEditor` parent reference in constructor
- Call methods on the parent HexEditor control
- Use the `FindReplaceService` indirectly through HexEditor API
- Follow WPF MVVM-lite pattern (code-behind for simplicity)

## 📚 Related Components

- **[HexEditor.xaml.cs](../HexEditor.xaml.cs)** - Parent control that hosts these dialogs
- **[FindReplaceService](../Services/FindReplaceService.cs)** - Backend service for search/replace logic
- **[ClipboardService](../Services/ClipboardService.cs)** - Used for data transfer operations

## 🎓 Usage Example

```csharp
// Opening the Find dialog from HexEditor
var findWindow = new FindWindow(this, existingSearchBytes);
findWindow.ShowDialog();

// Opening Find/Replace dialog
var findReplaceWindow = new FindReplaceWindow(this);
findReplaceWindow.ShowDialog();

// Simple byte replacement
var replaceWindow = new ReplaceByteWindow(this);
replaceWindow.ShowDialog();
```

## ✨ Features

- **Embedded HexEditors**: Use HexEditor control for byte pattern input
- **Live Search**: Results appear immediately in parent HexEditor
- **Clear/Reset**: Reset search patterns quickly
- **Validation**: Input validation for hexadecimal values
- **Keyboard Support**: Standard dialog keyboard shortcuts

---

✨ User-friendly dialogs for hex editor find and replace operations

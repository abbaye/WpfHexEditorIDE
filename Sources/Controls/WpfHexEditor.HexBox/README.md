# WpfHexEditor.HexBox

> Lightweight hex input field — MVVM architecture, zero external dependencies, V1 API compatibility.

[![.NET](https://img.shields.io/badge/.NET-net48%20%7C%20net8.0--windows-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](../../LICENSE)

---

## Architecture

```mermaid
graph TB
    subgraph HexBox["WpfHexEditor.HexBox"]
        HB["HexBox (UserControl)\nHexBox.xaml / HexBox.xaml.cs"]

        subgraph VM["ViewModels/"]
            HBV["HexBoxViewModel\n(MVVM — all business logic)"]
        end

        subgraph CORE["Core/"]
            HBS["HexBoxState\n(input state machine)"]
            HBC["HexConversion\n(byte ↔ hex string)"]
            HKV["HexKeyValidator\n(keyboard input filtering)"]
        end

        subgraph CMD["Commands/"]
            HC["HexCommands\n(Copy, Paste, Clear)"]
        end

        subgraph CNV["Converters/"]
            BC["ByteToHexStringConverter"]
            NC["NullToVisibilityConverter"]
        end

        subgraph MDL["Models/"]
            HBM["HexBoxModel\n(data model)"]
        end
    end

    HB --> HBV
    HBV --> HBS
    HBV --> HBC
    HBV --> HKV
    HB --> HC
    HB --> CNV
    HBV --> MDL

    style HB fill:#e3f2fd,stroke:#1976d2,stroke-width:3px
    style HBV fill:#f3e5f5,stroke:#7b1fa2,stroke-width:2px
```

---

## Project Structure

```
WpfHexEditor.HexBox/
├── HexBox.xaml               ← UserControl XAML
├── HexBox.xaml.cs            ← Minimal code-behind (DP sync with ViewModel)
│
├── ViewModels/
│   └── HexBoxViewModel.cs    ← All business logic
│
├── Core/
│   ├── HexBoxState.cs        ← Input state machine (nibble tracking)
│   ├── HexConversion.cs      ← Byte ↔ hex string conversion
│   └── HexKeyValidator.cs    ← Hex key filtering (0-9, A-F)
│
├── Commands/
│   └── HexCommands.cs
│
├── Converters/
│   └── ByteToHexStringConverter.cs
│
├── Models/
│   └── HexBoxModel.cs
│
└── Properties/
```

---

## Features

| Feature | Description |
|---------|-------------|
| **V1 API** | `LongValue` DependencyProperty — backward compatible with all existing XAML |
| **MVVM** | `HexBoxViewModel` contains all logic; code-behind is minimal |
| **Input validation** | Only hex characters (0–9, A–F) accepted |
| **Nibble editing** | Two-character hex input per byte (e.g. typing `F` then `F` → `0xFF`) |
| **Zero deps** | No dependency on `WpfHexEditor.Core` or any other project |
| **Multi-target** | .NET 4.8 and .NET 8.0-windows |

---

## Usage

### XAML

```xml
<Window xmlns:hb="clr-namespace:WpfHexEditor.HexBox;assembly=WpfHexEditor.HexBox">

    <!-- Simple hex input bound to a long value -->
    <hb:HexBox LongValue="{Binding ByteOffset, Mode=TwoWay}" />
</Window>
```

### Code-behind

```csharp
// Get / set the value
hexBox.LongValue = 0x1A2B3C4D;
long offset = hexBox.LongValue;

// React to changes
hexBox.LongValueChanged += (s, e) =>
    Console.WriteLine($"New offset: 0x{hexBox.LongValue:X}");
```

---

## Data Flow

```mermaid
sequenceDiagram
    participant User
    participant HexBox
    participant HexBoxViewModel
    participant HexBoxState

    User->>HexBox: KeyDown('F')
    HexBox->>HexBoxViewModel: HandleKeyInput('F')
    HexBoxViewModel->>HexKeyValidator: IsHexKey('F') → true
    HexBoxViewModel->>HexBoxState: AppendNibble(0xF)
    HexBoxState-->>HexBoxViewModel: nibble 1 of 2
    HexBoxViewModel-->>HexBox: update display "F_"

    User->>HexBox: KeyDown('A')
    HexBox->>HexBoxViewModel: HandleKeyInput('A')
    HexBoxViewModel->>HexBoxState: AppendNibble(0xA)
    HexBoxState-->>HexBoxViewModel: complete byte 0xFA
    HexBoxViewModel->>HexBox: LongValue = 0xFA (via DP)
    HexBox-->>User: display "FA"
```

---

## V1 Compatibility

`HexBox` maintains full backward compatibility with the V1 API. The `LongValue` DependencyProperty is preserved:

```csharp
// V1 usage — still works unchanged
HexEdit.LongValue = someOffset;
```

Internally, `HexBox.xaml.cs` syncs the V1 DP with `HexBoxViewModel.LongValue` via two-way binding, so upgrading from V1 to V2 requires zero changes in consuming code.

---

## Dependencies

`WpfHexEditor.HexBox` has **zero project-level dependencies** — it only uses standard WPF / .NET assemblies.

This makes it safe to use in any WPF application without pulling in the rest of the WpfHexEditor ecosystem.

---

## License

GNU Affero General Public License v3.0 — Copyright 2017–2026 Derek Tremblay. See [LICENSE](../../LICENSE).

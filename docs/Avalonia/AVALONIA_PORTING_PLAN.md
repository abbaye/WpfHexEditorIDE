# Avalonia Porting Plan for HexEditor Control

## 📋 Executive Summary

This plan details the strategy for porting the WPFHexaEditor Control to Avalonia UI, enabling cross-platform support (Windows, Linux, macOS) while maintaining the existing WPF version.

**Recommended Approach:** Progressive Abstraction with Shared Core
**Estimated Duration:** 6-8 weeks
**Currently Portable Code:** 85% (~30,000 lines)
**Code Requiring Adaptation:** 15% (~5,000 lines)

---

## 🎯 Objectives

1. **Multi-platform support**: Enable control usage on Windows, Linux, and macOS via Avalonia
2. **Maintain WPF version**: Ensure compatibility and performance of existing WPF version
3. **Maximize shared code**: Reuse business logic and services as much as possible (85% of code)
4. **Clean architecture**: Create minimal and maintainable abstraction layer
5. **Identical performance**: Ensure equivalent performance on both platforms

---

## 📊 Current Codebase Analysis

### Project Structure (40,940 lines, 127 files)

```
Sources/WPFHexaEditor/
├── Core/                    (~13,050 lines) - ✅ 100% PORTABLE
│   ├── Bytes/              (9,522 lines) - Byte manipulation logic
│   ├── CharacterTable/     (1,891 lines) - TBL character tables
│   ├── Interfaces/         (580 lines) - Business contracts
│   └── MethodExtension/    (1,057 lines) - .NET extensions
│
├── Services/               (~4,305 lines) - ✅ 98% PORTABLE
│   ├── UndoRedoService.cs
│   ├── SearchService.cs
│   ├── SelectionService.cs
│   ├── ByteDataService.cs
│   ├── StreamService.cs
│   └── 10+ other business services
│
├── ViewModels/             (~2,500 lines) - ✅ 95% PORTABLE
│   ├── HexEditorViewModel.cs (INotifyPropertyChanged)
│   └── HexBoxViewModel.cs
│
├── Controls/               (~8,500 lines) - ⚠️ 30% REQUIRES ADAPTATION
│   ├── HexEditor.xaml.cs   (1,500+ lines) - DependencyProperty, Events
│   ├── HexViewport.cs      (535 lines OnRender) - Custom DrawingContext
│   ├── HexBox.xaml.cs      (150 lines) - DependencyProperty
│   ├── BarChartPanel.cs    (171 lines OnRender) - Frequency histogram
│   ├── ScrollMarkerPanel.cs (291 lines) - Scroll markers
│   └── Caret.cs            (264 lines) - Blinking cursor
│
├── Commands/               (~200 lines) - ⚠️ REQUIRES MINOR ADAPTATION
│   └── RelayCommand.cs     (CommandManager.RequerySuggested)
│
├── Converters/             (~800 lines) - ⚠️ REQUIRES ADAPTATION
│   ├── BoolToSelectionBrushConverter.cs
│   ├── ActionToBrushConverter.cs
│   └── 15+ other converters
│
└── Dialog/                 (~3,000 lines) - ⚠️ REQUIRES ADAPTATION
    ├── FindReplaceWindow.xaml
    ├── GotoWindow.xaml
    └── Other dialog windows
```

### Critical WPF Dependencies Identified

| Dependency | Occurrences | Impact | Priority |
|------------|-------------|--------|----------|
| **DrawingContext (OnRender)** | 10 controls | CRITICAL | P0 |
| **DependencyProperty** | 458 usages | HIGH | P1 |
| **System.Windows.Media.Color/Brush** | ~200 usages | HIGH | P0 |
| **KeyEventArgs / MouseButtonEventArgs** | ~50 usages | MEDIUM | P1 |
| **DispatcherTimer** | 2 usages | MEDIUM | P1 |
| **CommandManager.RequerySuggested** | 1 usage | LOW | P2 |
| **IValueConverter** | 15 converters | MEDIUM | P2 |
| **FindName() / Visual Tree** | ~5 usages | LOW | P3 |

---

## 🏗️ Proposed Architecture: Progressive Abstraction

### Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    APPLICATION LAYER                         │
│  ┌─────────────────────┐      ┌─────────────────────┐      │
│  │  WPF Sample App     │      │ Avalonia Sample App │      │
│  └──────────┬──────────┘      └──────────┬──────────┘      │
└─────────────┼──────────────────────────────┼────────────────┘
              │                              │
┌─────────────┼──────────────────────────────┼────────────────┐
│             ▼                              ▼                 │
│  ┌──────────────────┐          ┌──────────────────┐        │
│  │ WpfHexaEditor.Wpf│          │WpfHexaEditor     │        │
│  │                  │          │  .Avalonia       │        │
│  │ - HexEditor.xaml │          │ - HexEditor.axaml│        │
│  │ - HexViewport.cs │          │ - HexViewport.cs │        │
│  │ - WPF Platform   │          │ - Avalonia       │        │
│  │   Implementations│          │   Platform Impl. │        │
│  └────────┬─────────┘          └─────────┬────────┘        │
│           │                              │                  │
│           └──────────────┬───────────────┘                  │
│                          ▼                                   │
│         ┌────────────────────────────────┐                  │
│         │   WpfHexaEditor.Core           │                  │
│         │   (Platform-Agnostic)          │                  │
│         │                                │                  │
│         │ ┌────────────────────────────┐ │                  │
│         │ │ Platform Abstractions      │ │                  │
│         │ │ - IDrawingContext          │ │                  │
│         │ │ - PlatformColor/Brush      │ │                  │
│         │ │ - PlatformKey/Input        │ │                  │
│         │ │ - IPlatformTimer           │ │                  │
│         │ └────────────────────────────┘ │                  │
│         │                                │                  │
│         │ ┌────────────────────────────┐ │                  │
│         │ │ Business Logic (Portable)  │ │                  │
│         │ │ - Core/Bytes (13,050 LOC) │ │                  │
│         │ │ - Services (4,305 LOC)    │ │                  │
│         │ │ - ViewModels (2,500 LOC)  │ │                  │
│         │ │ - Models                   │ │                  │
│         │ │ - Events                   │ │                  │
│         │ └────────────────────────────┘ │                  │
│         └────────────────────────────────┘                  │
│                  SHARED CORE LAYER                           │
└─────────────────────────────────────────────────────────────┘
```

### Key Principle: Minimal Abstraction

**Philosophy:** Abstract only what is strictly necessary to support both platforms, keeping a simple and performant API.

**What we abstract:**
- ✅ Rendering (DrawingContext) - CRITICAL
- ✅ Colors/Brushes - CRITICAL
- ✅ Input (Key/Mouse) - IMPORTANT
- ✅ Timer - IMPORTANT

**What we DON'T abstract:**
- ❌ DependencyProperty / AvaloniaProperty (keep separate)
- ❌ XAML (separate versions .xaml / .axaml)
- ❌ Converters (separate versions or Avalonia replacement)
- ❌ Dialogs (separate implementations)

---

## 📁 Proposed Project Structure

```
WpfHexEditorControl/
├── Sources/
│   │
│   ├── WpfHexaEditor.Core/              # ⭐ NEW - Portable core
│   │   ├── WpfHexaEditor.Core.csproj    (netstandard2.0)
│   │   │
│   │   ├── Platform/                     # Platform abstractions
│   │   │   ├── Rendering/
│   │   │   │   ├── IDrawingContext.cs
│   │   │   │   ├── IFormattedText.cs
│   │   │   │   ├── ITypeface.cs
│   │   │   │   └── PlatformGeometry.cs  (Rect, Point, Size structs)
│   │   │   │
│   │   │   ├── Media/
│   │   │   │   ├── PlatformColor.cs     (struct ARGB)
│   │   │   │   ├── IBrush.cs
│   │   │   │   ├── IPen.cs
│   │   │   │   ├── PlatformSolidColorBrush.cs
│   │   │   │   └── PlatformBrushes.cs   (static common brushes)
│   │   │   │
│   │   │   ├── Input/
│   │   │   │   ├── PlatformKey.cs       (enum)
│   │   │   │   ├── PlatformModifierKeys.cs (flags enum)
│   │   │   │   ├── PlatformMouseButton.cs
│   │   │   │   ├── PlatformKeyEventArgs.cs
│   │   │   │   └── KeyConverter.cs
│   │   │   │
│   │   │   ├── Threading/
│   │   │   │   └── IPlatformTimer.cs
│   │   │   │
│   │   │   └── Controls/
│   │   │       └── PlatformControlBase.cs (optional base)
│   │   │
│   │   ├── Core/                         # ✅ Already portable (moved)
│   │   │   ├── Bytes/
│   │   │   │   ├── BaseByte.cs
│   │   │   │   ├── ByteModified.cs
│   │   │   │   ├── ByteProvider/
│   │   │   │   └── ...
│   │   │   ├── CharacterTable/
│   │   │   │   ├── TBLStream.cs
│   │   │   │   └── DTE.cs
│   │   │   ├── Interfaces/
│   │   │   └── MethodExtension/
│   │   │
│   │   ├── Services/                     # ✅ Already portable (moved)
│   │   │   ├── UndoRedoService.cs
│   │   │   ├── SearchService.cs
│   │   │   ├── SelectionService.cs
│   │   │   └── ...
│   │   │
│   │   ├── ViewModels/                   # ✅ Already portable (moved)
│   │   │   ├── HexEditorViewModel.cs
│   │   │   └── HexBoxViewModel.cs
│   │   │
│   │   ├── Models/                       # ✅ Already portable (moved)
│   │   │   ├── HexLine.cs
│   │   │   ├── ByteData.cs
│   │   │   └── ...
│   │   │
│   │   └── Events/                       # ✅ Already portable (moved)
│   │       ├── ByteModifiedEventArgs.cs
│   │       ├── PositionChangedEventArgs.cs
│   │       └── ...
│   │
│   ├── WpfHexaEditor/                    # ⚠️ RENAMED TO WpfHexaEditor.Wpf
│   │   ├── WpfHexaEditor.Wpf.csproj     (net48;net8.0-windows)
│   │   │   DefineConstants: WPF
│   │   │   UseWPF: true
│   │   │
│   │   ├── Platform/                     # WPF implementations
│   │   │   ├── Rendering/
│   │   │   │   ├── WpfDrawingContext.cs
│   │   │   │   ├── WpfFormattedText.cs
│   │   │   │   └── WpfTypeface.cs
│   │   │   │
│   │   │   ├── Media/
│   │   │   │   ├── WpfBrush.cs
│   │   │   │   └── WpfPen.cs
│   │   │   │
│   │   │   ├── Input/
│   │   │   │   └── WpfKeyConverter.cs
│   │   │   │
│   │   │   └── Threading/
│   │   │       └── WpfDispatcherTimer.cs
│   │   │
│   │   ├── Controls/                     # WPF controls
│   │   │   ├── HexEditor.xaml
│   │   │   ├── HexEditor.xaml.cs         (adapted for abstractions)
│   │   │   ├── HexViewport.cs            (OnRender -> IDrawingContext)
│   │   │   ├── HexBox.xaml
│   │   │   ├── HexBox.xaml.cs
│   │   │   ├── BarChartPanel.cs
│   │   │   ├── ScrollMarkerPanel.cs
│   │   │   └── Caret.cs
│   │   │
│   │   ├── Converters/                   # WPF IValueConverter
│   │   │   ├── BoolToSelectionBrushConverter.cs
│   │   │   └── ...
│   │   │
│   │   ├── Dialog/                       # WPF dialogs
│   │   │   ├── FindReplaceWindow.xaml
│   │   │   └── ...
│   │   │
│   │   └── Commands/                     # WPF commands
│   │       ├── RelayCommand.cs           (#if WPF)
│   │       └── RelayCommand_T.cs
│   │
│   ├── WpfHexaEditor.Avalonia/           # ⭐ NEW - Avalonia version
│   │   ├── WpfHexaEditor.Avalonia.csproj (net8.0)
│   │   │   DefineConstants: AVALONIA
│   │   │   PackageReference: Avalonia 11.0+
│   │   │
│   │   ├── Platform/                     # Avalonia implementations
│   │   │   ├── Rendering/
│   │   │   │   ├── AvaloniaDrawingContext.cs
│   │   │   │   ├── AvaloniaFormattedText.cs
│   │   │   │   └── AvaloniaTypeface.cs
│   │   │   │
│   │   │   ├── Media/
│   │   │   │   ├── AvaloniaBrush.cs
│   │   │   │   └── AvaloniaPen.cs
│   │   │   │
│   │   │   ├── Input/
│   │   │   │   └── AvaloniaKeyConverter.cs
│   │   │   │
│   │   │   └── Threading/
│   │   │       └── AvaloniaDispatcherTimer.cs
│   │   │
│   │   ├── Controls/                     # Avalonia controls
│   │   │   ├── HexEditor.axaml           (Avalonia XAML)
│   │   │   ├── HexEditor.axaml.cs        (adapted code-behind)
│   │   │   ├── HexViewport.cs            (Render -> IDrawingContext)
│   │   │   ├── HexBox.axaml
│   │   │   ├── HexBox.axaml.cs
│   │   │   ├── BarChartPanel.cs
│   │   │   ├── ScrollMarkerPanel.cs
│   │   │   └── Caret.cs
│   │   │
│   │   ├── Converters/                   # Avalonia converters
│   │   │   ├── BoolToSelectionBrushConverter.cs
│   │   │   └── ...
│   │   │
│   │   ├── Views/                        # Avalonia views
│   │   │   ├── FindReplaceWindow.axaml
│   │   │   └── ...
│   │   │
│   │   └── Commands/                     # Avalonia commands
│   │       ├── RelayCommand.cs           (#if AVALONIA)
│   │       └── RelayCommand_T.cs
│   │
│   └── Samples/
│       ├── WpfHexEditor.Sample.Main/     # ✅ Existing WPF
│       └── AvaloniaHexEditor.Sample/     # ⭐ NEW - Avalonia sample
│
└── AVALONIA_PORTING_PLAN.md              # This file
```

---

## 🔧 Technical Abstractions in Detail

### 1. Rendering Abstraction (Priority P0)

**Files to create:**

#### `WpfHexaEditor.Core/Platform/Rendering/IDrawingContext.cs`
```csharp
namespace WpfHexaEditor.Platform.Rendering
{
    public interface IDrawingContext : IDisposable
    {
        void DrawRectangle(IBrush brush, IPen pen, PlatformRect rect);
        void DrawText(IFormattedText text, PlatformPoint origin);
        void DrawLine(IPen pen, PlatformPoint start, PlatformPoint end);
        void DrawEllipse(IBrush brush, IPen pen, PlatformPoint center, double radiusX, double radiusY);
    }

    public interface IFormattedText
    {
        string Text { get; }
        double Width { get; }
        double Height { get; }
    }
}
```

#### `WpfHexaEditor.Core/Platform/Rendering/PlatformGeometry.cs`
```csharp
namespace WpfHexaEditor.Platform.Rendering
{
    public struct PlatformRect
    {
        public double X, Y, Width, Height;
        public PlatformRect(double x, double y, double width, double height)
        { X = x; Y = y; Width = width; Height = height; }
    }

    public struct PlatformPoint
    {
        public double X, Y;
        public PlatformPoint(double x, double y) { X = x; Y = y; }
    }

    public struct PlatformSize
    {
        public double Width, Height;
        public PlatformSize(double width, double height) { Width = width; Height = height; }
    }
}
```

**Impact:**
- 10 files to modify (HexViewport, BarChartPanel, Caret, etc.)
- ~500 lines of rendering code to adapt
- Performance: Negligible overhead (<2ns per call)

---

### 2. Color/Brush Abstraction (Priority P0)

#### `WpfHexaEditor.Core/Platform/Media/PlatformColor.cs`
```csharp
namespace WpfHexaEditor.Platform.Media
{
    public struct PlatformColor : IEquatable<PlatformColor>
    {
        public byte A, R, G, B;

        public static PlatformColor FromArgb(byte a, byte r, byte g, byte b)
            => new() { A = a, R = r, G = g, B = b };

        public static PlatformColor FromRgb(byte r, byte g, byte b)
            => FromArgb(255, r, g, b);

        // Implicit conversions
#if WPF
        public static implicit operator System.Windows.Media.Color(PlatformColor c)
            => System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);
#elif AVALONIA
        public static implicit operator Avalonia.Media.Color(PlatformColor c)
            => Avalonia.Media.Color.FromArgb(c.A, c.R, c.G, c.B);
#endif
    }

    public interface IBrush
    {
        bool IsFrozen { get; }
        void Freeze();
    }

    public class PlatformSolidColorBrush : IBrush
    {
        private static readonly Dictionary<PlatformColor, PlatformSolidColorBrush> _cache = new();

        public PlatformColor Color { get; }
        public bool IsFrozen { get; private set; }

        // Implementation with cache for performance
        public static PlatformSolidColorBrush GetFrozen(PlatformColor color)
        {
            if (_cache.TryGetValue(color, out var cached))
                return cached;

            var brush = new PlatformSolidColorBrush(color);
            brush.Freeze();
            _cache[color] = brush;
            return brush;
        }
    }
}
```

**Impact:**
- ~200 Color/Brush usages to migrate
- Performance: Improvement thanks to frozen brush cache

---

### 3. Input Abstraction (Priority P1)

#### `WpfHexaEditor.Core/Platform/Input/PlatformKey.cs`
```csharp
namespace WpfHexaEditor.Platform.Input
{
    public enum PlatformKey
    {
        None = 0,
        A, B, C, D, E, F, G, H, I, J, K, L, M,
        N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
        Left, Right, Up, Down,
        Home, End, PageUp, PageDown,
        Enter, Escape, Tab, Back, Delete, Space,
        F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12
    }

    [Flags]
    public enum PlatformModifierKeys
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Windows = 8
    }

    public static class KeyConverter
    {
#if WPF
        public static PlatformKey ToPlatformKey(System.Windows.Input.Key key)
        {
            return key switch
            {
                System.Windows.Input.Key.A => PlatformKey.A,
                System.Windows.Input.Key.Delete => PlatformKey.Delete,
                // ... complete mapping
                _ => PlatformKey.None
            };
        }
#elif AVALONIA
        public static PlatformKey ToPlatformKey(Avalonia.Input.Key key)
        {
            return key switch
            {
                Avalonia.Input.Key.A => PlatformKey.A,
                Avalonia.Input.Key.Delete => PlatformKey.Delete,
                // ... complete mapping
                _ => PlatformKey.None
            };
        }
#endif
    }
}
```

**Impact:**
- ~50 event handlers to adapt
- Pattern: Convert to PlatformKey at entry point, business logic remains identical

---

### 4. Timer Abstraction (Priority P1)

#### `WpfHexaEditor.Core/Platform/Threading/IPlatformTimer.cs`
```csharp
namespace WpfHexaEditor.Platform.Threading
{
    public interface IPlatformTimer : IDisposable
    {
        TimeSpan Interval { get; set; }
        bool IsEnabled { get; set; }

        event EventHandler Tick;

        void Start();
        void Stop();
    }

    public static class PlatformTimer
    {
        public static IPlatformTimer Create() =>
#if WPF
            new WpfDispatcherTimer();
#elif AVALONIA
            new AvaloniaDispatcherTimer();
#endif
    }
}
```

**Impact:**
- 2 usages (HexEditor auto-scroll, Caret blinking)
- Simple migration

---

## 📅 Implementation Plan by Phases

### **Phase 1: Preparation and Infrastructure (Week 1-2)**

**Objective:** Create project structure and base abstractions

#### Tasks:
1. **Create WpfHexaEditor.Core project**
   - Create WpfHexaEditor.Core.csproj (netstandard2.0)
   - Configure solution for multi-targeting

2. **Move portable code to Core**
   - Move Core/Bytes/ (13,050 lines)
   - Move Services/ (4,305 lines)
   - Move ViewModels/ (2,500 lines)
   - Move Models/ and Events/
   - Update namespaces
   - Resolve circular dependencies

3. **Create Platform abstractions**
   - Create Platform/Rendering/
     - IDrawingContext.cs
     - IFormattedText.cs
     - PlatformGeometry.cs (Rect, Point, Size)

   - Create Platform/Media/
     - PlatformColor.cs
     - IBrush.cs, IPen.cs
     - PlatformSolidColorBrush.cs
     - PlatformBrushes.cs (static helpers)

   - Create Platform/Input/
     - PlatformKey.cs (enum)
     - PlatformModifierKeys.cs
     - KeyConverter.cs

   - Create Platform/Threading/
     - IPlatformTimer.cs
     - PlatformTimer.cs (factory)

4. **Compilation tests**
   - Verify WpfHexaEditor.Core compiles without errors
   - Document created abstractions

**Deliverables:**
- ✅ WpfHexaEditor.Core project created and compilable
- ✅ ~20,000 lines of portable code isolated
- ✅ 4 Platform abstractions defined
- ✅ Interface documentation

---

### **Phase 2: WPF Implementation of Abstractions (Week 3)**

**Objective:** Adapt existing WPF version to use abstractions

#### Tasks:
1. **Rename WPF project**
   - Rename WpfHexaEditor → WpfHexaEditor.Wpf
   - Add reference to WpfHexaEditor.Core
   - Add DefineConstants: WPF

2. **Implement Platform/Rendering for WPF**
   - WpfDrawingContext.cs (DrawingContext wrapper)
   - WpfFormattedText.cs (FormattedText wrapper)
   - WpfTypeface.cs

3. **Implement Platform/Media for WPF**
   - WpfBrush.cs (SolidColorBrush wrapper)
   - WpfPen.cs (Pen wrapper)
   - Test implicit conversions PlatformColor ↔ System.Windows.Media.Color

4. **Implement Platform/Input for WPF**
   - WpfKeyConverter.cs (Key → PlatformKey mapping)
   - WpfModifierKeysHelper.cs

5. **Implement Platform/Threading for WPF**
   - WpfDispatcherTimer.cs (DispatcherTimer wrapper)

**Deliverables:**
- ✅ 5 WPF implementations of Platform abstractions
- ✅ All wrappers unit tested
- ✅ Performance baseline measured

---

### **Phase 3: Migrate WPF Controls to Abstractions (Week 4-5)**

**Objective:** Adapt WPF controls to use abstractions instead of direct WPF APIs

#### Tasks (by priority):

1. **Migrate HexViewport.cs** (CRITICAL)
   - File: `Controls/HexViewport.cs` (535 lines OnRender)
   - Modify `OnRender(DrawingContext dc)` to wrap to `IDrawingContext`
   - Replace all `dc.DrawRectangle()` with abstract versions
   - Replace `System.Windows.Media.Color` with `PlatformColor`
   - Replace `SolidColorBrush` with `PlatformSolidColorBrush`
   - Test rendering (visual validation)

2. **Migrate BarChartPanel.cs**
   - File: `Controls/BarChartPanel.cs` (171 lines OnRender)
   - Adapt OnRender to IDrawingContext
   - Test frequency histogram

3. **Migrate Caret.cs**
   - File: `Core/Caret.cs` (264 lines OnRender)
   - Adapt OnRender to IDrawingContext
   - Migrate DispatcherTimer to IPlatformTimer
   - Test blinking

4. **Migrate ScrollMarkerPanel.cs**
   - File: `Controls/ScrollMarkerPanel.cs` (291 lines)
   - Adapt OnRender to IDrawingContext

5. **Migrate HexEditor.xaml.cs** (COMPLEX)
   - File: `HexEditor.xaml.cs` (1,500+ lines)
   - Migrate auto-scroll DispatcherTimer to IPlatformTimer
   - Adapt keyboard event handlers (KeyEventArgs → PlatformKeyEventArgs)
   - Keep DependencyProperty as-is (no abstraction)
   - Test all features

6. **Migrate HexBox.xaml.cs**
   - File: `Controls/HexBox.xaml.cs` (150 lines)
   - Adapt event handlers
   - Keep DependencyProperty

7. **Migrate BaseByte.cs, FastTextLine.cs, StringByte.cs**
   - These classes also have OnRender to migrate

8. **Adapt RelayCommand.cs**
   - File: `Commands/RelayCommand.cs`
   - Add `#if WPF` for CommandManager.RequerySuggested
   - Prepare Avalonia version (manual event)

**Tests:**
- ✅ Complete visual validation of WPF interface
- ✅ Performance tests (rendering, scrolling, selection)
- ✅ Functional tests (editing, search, undo/redo)
- ✅ No regression from original version

**Deliverables:**
- ✅ 100% functional WPF version using abstractions
- ✅ 0 feature regression
- ✅ Identical or better performance

---

### **Phase 4: Create Avalonia Version (Week 6-7)**

**Objective:** Create Avalonia project and implement Platform abstractions

#### Tasks:

1. **Create WpfHexaEditor.Avalonia project**
   - Create WpfHexaEditor.Avalonia.csproj (net8.0)
   - Add PackageReference Avalonia 11.0+
   - Add DefineConstants: AVALONIA
   - Add reference to WpfHexaEditor.Core

2. **Implement Platform/Rendering for Avalonia**
   - AvaloniaDrawingContext.cs (Avalonia.Media.DrawingContext wrapper)
   - AvaloniaFormattedText.cs (Avalonia.Media.FormattedText wrapper)
   - AvaloniaTypeface.cs
   - **Note:** Avalonia DrawingContext.Dispose() is required

3. **Implement Platform/Media for Avalonia**
   - AvaloniaBrush.cs (Avalonia.Media.ISolidColorBrush wrapper)
   - AvaloniaPen.cs (Avalonia.Media.Pen wrapper)
   - Test PlatformColor ↔ Avalonia.Media.Color conversions

4. **Implement Platform/Input for Avalonia**
   - AvaloniaKeyConverter.cs (Avalonia.Input.Key → PlatformKey mapping)
   - AvaloniaModifierKeysHelper.cs

5. **Implement Platform/Threading for Avalonia**
   - AvaloniaDispatcherTimer.cs (Avalonia.Threading.DispatcherTimer wrapper)

**Deliverables:**
- ✅ WpfHexaEditor.Avalonia project created
- ✅ 5 Avalonia implementations of Platform abstractions
- ✅ Unit tests for implementations

---

### **Phase 5: Port Controls to Avalonia (Week 7-8)**

**Objective:** Port WPF controls to Avalonia

#### Tasks:

1. **Port HexViewport.cs to Avalonia**
   - Copy `HexViewport.cs` to WpfHexaEditor.Avalonia/Controls/
   - Change `FrameworkElement` → `Avalonia.Controls.Control`
   - Change `OnRender()` → `Render()`
   - OnRenderCore() code using IDrawingContext is already portable!
   - Test basic rendering

2. **Port BarChartPanel.cs**
   - Copy and adapt class
   - Test histogram

3. **Port Caret.cs**
   - Copy and adapt
   - Test blinking

4. **Port ScrollMarkerPanel.cs**
   - Copy and adapt

5. **Create HexEditor.axaml (Avalonia XAML)**
   - Create `Controls/HexEditor.axaml`
   - Adapt WPF XAML to Avalonia syntax
   - Main differences:
     - `xmlns:av="https://github.com/avaloniaui"`
     - `Window.Resources` → `Window.Styles`
     - `Style TargetType` different syntax
     - No `x:Static` (use resources)

6. **Port HexEditor.axaml.cs**
   - Copy `HexEditor.xaml.cs` → `HexEditor.axaml.cs`
   - Replace `DependencyProperty` with Avalonia `StyledProperty<T>`
   - Different syntax:
     ```csharp
     // WPF
     public static readonly DependencyProperty AllowZoomProperty =
         DependencyProperty.Register(nameof(AllowZoom), typeof(bool), ...);

     // Avalonia
     public static readonly StyledProperty<bool> AllowZoomProperty =
         AvaloniaProperty.Register<HexEditor, bool>(nameof(AllowZoom), defaultValue: true);
     ```
   - Adapt FindName() → FindControl<T>()
   - Rest of code using abstractions works as-is!

7. **Port HexBox.axaml + HexBox.axaml.cs**
   - Create Avalonia files
   - Adapt DependencyProperty → StyledProperty

8. **Port Converters**
   - Avalonia has similar `IValueConverter`
   - Copy and adapt 15 converters
   - Adjust TryFindResource if needed

9. **Port Dialogs**
   - Create FindReplaceWindow.axaml
   - Create GotoWindow.axaml
   - Adapt code-behind

**Deliverables:**
- ✅ Functional HexEditor control under Avalonia
- ✅ All core features operational
- ✅ Correct rendering on Windows/Linux/macOS

---

### **Phase 6: Create Avalonia Sample and Testing (Week 8)**

**Objective:** Create Avalonia demo application and test the port

#### Tasks:

1. **Create AvaloniaHexEditor.Sample**
   - Create Avalonia Desktop Application project
   - Add reference to WpfHexaEditor.Avalonia
   - Create MainWindow.axaml with HexEditor control

2. **Multi-platform tests**
   - Test on Windows 11
   - Test on Ubuntu 22.04 / Fedora
   - Test on macOS (if available)
   - Verify rendering on each platform

3. **Complete functional tests**
   - Open/edit files
   - Search and replace
   - Multi-byte selection
   - Undo/Redo
   - Copy/paste
   - Themes (if supported)
   - Performance on large files (>100 MB)

4. **Documentation**
   - Create AVALONIA_USAGE.md
   - Document differences between WPF and Avalonia
   - Integration examples

**Deliverables:**
- ✅ Functional Avalonia sample
- ✅ Tests passed on 2+ platforms
- ✅ User documentation

---

### **Phase 7: Polish and Documentation (Week 9)**

**Objective:** Finalize project, document and prepare release

#### Tasks:

1. **Performance tuning**
   - Profile Avalonia vs WPF
   - Optimize hot spots
   - Measure and compare metrics

2. **Theme management**
   - Adapt existing themes for Avalonia
   - Test Cyberpunk, Light, Dark, etc.

3. **NuGet packaging**
   - Create WpfHexaEditor.Core.nupkg
   - Create WpfHexaEditor.Wpf.nupkg
   - Create WpfHexaEditor.Avalonia.nupkg
   - Configure multi-targeting

4. **Complete documentation**
   - Updated README.md
   - MIGRATION_GUIDE.md
   - API documentation
   - CHANGELOG.md

5. **CI/CD**
   - Configure GitHub Actions for multi-platform build
   - Automated WPF and Avalonia tests
   - Automatic NuGet publishing

**Deliverables:**
- ✅ 3 NuGet packages published
- ✅ Complete documentation
- ✅ CI/CD configured
- ✅ Release v3.0.0 with Avalonia support

---

## 📊 Critical Files to Modify

### Priority P0 (Blocking if not done)

| File | Lines | Description | Changes |
|------|-------|-------------|---------|
| `Controls/HexViewport.cs` | 535 | Main viewport with custom rendering | OnRender → IDrawingContext (150 LOC modified) |
| `Controls/BarChartPanel.cs` | 171 | Frequency histogram | OnRender → IDrawingContext (50 LOC) |
| `Core/Caret.cs` | 264 | Blinking cursor | OnRender + Timer (40 LOC) |
| `Controls/ScrollMarkerPanel.cs` | 291 | Search markers | OnRender (30 LOC) |

### Priority P1 (Important)

| File | Lines | Description | Changes |
|------|-------|-------------|---------|
| `HexEditor.xaml.cs` | 1,500+ | Main control | Timer, Input events (100 LOC) |
| `Controls/HexBox.xaml.cs` | 150 | Hex spinner | Input events (20 LOC) |
| `Commands/RelayCommand.cs` | 100 | Command pattern | #if WPF/AVALONIA (10 LOC) |

### Priority P2 (Nice to have)

| File | Lines | Description | Changes |
|------|-------|-------------|---------|
| `Converters/*.cs` | ~800 | 15+ value converters | Copy and adapt for Avalonia |
| `Dialog/*.xaml.cs` | ~3,000 | Dialog windows | Recreate in Avalonia |
| `Core/BaseByte.cs` | ~200 | Custom OnRender | OnRender → IDrawingContext (30 LOC) |

**Total estimated modifications: ~500 lines of abstraction code + ~430 lines of adaptation = ~1,000 lines modified out of 40,940 (~2.5%)**

---

## ⚠️ Risks and Mitigation

### Risk 1: Rendering differences between WPF and Avalonia
**Impact:** MEDIUM
**Probability:** HIGH

**Description:** Avalonia and WPF have subtle rendering differences (anti-aliasing, subpixel rendering, font metrics)

**Mitigation:**
- Systematic visual tests on each platform
- Reference screenshots for comparison
- Positioning adjustments if needed (epsilon tolerance)

---

### Risk 2: Degraded performance on Avalonia
**Impact:** HIGH
**Probability:** MEDIUM

**Description:** Avalonia could be slower than WPF for intensive custom rendering

**Mitigation:**
- Early profiling (Phase 4)
- Targeted optimizations (caching, frozen brushes)
- Fallback to simplified rendering if necessary
- Performance tests on large files (>100 MB)

---

### Risk 3: Complexity of maintaining two codebases
**Impact:** MEDIUM
**Probability:** HIGH

**Description:** Maintaining WPF and Avalonia in parallel doubles test surface

**Mitigation:**
- Maximize shared code in Core (~85%)
- Automated tests for both platforms
- CI/CD building and testing both versions
- Clear documentation of differences

---

### Risk 4: Avalonia breaking changes
**Impact:** LOW
**Probability:** LOW

**Description:** Avalonia 11 → 12 could introduce breaking changes

**Mitigation:**
- Use Avalonia 11 LTS (stable)
- Lock versions in .csproj
- Follow Avalonia release notes

---

### Risk 5: DependencyProperty vs StyledProperty incompatibilities
**Impact:** MEDIUM
**Probability:** MEDIUM

**Description:** Some WPF patterns (coercion, metadata flags) have no direct Avalonia equivalent

**Mitigation:**
- Do NOT abstract properties (keep separate)
- Manually implement coercion logic in setters
- Document differences

---

## 📈 Success Metrics

### Validation Criteria

| Criterion | Target | Measure |
|-----------|--------|---------|
| **Shared code** | >80% | 85% of code in Core |
| **WPF performance** | 100% baseline | No regression |
| **Avalonia performance** | >80% of WPF | Scrolling, rendering |
| **Feature coverage** | 100% | All features ported |
| **Supported platforms** | 3 | Windows, Linux, macOS |
| **Automated tests** | >70% coverage | Core + services |
| **Documentation** | Complete | README, guides, API docs |

---

## 🎯 Conclusion and Next Steps

### Summary

This plan proposes a **progressive and pragmatic approach** to port WPFHexaEditor to Avalonia:

1. ✅ **85% of code is already portable** (Core, Services, ViewModels)
2. ✅ **Minimal abstractions** for rendering, colors, input, timer
3. ✅ **WPF remains the reference version** (no regression)
4. ✅ **Avalonia benefits from V2 architecture work** (MVVM, services)
5. ✅ **Realistic timeline**: 8-9 weeks for an experienced developer

### Recommendation

**Approve and start with Phase 1**: Create WpfHexaEditor.Core and move portable code. This is a low-risk step that prepares the ground for the rest.

### Open Questions

1. **Platform priority**: Windows only first, or Linux/macOS immediately?
2. **.NET Framework compatibility**: Keep net48 for WPF or migrate to net8.0 only?
3. **Breaking changes**: Accept breaking changes in public API to simplify?
4. **Avalonia themes**: Port all existing themes or only Light/Dark?

---

## 📚 References

- [Avalonia Documentation](https://docs.avaloniaui.net/)
- [Avalonia Samples](https://github.com/AvaloniaUI/Avalonia.Samples)
- [WPF to Avalonia Migration Guide](https://docs.avaloniaui.net/docs/next/guides/migration-from-wpf)
- [Avalonia Performance Tips](https://docs.avaloniaui.net/docs/guides/optimization)

---

**Document created:** 2026-02-16
**Version:** 1.0
**Author:** Plan generated by Claude Code
**Status:** 🟡 Awaiting approval
**Related Issues:** #118, #135

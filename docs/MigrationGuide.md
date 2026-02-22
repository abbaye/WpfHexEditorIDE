# Migration Guide: V1 → V2

---
> **📚 HISTORICAL DOCUMENT**
>
> This document describes the V1→V2 migration that was completed in February 2026.
> As of **v2.6.0 (Feb 2026)**, the V1 Legacy code has been **completely removed** (17,093 LOC deleted).
>
> **Current Status:** All projects now use the V2 architecture under the name `HexEditor`.
>
> **IMPORTANT:** "V2" refers to the **architecture version**, not a class name. The control class
> is named `HexEditor` (in HexEditor.xaml/HexEditor.xaml.cs).
>
> **Purpose:** This document is kept for historical reference and to help understand the project's evolution.
> If you're starting a new project, simply use `HexEditor` - no migration needed!
---

## Overview

The V2 architecture (implemented in the HexEditor class) maintained 100% backward compatibility with V1 while providing modern architecture and significant performance improvements. This guide documented the migration process for existing V1 applications.

## Quick Summary

✅ **V1 code worked without changes with V2 architecture**
✅ **99% performance improvement (custom rendering)**
✅ **Native insert mode support**
✅ **Modern MVVM architecture**
✅ **All V1 features preserved**

## Migration Strategies

### Strategy 1: Drop-in Replacement (Recommended for most)
**Effort**: Minimal
**Benefits**: Immediate performance boost

Simply replace V1 assembly with V2:
1. Remove V1 NuGet package or assembly reference
2. Add V2 package/reference
3. Rebuild – no code changes needed!

### Strategy 2: Gradual Migration
**Effort**: Medium
**Benefits**: Leverage new V2 features over time

1. Replace V1 with V2 (works immediately)
2. Address Obsolete warnings as you modify code
3. Migrate to V2 API one feature at a time
4. Use V2-specific features (insert mode, etc.)

### Strategy 3: Full Rewrite
**Effort**: High
**Benefits**: Pure V2 architecture

For new projects or major refactors:
1. Start with V2 API from beginning
2. Use Color instead of Brush
3. Use bool instead of Visibility
4. Use V2 event model
5. Leverage MVVM data binding

## API Correspondence Table

### Type Differences

| V1 Type | V2 Type | Notes |
|---------|---------|-------|
| `Brush` (colors) | `Color` | Brush wrappers provided for compat |
| `Visibility` | `bool` | Auto-conversion Show* properties |
| `long` (position) | `VirtualPosition` | V2 supports virtual positions for insert mode |

### Property Migration

| V1 Property | V2 Equivalent | Phase |
|-------------|---------------|-------|
| `SelectionFirstColorBrush` | `SelectionFirstColor` (Color) | 1 |
| `SelectionSecondColorBrush` | `SelectionSecondColor` (Color) | 1 |
| `HeaderVisibility` | `ShowHeader` (bool) | 2 |
| `HexDataVisibility` | `ShowHexPanel` (bool) | 2 |
| `StringDataVisibility` | `ShowAscii` (bool) | 2 |
| `StatusBarVisibility` | `ShowStatusBar` (bool) | 2 |
| `LineInfoVisibility` | `ShowOffset` (bool) | 2 |

### Method Migration

| V1 Method | V2 Equivalent | Notes |
|-----------|---------------|-------|
| `FindFirst(string)` | `FindFirst(string)` | V2 adds byte[] overload |
| `SetPosition(string hex)` | `SetPosition(string hex)` | V2 adds long overload |
| `SubmitChanges()` | `Save()` | Alias provided for compat |
| `UnSelectAll(bool)` | `ClearSelection()` | V1 method still works |
| `ClearAllChange()` | `ClearUndoRedo()` | ViewModel method |
| `UpdateVisual()` | `InvalidateVisual()` | WPF standard |

### Event Migration

| V1 Event | V2 Event | Notes |
|----------|----------|-------|
| `SelectionStartChanged` | `SelectionChanged` | V2 consolidates selection events |
| `SelectionStopChanged` | `SelectionChanged` | Single event for all selection changes |
| `ChangesSubmited` | `FileClosed` | Name clarified |
| `Undone` | `UndoCompleted` | Name clarified |
| `Redone` | `RedoCompleted` | Name clarified |

## Common Migration Scenarios

### Scenario 1: Color Properties

**V1 Code:**
```csharp
hexEditor.SelectionFirstColorBrush = Brushes.Blue;
```

**V2 Code (recommended):**
```csharp
hexEditor.SelectionFirstColor = Colors.Blue;
```

**V2 Code (V1-compatible):**
```csharp
// Still works, but generates Obsolete warning
hexEditor.SelectionFirstColorBrush = Brushes.Blue;
```

### Scenario 2: Visibility Properties

**V1 Code:**
```csharp
hexEditor.HeaderVisibility = Visibility.Visible;
hexEditor.StatusBarVisibility = Visibility.Collapsed;
```

**V2 Code:**
```csharp
hexEditor.ShowHeader = true;
hexEditor.ShowStatusBar = false;
```

### Scenario 3: Search Operations

**V1 Code:**
```csharp
long position = hexEditor.FindFirst("48656C6C6F"); // Hex string
```

**V2 Code (byte array):**
```csharp
byte[] pattern = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F };
long position = hexEditor.FindFirst(pattern);
```

**V2 Code (string - V1 compatible):**
```csharp
// Still works with string
long position = hexEditor.FindFirst("48656C6C6F");
```

### Scenario 4: Events

**V1 Code:**
```csharp
hexEditor.SelectionStartChanged += OnSelectionStartChanged;
hexEditor.SelectionStopChanged += OnSelectionStopChanged;
```

**V2 Code (consolidated):**
```csharp
hexEditor.SelectionChanged += (s, e) => {
    Console.WriteLine($"Selection: {e.Start}-{e.Stop}");
};
```

**V2 Code (V1-compatible):**
```csharp
// V1 events still fire for compatibility
hexEditor.SelectionStartChanged += OnSelectionStartChanged;
hexEditor.SelectionStopChanged += OnSelectionStopChanged;
```

### Scenario 5: File Operations

**V1 Code:**
```csharp
hexEditor.SubmitChanges(); // Save file
```

**V2 Code:**
```csharp
hexEditor.Save(); // Clearer naming
```

**V2 Code (V1-compatible):**
```csharp
hexEditor.SubmitChanges(); // Still works
```

## XAML Data Binding

### V1 Style (Manual Updates)
```xml
<wpfHexEditor:HexEditor x:Name="HexEditor" />
```
```csharp
// Code-behind
HexEditor.OpenFile(path);
UpdateStatusBar(); // Manual UI update
```

### V2 Style (MVVM Binding)
```xml
<hex:HexEditor
    FileName="{Binding FilePath}"
    IsModified="{Binding IsModified, Mode=OneWayToSource}"
    Position="{Binding CurrentPosition, Mode=TwoWay}"
    SelectionStart="{Binding SelectionStart}"
    ReadOnlyMode="{Binding IsReadOnly}" />
```

Phase 8 DependencyProperties enable true XAML binding scenarios.

## New V2 Features

### 1. Native Insert Mode
```csharp
hexEditor.EditMode = EditMode.Insert; // V2-only feature
// Insert bytes without overwriting
hexEditor.InsertByte(0xFF, position);
```

### 2. Custom Background Blocks (Phase 7)
```csharp
var block = new CustomBackgroundBlock(0, 100, Brushes.Yellow, "Header");
hexEditor.AddCustomBackgroundBlock(block);
```

### 3. File Comparison (Phase 7)
```csharp
var differences = hexEditor1.Compare(hexEditor2);
foreach (var diff in differences) {
    Console.WriteLine($"Position {diff.BytePositionInStream}: {diff.Origine:X2} vs {diff.Destination:X2}");
}
```

### 4. State Persistence (Phase 7)
```csharp
// Save editor state (position, selection, bookmarks, etc.)
hexEditor.SaveCurrentState("editor-state.xml");

// Restore state later
hexEditor.LoadCurrentState("editor-state.xml");
```

### 5. TBL Advanced Features (Phase 7)
```csharp
hexEditor.TblShowMte = true;
hexEditor.TblDteColor = Colors.Yellow;
hexEditor.TblMteColor = Colors.LightBlue;
```

## Performance Comparison

| Operation | V1 | V2 | Improvement |
|-----------|----|----|-------------|
| Render 1000 lines | 450ms | 5ms | **99%** faster |
| Scroll through 100MB file | Laggy | Smooth | **Significant** |
| Insert mode | Not supported | Native | **New feature** |
| Selection updates | 50ms | 1ms | **98%** faster |

## Breaking Changes

✅ **None!** V2 is 100% backward compatible with V1 API.

However, **new code** should prefer V2 API:
- Use `Color` instead of `Brush` for colors
- Use `bool` instead of `Visibility` for show/hide
- Use consolidated events instead of granular V1 events
- Use `VirtualPosition` for insert mode scenarios

## Troubleshooting

### Issue: Obsolete warnings

**Cause**: Using V1-compatible API with Obsolete attribute
**Solution**: Migrate to V2 equivalent (see table above)
**Workaround**: Suppress warnings if migration is not immediate

### Issue: Performance not improved

**Cause**: Using legacy TextBlock rendering instead of modern DrawingContext
**Solution**: Ensure you're using the modern HexEditor control with V2 architecture
**Check**: Modern version uses `HexViewport` custom control

### Issue: Insert mode not working

**Cause**: V1 does not support insert mode
**Solution**: Only V2 supports native insert mode
**Verify**: `EditMode.Insert` is a V2-only feature

## Migration Checklist

- [ ] Replace V1 assembly with V2
- [ ] Build and verify existing functionality works
- [ ] Run tests to verify no regressions
- [ ] Address Obsolete warnings (optional)
- [ ] Consider enabling V2-only features (insert mode, etc.)
- [ ] Update documentation for new features
- [ ] Benchmark to verify performance improvements
- [ ] Gradually migrate to V2 API over time

## Need Help?

- **Documentation**: [Architecture.md](Architecture.md), [ApiReference.md](ApiReference.md)
- **Examples**: See `Samples/` directory for V1 samples running on V2
- **Issues**: [GitHub Issues](https://github.com/abbaye/WpfHexEditorControl/issues)

## Summary

The V2 architecture provided:
- ✅ 100% V1 compatibility (no code changes needed)
- ✅ 99% performance improvement
- ✅ Native insert mode
- ✅ Modern MVVM architecture
- ✅ Path to gradually adopt V2 features

**Historical Recommendation**: Users could start with drop-in replacement, then gradually migrate to V2 API as they touched related code.

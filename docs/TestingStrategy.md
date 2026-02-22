# Testing Strategy - Phase 11

## Overview

Comprehensive testing strategy for HexEditor, covering V1 compatibility, V2 architecture features, performance benchmarks, and stability tests.

## Test Structure

```
Tests/
├── WPFHexaEditor.Tests/              # Unit tests
│   ├── CompatibilityTests/
│   │   ├── Phase1_TypeCompatibilityTests.cs
│   │   ├── Phase2_VisibilityCompatibilityTests.cs
│   │   ├── Phase3_StringSearchTests.cs
│   │   ├── Phase4_EventCompatibilityTests.cs
│   │   ├── Phase5_ConfigPropertiesTests.cs
│   │   ├── Phase6_V1MethodsTests.cs
│   │   └── Phase7_AdvancedFeaturesTests.cs
│   ├── ViewModelTests/
│   │   ├── HexEditorViewModelTests.cs
│   │   ├── SelectionTests.cs
│   │   ├── EditOperationsTests.cs
│   │   └── VirtualPositionTests.cs
│   ├── ServiceTests/
│   │   ├── UndoRedoServiceTests.cs
│   │   ├── ClipboardServiceTests.cs
│   │   └── SearchServiceTests.cs
│   └── ModelTests/
│       └── VirtualPositionTests.cs
├── WPFHexaEditor.IntegrationTests/   # Integration tests
│   ├── EndToEndTests.cs
│   ├── V1SampleCompatibilityTests.cs
│   └── RegressionTests.cs
└── Benchmarks/
    └── WPFHexaEditor.Benchmarks/     # Performance benchmarks
        ├── RenderingBenchmarks.cs
        ├── OperationBenchmarks.cs
        ├── MemoryBenchmarks.cs
        └── ComparisonBenchmarks.cs
```

## Phase 1-7 Compatibility Tests

### Phase 1: Type Compatibility Tests
**File**: `Phase1_TypeCompatibilityTests.cs`

```csharp
[TestClass]
public class Phase1_TypeCompatibilityTests
{
    [TestMethod]
    public void BrushToColor_Conversion_Works()
    {
        var editor = new HexEditor();
        editor.SelectionFirstColorBrush = Brushes.Blue;
        Assert.AreEqual(Colors.Blue, editor.SelectionFirstColor);
    }

    [TestMethod]
    public void ColorToBrush_Conversion_Works()
    {
        var editor = new HexEditor();
        editor.SelectionFirstColor = Colors.Red;
        var brush = editor.SelectionFirstColorBrush as SolidColorBrush;
        Assert.IsNotNull(brush);
        Assert.AreEqual(Colors.Red, brush.Color);
    }

    [TestMethod]
    public void AllBrushProperties_AreDefined()
    {
        // Test all 11 Brush wrapper properties exist
        var type = typeof(HexEditor);
        Assert.IsNotNull(type.GetProperty("SelectionFirstColorBrush"));
        Assert.IsNotNull(type.GetProperty("SelectionSecondColorBrush"));
        // ... test all 11 properties
    }
}
```

### Phase 2: Visibility Compatibility Tests
**File**: `Phase2_VisibilityCompatibilityTests.cs`

```csharp
[TestClass]
public class Phase2_VisibilityCompatibilityTests
{
    [TestMethod]
    public void HeaderVisibility_MapsToShowHeader()
    {
        var editor = new HexEditor();
        editor.HeaderVisibility = Visibility.Visible;
        Assert.IsTrue(editor.ShowHeader);

        editor.HeaderVisibility = Visibility.Collapsed;
        Assert.IsFalse(editor.ShowHeader);
    }

    [TestMethod]
    public void ShowHeader_MapsToHeaderVisibility()
    {
        var editor = new HexEditor();
        editor.ShowHeader = true;
        Assert.AreEqual(Visibility.Visible, editor.HeaderVisibility);

        editor.ShowHeader = false;
        Assert.AreEqual(Visibility.Collapsed, editor.HeaderVisibility);
    }

    // Similar tests for StatusBar, Offset, Ascii visibility
}
```

### Phase 3: String Search Tests
**File**: `Phase3_StringSearchTests.cs`

```csharp
[TestClass]
public class Phase3_StringSearchTests
{
    [TestMethod]
    public void FindFirst_WithString_FindsPattern()
    {
        var editor = new HexEditor();
        editor.OpenFile("test-file.bin");

        long position = editor.FindFirst("Hello");
        Assert.IsTrue(position >= 0);
    }

    [TestMethod]
    public void FindNext_WithString_FindsNextOccurrence()
    {
        var editor = new HexEditor();
        editor.OpenFile("test-file.bin");

        long first = editor.FindFirst("test");
        long second = editor.FindNext("test");

        Assert.IsTrue(second > first);
    }

    [TestMethod]
    public void ReplaceAll_WithString_ReplacesAllOccurrences()
    {
        var editor = new HexEditor();
        editor.OpenFile("test-file.bin");

        int count = editor.ReplaceAll("old", "new");
        Assert.IsTrue(count > 0);
    }
}
```

### Phase 4: Event Compatibility Tests
**File**: `Phase4_EventCompatibilityTests.cs`

```csharp
[TestClass]
public class Phase4_EventCompatibilityTests
{
    [TestMethod]
    public void SelectionStartChanged_Fires_OnSelectionStartChange()
    {
        var editor = new HexEditor();
        bool eventFired = false;

        editor.SelectionStartChanged += (s, e) => eventFired = true;
        editor.SelectionStart = 100;

        Assert.IsTrue(eventFired);
    }

    [TestMethod]
    public void AllV1Events_AreDefined()
    {
        var type = typeof(HexEditor);
        var events = type.GetEvents();

        // Verify all 20 V1 events exist
        Assert.IsNotNull(type.GetEvent("SelectionStartChanged"));
        Assert.IsNotNull(type.GetEvent("SelectionStopChanged"));
        Assert.IsNotNull(type.GetEvent("DataCopied"));
        // ... test all 20 events
    }

    [TestMethod]
    public void Event_OrderingIsCorrect()
    {
        var editor = new HexEditor();
        var order = new List<string>();

        editor.SelectionStartChanged += (s, e) => order.Add("Start");
        editor.SelectionChanged += (s, e) => order.Add("Changed");

        editor.SelectionStart = 50;

        Assert.AreEqual("Start", order[0]);
        Assert.AreEqual("Changed", order[1]);
    }
}
```

### Phase 5: Configuration Properties Tests
**File**: `Phase5_ConfigPropertiesTests.cs`

```csharp
[TestClass]
public class Phase5_ConfigPropertiesTests
{
    [TestMethod]
    public void AllConfigProperties_HaveDefaultValues()
    {
        var editor = new HexEditor();

        Assert.IsTrue(editor.AllowContextMenu); // default true
        Assert.IsTrue(editor.AllowZoom); // default true
        Assert.AreEqual(MouseWheelSpeed.Normal, editor.MouseWheelSpeed);
        Assert.AreEqual(DataVisualType.Hexadecimal, editor.DataStringVisual);
    }

    [TestMethod]
    public void AllConfigProperties_AreSettable()
    {
        var editor = new HexEditor();

        editor.AllowContextMenu = false;
        Assert.IsFalse(editor.AllowContextMenu);

        editor.MouseWheelSpeed = MouseWheelSpeed.Fast;
        Assert.AreEqual(MouseWheelSpeed.Fast, editor.MouseWheelSpeed);
    }
}
```

### Phase 6: V1 Methods Tests
**File**: `Phase6_V1MethodsTests.cs`

```csharp
[TestClass]
public class Phase6_V1MethodsTests
{
    [TestMethod]
    public void SetPosition_WithHexString_ParsesCorrectly()
    {
        var editor = new HexEditor();
        editor.OpenFile("test.bin");

        editor.SetPosition("0xFF");
        Assert.AreEqual(255, editor.Position);

        editor.SetPosition("0x100");
        Assert.AreEqual(256, editor.Position);
    }

    [TestMethod]
    public void SetPosition_WithByteLength_CreatesSelection()
    {
        var editor = new HexEditor();
        editor.OpenFile("test.bin");

        editor.SetPosition(100, 50); // Start at 100, select 50 bytes

        Assert.AreEqual(100, editor.SelectionStart);
        Assert.AreEqual(149, editor.SelectionStop); // 100 + 50 - 1
        Assert.AreEqual(50, editor.SelectionLength);
    }

    [TestMethod]
    public void SubmitChanges_AliasesCorrectly()
    {
        var editor = new HexEditor();
        editor.OpenFile("test.bin");

        editor.ModifyByte(0xFF, 0);
        editor.SubmitChanges();

        Assert.IsFalse(editor.IsModified);
    }

    [TestMethod]
    public void Undo_WithRepeat_UndoesMultipleOperations()
    {
        var editor = new HexEditor();
        editor.OpenFile("test.bin");

        // Make 5 changes
        for (int i = 0; i < 5; i++)
            editor.ModifyByte((byte)i, i);

        // Undo 3 operations
        editor.Undo(3);

        // Should be able to undo 2 more
        Assert.IsTrue(editor.CanUndo);
    }
}
```

### Phase 7: Advanced Features Tests
**File**: `Phase7_AdvancedFeaturesTests.cs`

```csharp
[TestClass]
public class Phase7_AdvancedFeaturesTests
{
    [TestMethod]
    public void CustomBackgroundBlocks_AddAndRetrieve()
    {
        var editor = new HexEditor();
        var block = new CustomBackgroundBlock(0, 100, Brushes.Yellow);

        editor.AddCustomBackgroundBlock(block);

        var retrieved = editor.GetCustomBackgroundBlock(50);
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(0, retrieved.StartOffset);
        Assert.AreEqual(100, retrieved.Length);
    }

    [TestMethod]
    public void FileComparison_FindsDifferences()
    {
        var editor1 = new HexEditor();
        var editor2 = new HexEditor();

        editor1.OpenFile("file1.bin");
        editor2.OpenFile("file2.bin");

        var diffs = editor1.Compare(editor2).ToList();

        Assert.IsTrue(diffs.Count > 0);
        Assert.IsNotNull(diffs[0].Origine);
        Assert.IsNotNull(diffs[0].Destination);
    }

    [TestMethod]
    public void StatePersistence_SaveAndLoad()
    {
        var editor = new HexEditor();
        editor.OpenFile("test.bin");
        editor.SetPosition(500);
        editor.SelectionStart = 100;
        editor.SelectionStop = 199;

        editor.SaveCurrentState("state.xml");

        var editor2 = new HexEditor();
        editor2.OpenFile("test.bin");
        editor2.LoadCurrentState("state.xml");

        Assert.AreEqual(500, editor2.Position);
        Assert.AreEqual(100, editor2.SelectionStart);
        Assert.AreEqual(199, editor2.SelectionStop);
    }
}
```

## Integration Tests

### End-to-End Tests
**File**: `EndToEndTests.cs`

```csharp
[TestClass]
public class EndToEndTests
{
    [TestMethod]
    public void OpenFile_Edit_Save_Workflow()
    {
        var editor = new HexEditor();
        string tempFile = CreateTempFile();

        editor.OpenFile(tempFile);
        Assert.IsTrue(editor.IsFileLoaded);

        editor.ModifyByte(0xFF, 0);
        Assert.IsTrue(editor.IsModified);

        editor.Save();
        Assert.IsFalse(editor.IsModified);

        // Verify change persisted
        byte[] data = File.ReadAllBytes(tempFile);
        Assert.AreEqual(0xFF, data[0]);
    }

    [TestMethod]
    public void Search_Replace_Undo_Workflow()
    {
        var editor = new HexEditor();
        editor.OpenFile("test.bin");

        long pos = editor.FindFirst("test");
        Assert.IsTrue(pos >= 0);

        editor.ReplaceFirst("test", "done");
        Assert.IsTrue(editor.IsModified);

        editor.Undo();
        Assert.IsFalse(editor.IsModified);
    }
}
```

### V1 Sample Compatibility Tests
**File**: `V1SampleCompatibilityTests.cs`

```csharp
[TestClass]
public class V1SampleCompatibilityTests
{
    [TestMethod]
    public void V1Sample_CSharp_RunsWithoutErrors()
    {
        // Test that V1 C# sample works with V2
        // Load V1 sample XAML, verify it initializes
        // Execute V1 sample operations
        Assert.IsTrue(true); // Placeholder
    }

    [TestMethod]
    public void V1Sample_BinaryFilesDifference_RunsWithoutErrors()
    {
        // Test V1 diff sample
        Assert.IsTrue(true);
    }

    [TestMethod]
    public void V1Sample_InsertByteAnywhere_RunsWithoutErrors()
    {
        // Test V1 insert sample
        Assert.IsTrue(true);
    }
}
```

## Performance Benchmarks

### Rendering Benchmarks
**File**: `RenderingBenchmarks.cs`

```csharp
[TestClass]
public class RenderingBenchmarks
{
    [BenchmarkDotNet.Attributes.Benchmark]
    public void Render_1000_Lines()
    {
        var editor = new HexEditor();
        editor.OpenFile("large-file.bin");
        // Measure rendering time
    }

    [BenchmarkDotNet.Attributes.Benchmark]
    public void Scroll_Through_LargeFile()
    {
        var editor = new HexEditor();
        editor.OpenFile("100mb-file.bin");
        // Scroll through file, measure perf
    }

    [Benchmark]
    [Arguments(100)]
    [Arguments(1000)]
    [Arguments(10000)]
    public void RenderLines(int lineCount)
    {
        // Benchmark different line counts
    }
}
```

### Operation Benchmarks
**File**: `OperationBenchmarks.cs`

```csharp
[TestClass]
public class OperationBenchmarks
{
    [Benchmark]
    public void OpenFile_1MB() => OpenFile("1mb.bin");

    [Benchmark]
    public void OpenFile_10MB() => OpenFile("10mb.bin");

    [Benchmark]
    public void OpenFile_100MB() => OpenFile("100mb.bin");

    [Benchmark]
    public void OpenFile_1GB() => OpenFile("1gb.bin");

    [Benchmark]
    public void Search_FindFirst() {
        var editor = new HexEditor();
        editor.OpenFile("test.bin");
        editor.FindFirst(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F });
    }

    [Benchmark]
    public void InsertByte_1000Times() {
        var editor = new HexEditor();
        editor.OpenFile("test.bin");
        editor.EditMode = EditMode.Insert;
        for (int i = 0; i < 1000; i++)
            editor.InsertByte(0xFF, i);
    }
}
```

## Test Execution

### Unit Tests
```powershell
cd Tests/WPFHexaEditor.Tests
dotnet test
```

### Integration Tests
```powershell
cd Tests/WPFHexaEditor.IntegrationTests
dotnet test
```

### Benchmarks
```powershell
cd Benchmarks/WPFHexaEditor.Benchmarks
dotnet run -c Release
```

## Coverage Goals

- **Unit Tests**: > 80% code coverage
- **Integration Tests**: All V1 samples pass
- **Benchmarks**: Document 90%+ performance improvement over V1

## Continuous Integration

```yaml
# .github/workflows/test.yml
name: Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v2
      - name: Setup .NET
        uses: actions/setup-dotnet@v1
        with:
          dotnet-version: '8.0.x'
      - name: Run Unit Tests
        run: dotnet test Tests/WPFHexaEditor.Tests
      - name: Run Integration Tests
        run: dotnet test Tests/WPFHexaEditor.IntegrationTests
      - name: Run Benchmarks
        run: dotnet run --project Benchmarks/WPFHexaEditor.Benchmarks -c Release
```

## Success Criteria

- ✅ All Phase 1-7 compatibility tests pass
- ✅ All V1 samples run without modification
- ✅ Performance benchmarks show >= 90% improvement
- ✅ No memory leaks detected
- ✅ Code coverage >= 80%
- ✅ 0 critical bugs

## Implementation Notes

This document provides the **testing strategy** and **structure**. Actual test implementation should be done incrementally:

1. Start with Phase 1-2 tests (type/visibility)
2. Add Phase 3-4 tests (search/events)
3. Add Phase 5-7 tests (config/methods/advanced)
4. Add integration tests
5. Add benchmarks last

Total estimated effort for full test suite: 15-20 hours.

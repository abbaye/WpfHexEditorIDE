# WpfHexEditor.BinaryAnalysis

> Binary analysis engine — 40+ data type interpretations, anomaly detection, binary templates, Intel HEX / S-Record support.

[![.NET](https://img.shields.io/badge/.NET-net8.0--windows-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](../../LICENSE)

---

## Architecture

```mermaid
graph TB
    subgraph BA["WpfHexEditor.BinaryAnalysis"]

        subgraph SVC["Services/"]
            DIS["DataInspectorService\n40+ type interpretations\nat caret position"]
            DST["DataStatisticsService\nentropy, byte frequency,\npattern statistics"]
            ADS["AnomalyDetectionService\ndetects suspicious byte patterns\nand structural anomalies"]
            BTC["BinaryTemplateCompiler\ncompiles .whtmpl templates\nto parsed field definitions"]
            IHS["IntelHexService\nread/write Intel HEX .hex\nformat (8-bit/16-bit/32-bit)"]
            SRS["SRecordService\nread/write Motorola S-Record\n(S19/S28/S37)"]
        end

        subgraph MDL["Models/"]
            DII["DataInspectorItem\n(type name, value, bytes used)"]
            FST["FileStatistics\n(entropy, histogram, runs)"]
            BT["BinaryTemplate\n(field definitions)"]
        end
    end

    subgraph CORE["WpfHexEditor.Core"]
        BP["ByteProvider\n(data access)"]
    end

    DIS --> BP
    DST --> BP
    ADS --> BP
    BTC --> BP
    IHS --> BP

    DIS --> DII
    DST --> FST
    BTC --> BT

    style DIS fill:#e3f2fd,stroke:#1976d2,stroke-width:2px
    style ADS fill:#fce4ec,stroke:#c62828,stroke-width:2px
    style BTC fill:#f3e5f5,stroke:#6a1b9a,stroke-width:2px
```

---

## Project Structure

```
WpfHexEditor.BinaryAnalysis/
├── Services/
│   ├── DataInspectorService.cs       ← 40+ type interpretations
│   ├── DataStatisticsService.cs      ← Entropy, frequency analysis
│   ├── AnomalyDetectionService.cs    ← Suspicious pattern detection
│   ├── BinaryTemplateCompiler.cs     ← .whtmpl template engine
│   ├── IntelHexService.cs            ← Intel HEX format
│   └── SRecordService.cs             ← Motorola S-Record format
│
└── Models/
    ├── DataInspectorItem.cs
    ├── FileStatistics.cs
    └── BinaryTemplate.cs
```

---

## DataInspectorService

Interprets the bytes at the current caret position as 40+ data types simultaneously:

```mermaid
flowchart LR
    POS["Caret position\n(byte offset)"] --> DIS["DataInspectorService"]

    DIS --> INT["Integers\nInt8, UInt8\nInt16/32/64 LE+BE\nUInt16/32/64 LE+BE"]
    DIS --> FLT["Floats\nFloat16, Float32, Float64\nBFloat16"]
    DIS --> STR["Strings\nASCII, UTF-8, UTF-16 LE+BE\nLatin-1"]
    DIS --> DATE["Date/Time\nDOS date, Unix epoch\nWindows FILETIME, OLE date"]
    DIS --> MISC["Misc\nGUID, Color (ARGB)\nBCD, VLQ, bit flags"]
```

### Usage

```csharp
var service = new DataInspectorService(byteProvider);

// Get all interpretations at offset 0x100
IReadOnlyList<DataInspectorItem> items = service.Inspect(0x100);

foreach (var item in items)
    Console.WriteLine($"{item.TypeName,-20} {item.ValueString}");

// Output example:
// Int8                 -57
// UInt8                199
// Int16 LE             -14649
// UInt16 LE            50887
// Float32              …
// ASCII                "É…"
// Unix Epoch (32-bit)  1970-01-01 00:02:19 UTC
```

---

## DataStatisticsService

```csharp
var stats = new DataStatisticsService(byteProvider);
FileStatistics result = await stats.AnalyzeAsync();

Console.WriteLine($"Entropy:         {result.Entropy:F4} bits/byte");
Console.WriteLine($"Most common:     0x{result.MostCommonByte:X2} ({result.MostCommonCount}×)");
Console.WriteLine($"Byte histogram:  {result.Histogram.Length} entries");
Console.WriteLine($"Zero runs:       {result.LongestZeroRun} bytes max");
```

---

## AnomalyDetectionService

Detects structural anomalies useful for malware analysis, file corruption detection, and format validation:

```csharp
var anomalies = await anomalyService.DetectAsync();

foreach (var a in anomalies)
    Console.WriteLine($"[{a.Severity}] {a.Description} at 0x{a.Offset:X8}");

// Example output:
// [Warning]  High entropy region (possible encryption/compression) at 0x00001000
// [Warning]  Null padding block (512 bytes) at 0x00004000
// [Info]     Repeated 4-byte pattern detected at 0x00002000
```

---

## BinaryTemplateCompiler

Compiles `.whtmpl` binary template files into parsed field definitions for the ParsedFieldsPanel:

```csharp
var compiler = new BinaryTemplateCompiler();
BinaryTemplate template = compiler.Compile(File.ReadAllText("elf64.whtmpl"));

// Apply to data
var fields = template.Parse(byteProvider, baseOffset: 0);
foreach (var field in fields)
    Console.WriteLine($"{field.Name,-30} {field.ValueString}");
```

---

## Intel HEX / S-Record Support

```csharp
// Read Intel HEX
using var hexService = new IntelHexService();
var segments = hexService.ReadFile("firmware.hex");
foreach (var seg in segments)
    Console.WriteLine($"Segment at 0x{seg.Address:X8}: {seg.Data.Length} bytes");

// Write Intel HEX
hexService.WriteFile("output.hex", segments, IntelHexFormat.I32HEX);

// Read Motorola S-Record
var srecService = new SRecordService();
var records = srecService.ReadFile("program.s19");
```

---

## Integration in the IDE

```mermaid
flowchart TD
    HE["HexEditor\n(caret position)"] --> |CaretPositionChanged| DIS["DataInspectorService"]
    DIS --> |IReadOnlyList&lt;DataInspectorItem&gt;| DIP["DataInspectorPanel\n(WpfHexEditor.Panels.BinaryAnalysis)"]
    DIP --> |displayed in IDE| UI["IDE DataInspector panel"]

    HE --> |ByteProvider| DST["DataStatisticsService"]
    DST --> |FileStatistics| FSP["FileStatisticsPanel"]
```

---

## Dependencies

| Project | Why |
|---------|-----|
| `WpfHexEditor.Core` | `ByteProvider` for all data access |

---

## License

Apache 2.0 — Copyright 2026 Derek Tremblay. See [LICENSE](../../LICENSE).

# whfmt.Formats

**856 binary format definitions** (`.whfmt`) shipped as `AdditionalFiles` — ready to pair with [`whfmt.SourceGenerator`](https://www.nuget.org/packages/whfmt.SourceGenerator) for zero-CLI, compile-time C# parsers.

## Quick start

### 1. Install both packages

```xml
<PackageReference Include="whfmt.Formats"         Version="1.0.0" />
<PackageReference Include="whfmt.SourceGenerator" Version="1.0.0" />
```

That's it. At compile time, every format in this package is passed to `whfmt.SourceGenerator` as an `AdditionalFile` and a strongly-typed C# parser class is emitted automatically.

### 2. Use a generated parser

```csharp
// WhfmtFormats.Images namespace — from Formats/Images/PNG.whfmt
var png = PngParser.ParseFile("photo.png");
Console.WriteLine(png.Width);
Console.WriteLine(png.Height);

// WhfmtFormats.Audio namespace — from Formats/Audio/MP3.whfmt
var mp3 = Mp3Parser.ParseFile("track.mp3");
Console.WriteLine(mp3.SyncWord);
```

## Included categories

| Namespace                    | Category        | Formats |
|------------------------------|-----------------|---------|
| `WhfmtFormats.ThreeD`        | 3D              | ~24     |
| `WhfmtFormats.Archives`      | Archives        | ~35     |
| `WhfmtFormats.Audio`         | Audio           | ~40     |
| `WhfmtFormats.CAD`           | CAD             | ~18     |
| `WhfmtFormats.Certificates`  | Certificates    | ~10     |
| `WhfmtFormats.Crypto`        | Crypto          | ~8      |
| `WhfmtFormats.Data`          | Data            | ~25     |
| `WhfmtFormats.Database`      | Database        | ~15     |
| `WhfmtFormats.Disk`          | Disk            | ~20     |
| `WhfmtFormats.Documents`     | Documents       | ~30     |
| `WhfmtFormats.Executables`   | Executables     | ~20     |
| `WhfmtFormats.Firmware`      | Firmware        | ~15     |
| `WhfmtFormats.Fonts`         | Fonts           | ~12     |
| `WhfmtFormats.GIS`           | GIS             | ~15     |
| `WhfmtFormats.Game`          | Game            | ~40     |
| `WhfmtFormats.Images`        | Images          | ~60     |
| `WhfmtFormats.IoT`           | IoT             | ~10     |
| `WhfmtFormats.MachineLearning` | ML            | ~10     |
| `WhfmtFormats.Medical`       | Medical         | ~15     |
| `WhfmtFormats.Network`       | Network         | ~30     |
| `WhfmtFormats.Programming`   | Programming     | ~25     |
| `WhfmtFormats.RomHacking`    | Rom Hacking     | ~20     |
| `WhfmtFormats.Science`       | Science         | ~15     |
| `WhfmtFormats.Subtitles`     | Subtitles       | ~10     |
| `WhfmtFormats.System`        | System          | ~30     |
| `WhfmtFormats.Text`          | Text            | ~15     |
| `WhfmtFormats.Video`         | Video           | ~40     |
| `WhfmtFormats.Virtualization`| Virtualization  | ~15     |

## Use only specific formats

Don't need all 856 parsers? Exclude categories or individual files:

```xml
<!-- Exclude entire categories -->
<AdditionalFiles Remove="$(NuGetPackageRoot)whfmt.formats\1.0.0\contentFiles\any\any\Formats\Game\**" />

<!-- Or reference whfmt.SourceGenerator without whfmt.Formats and add only what you need -->
<AdditionalFiles Include="path\to\PNG.whfmt">
  <WhfmtNamespace>MyApp.Parsers</WhfmtNamespace>
  <WhfmtClass>PngParser</WhfmtClass>
</AdditionalFiles>
```

## Override namespace or class name

```xml
<AdditionalFiles Update="$(NuGetPackageRoot)whfmt.formats\1.0.0\contentFiles\any\any\Formats\Images\PNG.whfmt">
  <WhfmtNamespace>MyApp.ImageParsers</WhfmtNamespace>
  <WhfmtClass>PngReader</WhfmtClass>
</AdditionalFiles>
```

## Relationship to other whfmt packages

| Package | Role |
|---------|------|
| `whfmt.Formats` | This package — ships `.whfmt` definitions as `AdditionalFiles` |
| `whfmt.SourceGenerator` | Roslyn generator — turns `AdditionalFiles` `.whfmt` into C# parsers |
| `whfmt.FileFormatCatalog` | Runtime catalog — format matching, detection, metadata queries |
| `whfmt.CodeGen` | CLI tool — one-shot generation for F#/Rust/VB or outside MSBuild |

## Requirements

- .NET SDK 6.0 or later
- [`whfmt.SourceGenerator`](https://www.nuget.org/packages/whfmt.SourceGenerator) — required to actually generate parsers from these definitions

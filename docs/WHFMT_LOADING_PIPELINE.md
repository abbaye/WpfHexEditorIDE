# WHFMT Loading Pipeline

> **Scope:** End-to-end lifecycle of a `.whfmt` format definition — from embedded resource to
> in-memory `FormatDefinition` and format detection.  
> **Last updated:** 2026-04-14

---

## 1. What is a `.whfmt` file?

A `.whfmt` file is a **JSONC document** (JSON + comments + trailing commas) that describes a
binary or text file format. It contains:

| Section | Purpose |
|---|---|
| `formatName`, `version`, `category` | Identity metadata |
| `extensions` | Associated file extensions |
| `detection` | Signature rules, entropy hints, text patterns |
| `blocks` | Binary structure layout (optional for text formats) |
| `variables` / `functions` | Script state & custom helpers |
| `VersionDetection`, `VersionedBlocks` | Per-version structure variants |
| `Checksums`, `Assertions` | Integrity & well-formedness constraints |
| `Forensic`, `Navigation`, `Inspector` | Analysis, UX, and tooling metadata |
| `preferredEditor`, `diffMode` | Editor routing hints |

**Validation rules (`IsValid()`):**
- `FormatName` must be non-empty.
- `Detection` must be non-null and internally valid.
- Binary formats (`isTextFormat = false`) **must** declare at least one `Block`.
- Text formats may have zero blocks (identified by extension/signature only).

---

## 2. File Organisation

```
Sources/
└── Core/
    └── WpfHexEditor.Core.Definitions/
        ├── FormatDefinitions/          ← whfmt files (embedded resources)
        │   ├── Archives/
        │   ├── Database/
        │   ├── Images/
        │   ├── Programming/
        │   └── ...
        └── WpfHexEditor.Core.Definitions.csproj
```

All `.whfmt` (and `.grammar`) files under `FormatDefinitions/` are compiled as **manifest
embedded resources** via:

```xml
<ItemGroup>
  <EmbeddedResource Include="FormatDefinitions\**\*.whfmt" />
  <EmbeddedResource Include="FormatDefinitions\**\*.grammar" />
</ItemGroup>
```

Resulting resource key pattern:
```
WpfHexEditor.Definitions.FormatDefinitions.{Category}.{FormatName}.whfmt
```

---

## 3. Complete Loading Pipeline

### 3.1 High-Level Overview

```mermaid
flowchart TD
    A([App Startup]) --> B[MainWindow\nInitializePluginSystemAsync]
    B --> C[FormatCatalogService\nInitialize]

    C --> D[EmbeddedFormatCatalog\nGetAll]
    C --> E[External directory\n*.whfmt on disk]
    C --> F[User AppData\n%APPDATA%\\WpfHexEditor\\FormatDefinitions]

    D --> G[MakeEntries\nScan manifest resources]
    G --> H[LoadHeader / LoadGrammarHeader\nLightweight JSON/XML parse]
    H --> I[(EmbeddedFormatEntry\nFrozenSet — headers only)]

    I --> J[GetJson\nFull JSON — cached]
    E --> J
    F --> J

    J --> K[ImportFromJson\nDeserialize + JSONC guard]
    K --> L{IsValid?}
    L -- Yes --> M[(FormatDefinition\nin memory)]
    L -- No --> N[FormatLoadFailure\nrecorded]

    M --> O[FormatDetectionService\nSetSharedCatalog]
    O --> P([CatalogReady event\nIDE ready])
```

---

### 3.2 Step-by-step Breakdown

#### Step 1 — Header Scan (`EmbeddedFormatCatalog.MakeEntries`)

Triggered **once** (lazy) on first call to `GetAll()`.

```mermaid
sequenceDiagram
    participant App as App (MainWindow)
    participant Cat as EmbeddedFormatCatalog (Singleton)
    participant Asm as Assembly

    App->>Cat: GetAll()
    Cat->>Cat: LazyInitializer.EnsureInitialized
    Cat->>Asm: GetManifestResourceNames()
    Asm-->>Cat: string[] resourceKeys

    loop Each key containing "FormatDefinitions"
        alt ends with .whfmt
            Cat->>Asm: GetManifestResourceStream(key)
            Asm-->>Cat: Stream
            Cat->>Cat: LoadHeader(key)\nJsonDocument.Parse — skip comments
            Cat-->>Cat: EmbeddedFormatEntry (lightweight)
        else ends with .grammar
            Cat->>Asm: GetManifestResourceStream(key)
            Cat->>Cat: LoadGrammarHeader(key)\nXmlReader — name + extensions
            Cat-->>Cat: EmbeddedFormatEntry
        end
    end

    Cat-->>App: IReadOnlySet<EmbeddedFormatEntry> (FrozenSet, sorted)
```

**`EmbeddedFormatEntry` fields loaded at this stage:**

| Field | Source JSON path |
|---|---|
| `Name` | `formatName` |
| `Category` | Extracted from resource key path |
| `Description` | `description` |
| `Extensions` | `extensions[]` |
| `Version` | `version` |
| `Author` | `author` |
| `QualityScore` | `QualityMetrics.CompletenessScore` |
| `PreferredEditor` | `preferredEditor` |
| `IsTextFormat` | `detection.isTextFormat` |
| `HasSyntaxDefinition` | presence of `syntaxDefinition` key |
| `DiffMode` | `diffMode` |

> Block definitions, variables, and functions are **NOT** loaded at this stage.

---

#### Step 2 — JSON Cache (`EmbeddedFormatCatalog.GetJson`)

```mermaid
flowchart LR
    A[GetJson\nresourceKey] --> B{_jsonCache\nhit?}
    B -- Yes --> C[Return cached string]
    B -- No --> D[Lock _jsonCacheLock]
    D --> E{Double-check\ncache}
    E -- Still missing --> F[OpenManifestResourceStream\nReadToEnd]
    F --> G[Store in _jsonCache]
    G --> C
    E -- Now present --> C
```

- Cache scope: **process lifetime** (assembly resources are immutable).
- Thread-safe: double-check locking pattern.
- `PreWarm()` can eagerly fill the cache at startup.

---

#### Step 3 — Deserialization & Validation (`FormatDetectionService.ImportFromJson`)

```mermaid
flowchart TD
    A[ImportFromJson\njson string] --> B[TrimStart span]
    B --> C{Starts with\n'/*' or '//'?}
    C -- Yes --> D[Skip comment block\nadvance pointer]
    D --> C
    C -- No --> E{"First char\n'{' or '['?"}
    E -- No --> F[Return null\nnot JSON]
    E -- Yes --> G[JsonSerializer.Deserialize\nFormatDefinition\nPropertyNameCaseInsensitive\nSkipComments\nAllowTrailingCommas]
    G --> H{IsValid?}
    H -- Yes --> I[Return FormatDefinition]
    H -- No --> J[Return null]
```

> The JSONC comment-skip guard (Step C→D) is critical: many `.whfmt` files begin with a
> `/* header block */`. Without it, the `{` check fails and the entire format is silently
> discarded. See memory: `feedback_whfmt_guard.md`.

---

#### Step 4 — Multi-Source Catalog Assembly (`FormatCatalogService.Initialize`)

```mermaid
flowchart TD
    A[Initialize\nembeddedEntries, externalDir] --> B[Load embedded formats]
    A --> C[Load external directory\n*.whfmt from disk]
    A --> D[Load user AppData\n%APPDATA%\\WpfHexEditor\\FormatDefinitions]

    B --> E{ImportFromJson\n+ IsValid?}
    C --> E
    D --> E

    E -- OK --> F[Add to _formats\nList~FormatDefinition~]
    E -- Fail --> G[Record FormatLoadFailure\nsource + reason]

    F --> H[FormatDetectionService\nSetSharedCatalog\nstatic, locked]
    H --> I[Fire CatalogReady event]
    G --> J[Exposed via LoadFailures\nlogged to OutputPanel]
```

**Source priority:**

| Priority | Source | Override behaviour |
|---|---|---|
| 1 | Embedded resources (DLL) | Base definitions |
| 2 | External directory (parameter) | Can override embedded by name |
| 3 | User AppData | Personal overrides (highest) |

---

## 4. Caching Map

```mermaid
graph LR
    subgraph EmbeddedFormatCatalog[EmbeddedFormatCatalog — Singleton]
        HC[(Header cache\nFrozenSet EmbeddedFormatEntry\nLazy, one-time)]
        JC[(JSON cache\nDictionary string→string\nLazy per key, locked)]
    end

    subgraph FormatDetectionService
        SC[(Shared catalog\nIReadOnlyList FormatDefinition\nStatic, set once at startup)]
    end

    HC -->|feeds| JC
    JC -->|feeds| SC
```

| Cache | Type | Invalidation |
|---|---|---|
| Header entries | `FrozenSet<EmbeddedFormatEntry>` | Never — one-time lazy init |
| JSON content | `Dictionary<string, string>` | Never — assembly immutable |
| Shared catalog | `IReadOnlyList<FormatDefinition>` | Set once at `CatalogReady` |
| Instance formats | `List<FormatDefinition>` per service | `ClearFormats()` |

---

## 5. Format Detection Flow

Once the catalog is ready, detection runs each time a file is opened.

```mermaid
flowchart TD
    A([File opened]) --> B[DetectFormat\nbyte array + fileName]
    B --> C[ContentAnalyzer.Analyze\ntext vs binary, entropy]

    C --> D[TIER 1 — Strong Signatures\nRequired=true, strength≥Medium]
    D --> E{Confidence\n≥ 0.7?}
    E -- Yes --> I
    E -- No --> F[TIER 2 — Text Formats\nisTextFormat=true\npattern + extension match]
    F --> G{Match\nfound?}
    G -- Yes --> I
    G -- No --> H[TIER 3 — Weak/Fallback\nRequired=false, strength≤Weak\nextension-heavy]
    H --> I[ScoreAndRankCandidates]

    I --> J[DecideFormat\nhighest score wins]
    J --> K{Format\nfound?}
    K -- Yes --> L[Open with preferredEditor\nor default HexEditor]
    K -- No --> M[Fallback: HexEditor\nno format]
```

**Scoring weights:**

| Signal | Weight |
|---|---|
| Signature confidence | 30 % |
| Extension match | 30 % (40 % for shared signatures) |
| ZIP-container content analysis | 25 % |
| General content analysis | 20 % |

---

### 5.1 `TryDetectFormat` Inner Logic

```mermaid
sequenceDiagram
    participant D as DetectFormat
    participant F as FormatDefinition
    participant I as FormatScriptInterpreter

    D->>F: Check Detection.Required signature
    alt Signature mismatch
        F-->>D: return false (early exit)
    end

    D->>F: Check EntropyHint (weak signatures only)
    alt Entropy out of range
        F-->>D: return false
    end

    D->>I: new FormatScriptInterpreter(data, variables, byteProvider)
    I->>I: ExecuteFunctions(format.Functions)
    I->>I: ExecuteBlocks(format.Blocks)
    I-->>D: List<CustomBackgroundBlock>, variables

    opt v2.0 Checksums
        D->>D: ChecksumEngine.Execute
    end

    opt v2.0 Assertions
        D->>D: AssertionRunner.Run
    end

    D-->>D: return blocks.Count > 0
```

---

## 6. Class Responsibility Map

```mermaid
classDiagram
    class EmbeddedFormatCatalog {
        <<Singleton>>
        +GetAll() IReadOnlySet~EmbeddedFormatEntry~
        +GetJson(key) string
        +PreWarm() void
        -MakeEntries() FrozenSet
        -LoadHeader(key) EmbeddedFormatEntry?
        -LoadGrammarHeader(key) EmbeddedFormatEntry?
        -_jsonCache Dictionary
    }

    class EmbeddedFormatEntry {
        <<record>>
        +ResourceKey string
        +Name string
        +Category string
        +Extensions IReadOnlyList~string~
        +PreferredEditor string?
        +IsTextFormat bool
        +QualityScore double
    }

    class FormatCatalogService {
        +Initialize(embedded, externalDir) Task
        +LoadFailures IReadOnlyList~FormatLoadFailure~
        +CatalogReady event
        -_formats List~FormatDefinition~
    }

    class FormatDetectionService {
        +ImportFromJson(json) FormatDefinition?
        +DetectFormat(data, file, provider) FormatDetectionResult
        +SetSharedCatalog(formats) void$
        -s_sharedFormats IReadOnlyList$
    }

    class FormatDefinition {
        +FormatName string
        +Extensions string[]
        +Detection DetectionRule
        +Blocks List~BlockDefinition~
        +Variables Dictionary
        +IsValid() bool
    }

    class FormatScriptInterpreter {
        +ExecuteBlocks(blocks) List~Block~
        +ExecuteFunctions(functions) void
        +Variables Dictionary
    }

    EmbeddedFormatCatalog "1" --> "*" EmbeddedFormatEntry : produces
    FormatCatalogService --> EmbeddedFormatCatalog : reads headers + JSON
    FormatCatalogService --> FormatDetectionService : calls ImportFromJson
    FormatCatalogService --> FormatDetectionService : calls SetSharedCatalog
    FormatDetectionService --> FormatDefinition : deserializes
    FormatDetectionService --> FormatScriptInterpreter : instantiates per detection
```

---

## 7. Error Handling

| Stage | Error | Handling |
|---|---|---|
| `MakeEntries` | Parse exception on header | Entry skipped silently; next entry continues |
| `ImportFromJson` | Invalid JSON / non-JSON content | Returns `null`; not treated as failure |
| `ImportFromJson` | `IsValid()` = false | Returns `null` |
| `FormatCatalogService.Initialize` | File I/O or parse exception | `FormatLoadFailure` recorded + logged |
| `FormatCatalogService.Initialize` | `IsValid()` = false (external) | `FormatLoadFailure` recorded |
| `DetectFormat` | No match | Returns `null`; hex editor fallback |
| `DetectFormat` | Timeout (3 s) | Best candidate returned |

**User-visible surfaces:**
- **OutputPanel** — `OutputLogger.Error/Warn` for each `FormatLoadFailure`.
- **Startup is never blocked** — failures are best-effort; the catalog loads whatever succeeds.

---

## 8. Full Call Chain Example — ZIP Format

```
MainWindow.InitializePluginSystemAsync()
 └─ FormatCatalogService.Initialize(embeddedEntries, externalDir)
     └─ EmbeddedFormatCatalog.Instance.GetAll()
         └─ MakeEntries()
             └─ Assembly.GetManifestResourceNames()
                 → "WpfHexEditor.Definitions.FormatDefinitions.Archives.ZIP.whfmt"
             └─ LoadHeader("...ZIP.whfmt")
                 └─ JsonDocument.Parse (CommentHandling.Skip)
                 └─ EmbeddedFormatEntry { Name="ZIP Archive", Extensions=[".zip",".jar",...], Category="Archives" }
     └─ For each EmbeddedFormatEntry (whfmt only):
         └─ EmbeddedFormatCatalog.GetJson(entry.ResourceKey)
             └─ _jsonCache miss → OpenManifestResourceStream → ReadToEnd → cache
         └─ FormatDetectionService.ImportFromJson(json)
             └─ Skip /* comment header */
             └─ JsonSerializer.Deserialize<FormatDefinition>()
             └─ IsValid() → true
             └─ Return FormatDefinition { FormatName="ZIP Archive", Blocks=[...] }
         └─ _formats.Add(formatDefinition)
     └─ FormatDetectionService.SetSharedCatalog(_formats)  ← static, locked
     └─ Fire CatalogReady
```

---

## 9. Key Files Reference

| File | Path |
|---|---|
| `.csproj` (embedding) | `Core/WpfHexEditor.Core.Definitions/WpfHexEditor.Core.Definitions.csproj` |
| `EmbeddedFormatCatalog.cs` | `Core/WpfHexEditor.Core.Definitions/EmbeddedFormatCatalog.cs` |
| `IEmbeddedFormatCatalog.cs` | `Core/WpfHexEditor.Core.Definitions/IEmbeddedFormatCatalog.cs` |
| `FormatDefinition.cs` | `Core/WpfHexEditor.Core/Models/FormatDefinition.cs` |
| `FormatCatalogService.cs` | `Core/WpfHexEditor.Core/Services/FormatCatalogService.cs` |
| `FormatDetectionService.cs` | `Core/WpfHexEditor.Core/Services/FormatDetectionService.cs` |
| `FormatScriptInterpreter.cs` | `Core/WpfHexEditor.Core/Services/FormatScriptInterpreter.cs` |
| `HexEditor.FormatDetection.cs` | `Editors/WpfHexEditor.HexEditor/PartialClasses/Features/` |
| `MainWindow.PluginSystem.cs` | `WpfHexEditor.App/PartialClasses/MainWindow.PluginSystem.cs` |
| `EmbeddedWhfmt_Tests.cs` | `Tests/` (build gate: 400+ formats) |

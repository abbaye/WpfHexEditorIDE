# WpfHexEditor — Documentation Index

Complete documentation for the WpfHexEditor IDE.

**Last Updated:** 2026-04-15

---

## 🚀 Getting Started

- **[Quick Start Guide](QuickStart.md)** — Get up and running quickly
- **[Migration Guide](MigrationGuide.md)** — Migrating from V1 to V2
- **[API Reference](ApiReference.md)** — HexEditor control API
- **[Rider / JetBrains Guide](IDE/RIDER_GUIDE.md)** — IDE setup for Rider users

---

## 🏗️ Architecture

- **[Solution Architecture](architecture/Solution_Architecture.md)** — Overall solution structure and service layers
- **[HexEditor Architecture](architecture/HexEditorArchitecture.md)** — V2 MVVM architecture with partial class organization
- **[Architecture Overview](architecture/Overview.md)** — Component layers and data flow
- **[Multilingual System](architecture/Multilingual_System.md)** — Localization architecture (6 languages)

### Core Systems

- **[ByteProvider System](architecture/core-systems/byteprovider-system.md)**
- **[Undo/Redo System](architecture/core-systems/undo-redo-system.md)**
- **[Rendering System](architecture/core-systems/rendering-system.md)**
- **[Edit Tracking](architecture/core-systems/edit-tracking.md)**
- **[Position Mapping](architecture/core-systems/position-mapping.md)**
- **[Service Layer](architecture/core-systems/service-layer.md)**

### Data Flow

- **[File Operations](architecture/data-flow/file-operations.md)**
- **[Edit Operations](architecture/data-flow/edit-operations.md)**
- **[Save Operations](architecture/data-flow/save-operations.md)**
- **[Search Operations](architecture/data-flow/search-operations.md)**

---

## 📦 Projects

### IDE Application

- **[WpfHexEditor.App](../Sources/WpfHexEditor.App/README.md)** — Main IDE host, plugin system, build integration
- **[WpfHexEditor.Shell](../Sources/WpfHexEditor.Shell/README.md)** — Docking engine + 19 themes
- **[WpfHexEditor.Panels.IDE](../Sources/WpfHexEditor.Panels.IDE/README.md)** — Solution Explorer, Properties, Error List
- **[WpfHexEditor.Options](../Sources/WpfHexEditor.Options/README.md)** — Options panel

### Editors

- **[Code Editor](../Sources/WpfHexEditor.Editor.CodeEditor/README.md)** — Multi-language code editor (~90%)
- **[XAML Designer](../Sources/WpfHexEditor.Editor.XamlDesigner/README.md)** — Visual XAML designer with bidirectional sync (~70%)
- **[Text Editor](../Sources/WpfHexEditor.Editor.TextEditor/README.md)** — Plain text editing (~50%)
- **[Hex Editor](../Sources/WpfHexEditor.HexEditor/README.md)** — Binary hex editing (~75%)
- **[TBL Editor](../Sources/WpfHexEditor.Editor.TblEditor/README.md)** — Character table editor
- **[Script Editor](../Sources/WpfHexEditor.Editor.ScriptEditor/README.md)** — `.hxscript` editor
- **[Image Viewer](../Sources/WpfHexEditor.Editor.ImageViewer/README.md)** — Binary image viewer
- **[Entropy Viewer](../Sources/WpfHexEditor.Editor.EntropyViewer/README.md)** — Entropy graph
- **[Diff Viewer](../Sources/WpfHexEditor.Editor.DiffViewer/README.md)** — Binary + text + structure diff (GlyphRun canvas renderers)
- **[Structure Editor](../Sources/WpfHexEditor.Editor.StructureEditor/README.md)** — `.whfmt` binary template editor
- **[Document Editor](../Sources/WpfHexEditor.Editor.DocumentEditor/README.md)** — WYSIWYG RTF/DOCX/ODT
- **[Disassembly Viewer](../Sources/WpfHexEditor.Editor.DisassemblyViewer/README.md)** — x86/x64/ARM disassembler
- **[Audio Viewer](../Sources/WpfHexEditor.Editor.AudioViewer/README.md)** — Waveform display (WAV/MP3/FLAC/OGG)
- **[Markdown Editor](../Sources/WpfHexEditor.Editor.MarkdownEditor/README.md)** — Markdown with live preview
- **[JSON Editor](../Sources/WpfHexEditor.Editor.JsonEditor/README.md)** — JSON viewer
- **[Class Diagram](../Sources/WpfHexEditor.Editor.ClassDiagram/README.md)** — UML class diagram generator

### Infrastructure

- **[Editor Core](../Sources/Editors/WpfHexEditor.Editor.Core/README.md)** — `IDocumentEditor`, shared editor contracts
- **[Core.Contracts](../Sources/Core/WpfHexEditor.Core.Contracts/)** — `IEmbeddedFormatCatalog`, `EmbeddedFormatEntry`, `FormatCategory`, `SchemaName` — cross-platform `net8.0`
- **[Core.Definitions](../Sources/Core/WpfHexEditor.Core.Definitions/README.md)** — 690+ embedded `.whfmt` definitions; published as [`whfmt.FileFormatCatalog`](features/whfmt-FileFormatCatalog-guide.md) NuGet
- **[SDK](../Sources/Plugins/WpfHexEditor.SDK/README.md)** — Plugin SDK extension points
- **[Events](../Sources/Core/WpfHexEditor.Core.Events/README.md)** — IDE-wide event bus + all domain events
- **[LSP](../Sources/Core/WpfHexEditor.Core.LSP.Client/README.md)** — Language Server Protocol 3.17 client
- **[BuildSystem](../Sources/Core/WpfHexEditor.Core.BuildSystem/README.md)** — Build orchestration + adapter contracts
- **[Core.SourceAnalysis](../Sources/Core/WpfHexEditor.Core.SourceAnalysis/README.md)** — Regex-based outline engine (BCL-only)
- **[ProjectSystem](../Sources/Core/WpfHexEditor.Core.ProjectSystem/README.md)** — Solution/project model
- **[Terminal](../Sources/Controls/WpfHexEditor.Terminal/README.md)** — Integrated terminal panel
- **[PluginHost](../Sources/Plugins/WpfHexEditor.PluginHost/README.md)** — Plugin discovery + loading
- **[Docking Core](../Sources/Docking/WpfHexEditor.Docking.Core/README.md)** — Docking engine contracts
- **[Core.Roslyn](../Sources/Core/WpfHexEditor.Core.Roslyn/README.md)** — In-process Roslyn C#/VB.NET analysis
- **[Core.Workspaces](../Sources/Core/WpfHexEditor.Core.Workspaces/README.md)** — `.whidews` workspace engine
- **[Core.Diff](../Sources/Core/WpfHexEditor.Core.Diff/README.md)** — Myers/Binary/Semantic diff algorithms
- **[Core.MCP](../Sources/Core/WpfHexEditor.Core.MCP/README.md)** — Model Context Protocol support

### Plugins

- **[AI Assistant](../Sources/Plugins/WpfHexEditor.Plugins.AIAssistant/README.md)** — Multi-provider AI chat (Anthropic/OpenAI/Gemini/Ollama/Claude Code CLI), 24 MCP tools, streaming, inline apply
- **[SynalysisGrammar](../Sources/Plugins/WpfHexEditor.Plugins.SynalysisGrammar/README.md)** — UFWB binary grammar support
- **[XamlDesigner Plugin](../Sources/Plugins/WpfHexEditor.Plugins.XamlDesigner/README.md)** — XAML designer IDE integration
- **[SolutionLoader.Folder](../Sources/WpfHexEditor.Plugins.SolutionLoader.Folder/README.md)** — Open-folder VS Code–style
- **[SolutionLoader.VS](../Sources/WpfHexEditor.Plugins.SolutionLoader.VS/README.md)** — Visual Studio .sln/.csproj support
- **[SolutionLoader.WH](../Sources/WpfHexEditor.Plugins.SolutionLoader.WH/README.md)** — Native .whsln/.whproj support
- **[Build.MSBuild](../Sources/WpfHexEditor.Plugins.Build.MSBuild/README.md)** — MSBuild / dotnet CLI adapter
- **[AssemblyExplorer](../Sources/Plugins/WpfHexEditor.Plugins.AssemblyExplorer/README.md)** — .NET PE inspection + decompilation
- **[ParsedFields](../Sources/Plugins/WpfHexEditor.Plugins.ParsedFields/README.md)** — Binary structure overlay
- **[DataInspector](../Sources/Plugins/WpfHexEditor.Plugins.DataInspector/README.md)** — 40+ type interpretations
- **[ArchiveExplorer](../Sources/Plugins/WpfHexEditor.Plugins.ArchiveExplorer/README.md)** — ZIP/7z/TAR tree browser
- **[FileComparison](../Sources/Plugins/WpfHexEditor.Plugins.FileComparison/README.md)** — DiffHub + binary/text/structure diff
- **[LSPTools](../Sources/Plugins/WpfHexEditor.Plugins.LSPTools/README.md)** — Call/Type Hierarchy panels
- **[UnitTesting](../Sources/Plugins/WpfHexEditor.Plugins.UnitTesting/README.md)** — Unit test runner (xunit/nunit/mstest)
- **[Debugger](../Sources/Plugins/WpfHexEditor.Plugins.Debugger/README.md)** — Integrated debugger UI
- **[Git](../Sources/Plugins/WpfHexEditor.Plugins.Git/README.md)** — Git integration
- **[ScriptRunner](../Sources/Plugins/WpfHexEditor.Plugins.ScriptRunner/README.md)** — Script execution panel
- **[DiagnosticTools](../Sources/Plugins/WpfHexEditor.Plugins.DiagnosticTools/README.md)** — CPU/memory monitoring

### Sample Applications

- **[Sample.CodeEditor](../Sources/Samples/WpfHexEditor.Sample.CodeEditor/README.md)** — Standalone CodeEditor demo
- **[Sample.Docking](../Sources/Samples/WpfHexEditor.Sample.Docking/README.md)** — Docking framework demo
- **[Sample.HexEditor](../Sources/Samples/WpfHexEditor.Sample.HexEditor/README.md)** — Full hex editor demo
- **[Sample.Terminal](../Sources/Samples/WpfHexEditor.Sample.Terminal/README.md)** — Terminal panel demo

---

## ⚡ Performance

- **[Performance Guide](performance/Performance_Guide.md)**
- **[Save Optimization](performance/Save_Optimization.md)**
- **[V2 Performance Improvements](performance/V2_Performance_Improvements.md)**

---

## 📋 Features

- **[Format Detection (690+)](features/FormatDetection_400.md)** — Auto-detection for 690+ file formats via magic bytes, extensions, MIME types
- **[Format Definition Schema](FormatDefinition_Schema.md)** — `.whfmt` binary template format (schema v2.3)
- **[whfmt.FileFormatCatalog Guide](features/whfmt-FileFormatCatalog-guide.md)** — NuGet package API reference, architecture, L1–L3 integration guides

---

## 🔗 Resources

- **[Main README](../README.md)** — Project overview and feature table
- **[CHANGELOG](CHANGELOG.md)** — Version history (v0.5.0 → v0.6.4.75)
- **[ROADMAP](ROADMAP.md)** — Planned features and progress
- **[CONTRIBUTING](../.github/CONTRIBUTING.md)** — How to contribute
- **[SECURITY](../.github/SECURITY.md)** — Security policy
- **[Wiki](../WpfHexEditorControl.wiki/Home.md)** — Community wiki

---

## 📖 Wiki

- **[Home](../WpfHexEditorControl.wiki/Home.md)**
- **[Architecture](../WpfHexEditorControl.wiki/Architecture.md)**
- **[Plugin System](../WpfHexEditorControl.wiki/Plugin-System.md)**
- **[Docking Engine](../WpfHexEditorControl.wiki/Docking-Engine.md)**
- **[Editor Registry](../WpfHexEditorControl.wiki/Editor-Registry.md)**
- **[ByteProvider](../WpfHexEditorControl.wiki/ByteProvider.md)**
- **[Terminal](../WpfHexEditorControl.wiki/Terminal.md)**
- **[ContentId Routing](../WpfHexEditorControl.wiki/ContentId-Routing.md)**
- **[Performance](../WpfHexEditorControl.wiki/Performance.md)**
- **[FAQ](../WpfHexEditorControl.wiki/FAQ.md)**

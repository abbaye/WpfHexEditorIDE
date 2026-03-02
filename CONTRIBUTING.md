# 🤝 Contributing to WpfHexEditor

Contributions are welcome and appreciated! This guide covers everything you need to get started.

---

## Getting Started

### Prerequisites

- **Visual Studio 2022** (recommended) or Rider 2024+
- **.NET 8.0 SDK** — [download](https://dotnet.microsoft.com/download)
- **Windows** (WPF platform requirement)

### Fork & Clone

```bash
git clone https://github.com/YOUR_USERNAME/WpfHexEditorIDE.git
cd WpfHexEditorIDE
```

Open `WpfHexEditorControl.sln` in Visual Studio 2022.

Set **`WpfHexEditor.App`** as the startup project and press F5 to verify everything builds and runs.

---

## Project Structure

```
Sources/
├── WpfHexEditor.App/              ← IDE application (startup project)
├── WpfHexEditor.Core/             ← ByteProvider, services, data layer
├── WpfHexEditor.HexEditor/        ← HexEditor WPF control (partial classes by feature)
│   └── PartialClasses/
│       ├── Core/                  ← File ops, stream ops, events, async
│       └── Features/              ← Search, TBL, bookmarks, format detection, ...
├── WpfHexEditor.Editor.Core/      ← IDocumentEditor, IEditorFactory contracts
├── WpfHexEditor.Editor.TblEditor/ ← TBL editor (standalone, IDocumentEditor)
├── WpfHexEditor.Editor.JsonEditor/← JSON editor (standalone, IDocumentEditor)
├── WpfHexEditor.Editor.TextEditor/← Text editor (standalone, IDocumentEditor)
├── WpfHexEditor.Panels.IDE/       ← SolutionExplorer, Properties, ErrorPanel
├── WpfHexEditor.Panels.BinaryAnalysis/ ← ParsedFields, DataInspector, StructureOverlay
├── WpfHexEditor.Docking.Wpf/      ← VS-style docking engine
├── WpfHexEditor.ProjectSystem/    ← Solution/Project management
├── WpfHexEditor.HexBox/           ← Standalone hex input control
├── WpfHexEditor.BarChart/         ← Byte distribution chart
└── WpfHexEditor.Tests/            ← Unit tests (xUnit)
```

---

## Branch Conventions

| Branch | Purpose |
|--------|---------|
| `master` | Stable, release-ready |
| `development` | Active development — target this for PRs |
| `feature/my-feature` | Feature branches (off `development`) |
| `fix/issue-123` | Bug fix branches |

**Always branch from `development`, not `master`.**

---

## Code Standards

### Architecture Patterns
- **MVVM** — ViewModels must not reference Views; use Commands and bindings
- **Partial classes** — HexEditor features are split into `PartialClasses/Features/*.cs`; follow this pattern for new features
- **Services** — Business logic belongs in services (`WpfHexEditor.Core/Services/`), not in the control
- **Interfaces** — New panel types must implement the relevant interface in `WpfHexEditor.Editor.Core`

### Coding Style
- C# 13, nullable reference types enabled
- `PascalCase` for public members, `_camelCase` for private fields
- Async methods suffixed with `Async`; always accept `CancellationToken`
- XML doc comments on all public API members

### Adding a New Editor
1. Create a new project `WpfHexEditor.Editor.YourEditor`
2. Implement `IDocumentEditor` (from `WpfHexEditor.Editor.Core`)
3. Create an `IEditorFactory` and register it: `EditorRegistry.Instance.Register(new YourEditorFactory())`
4. Add a stub entry to the Editors table in `README.md` and `FEATURES.md`

### Adding a New Panel
1. Create the UserControl in the appropriate `WpfHexEditor.Panels.*` project
2. Define the interface in `WpfHexEditor.Editor.Core`
3. Add `Panel_*` and your panel-specific theme keys to all 8 `Colors.xaml` files
4. Connect in `WpfHexEditor.App/MainWindow.xaml.cs` via `DockHost.ActiveItemChanged`

### Theme Keys
Every new UI color **must** be a `DynamicResource` key defined in all 8 theme `Colors.xaml` files:
`Dark` · `Light` · `VS2022Dark` · `DarkGlass` · `Minimal` · `Office` · `Cyberpunk` · `VisualStudio`

---

## Running Tests

```bash
dotnet test Sources/WpfHexEditor.Tests/
dotnet test Sources/WpfHexEditor.BinaryAnalysis.Tests/
dotnet test Sources/WpfHexEditor.Docking.Tests/
```

---

## Submitting a Pull Request

1. **Branch** from `development`: `git checkout -b feature/my-feature development`
2. **Make your changes** — keep commits focused and descriptive
3. **Run the tests** — all tests must pass
4. **Push** your branch and open a PR targeting `development`
5. **Describe** what you changed and why in the PR description

**PR checklist:**
- [ ] Targets `development` branch
- [ ] Tests pass
- [ ] New theme keys added to all 8 `Colors.xaml` (if any UI color added)
- [ ] Public API has XML doc comments
- [ ] README / FEATURES updated if a new editor or panel was added

---

## Reporting Issues

- 🐛 **Bug report**: [GitHub Issues](https://github.com/abbaye/WpfHexEditorIDE/issues) — include OS, .NET version, minimal repro
- 💡 **Feature request**: [GitHub Discussions](https://github.com/abbaye/WpfHexEditorIDE/discussions)

---

## Useful Links

- [Architecture Overview](docs/architecture/Overview.md)
- [API Reference](docs/api-reference/)
- [CHANGELOG](CHANGELOG.md)
- [GETTING_STARTED](GETTING_STARTED.md)

Thank you for contributing! 🙏

# WpfHexEditor.SDK Changelog

All notable changes to the public plugin SDK are documented in this file.
This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

**Versioning policy:**
- **MAJOR** (X.0.0) — Breaking interface changes (removed/renamed members, changed signatures).
  Plugin authors **must** update their code. See `SDK_MIGRATION.md` for upgrade guides.
- **MINOR** (2.X.0) — New interfaces, new members with default implementations, new models/events.
  Backward-compatible. Existing plugins continue to work without changes.
- **PATCH** (2.0.X) — Bug fixes, documentation, XML doc corrections.
  No behavioral changes.

---

## [2.0.0] - 2026-03-26

### Added
- SemVer versioning contract with `AssemblyVersion`, `FileVersion`, and `InformationalVersion`
- `GenerateDocumentationFile` for XML doc output (enables future docfx API reference)
- Package metadata (`Authors`, `Copyright`, `PackageId`, `PackageLicenseExpression`, `RepositoryUrl`)
- This CHANGELOG file
- `SDK_MIGRATION.md` — migration guide template for breaking changes

### Changed
- Version bumped from `1.2.0` to `2.0.0` to establish SemVer baseline
- `InformationalVersion` set to `2.0.0+frozen` to indicate API freeze

### Deprecated
- `IMarketplaceService` — Preview stub. Method signatures will change when marketplace backend is implemented.
- `MarketplaceListing` — Preview model. Properties will change with marketplace implementation.

### Stable Interfaces (frozen in 2.x)
The following interfaces are considered stable and will not receive breaking changes in SDK 2.x:
- `IWpfHexEditorPlugin` / `IWpfHexEditorPluginV2`
- `IIDEHostContext`
- `IHexEditorService`, `ICodeEditorService`, `IDocumentHostService`
- `IOutputService`, `IErrorPanelService`, `ITerminalService`
- `IParsedFieldService`, `ISolutionExplorerService`
- `IUIRegistry`, `IThemeService`, `IPermissionService`
- `IFocusContextService`, `IPluginEventBus`, `IExtensionRegistry`
- `ICommandRegistry`, `IPluginCapabilityRegistry`
- `IDebuggerService`, `IDiffService`, `ITestRunnerService`
- `IScriptingService`, `IWorkspaceService`
- `IPluginWithOptions`, `IPluginState`
- `IPanel`, `IDocument`
- All extension points: `IBinaryParserExtension`, `IDecompilerExtension`, `IFileAnalyzerExtension`, `IHexViewOverlayExtension`, `IQuickInfoProvider`
- All models: `PluginManifest`, `PluginCapabilities`, `PluginPermission`, `PluginState`, `PluginFeature`, `PluginIsolationMode`, `PluginDependencySpec`, `PluginVersionConstraint`, `PluginActivationConfig`
- All descriptors: `PanelDescriptor`, `MenuItemDescriptor`, `ToolbarItemDescriptor`, `DocumentDescriptor`, `StatusBarItemDescriptor`
- `RelayCommand`

---

## [1.2.0] - 2026-03-25

### Added
- `IFormatParsingService` on `IIDEHostContext` (nullable, default null)
- `IWorkspaceService` on `IIDEHostContext` (nullable, default null)
- `FormatParsingEvents` (FormatDetected, ParsingStarted, ParsingCompleted, ParsingFailed)
- `ParsedFieldsUpdateRequestedEvent`
- `IFormatCatalogService` re-export stub

## [1.1.0] - 2026-03-24

### Added
- `IDiffService` on `IIDEHostContext`
- `ITestRunnerService` on `IIDEHostContext`
- `IScriptingService` on `IIDEHostContext`
- `ISolutionExplorerContextMenuContributor` + `SolutionContextMenuItem`
- `ICommandRegistry` on `IIDEHostContext`
- `IGrammarProvider` for syntax grammar injection
- `GrammarAppliedEvent`

## [1.0.0] - 2026-03-19

### Initial Release
- Core plugin contracts: `IWpfHexEditorPlugin`, `IWpfHexEditorPluginV2`
- `IIDEHostContext` with 11 service contracts
- All service interfaces (HexEditor, CodeEditor, Output, ErrorPanel, Terminal, etc.)
- Plugin models (Manifest, Capabilities, Permission, State, Feature, IsolationMode)
- Extension points (BinaryParser, Decompiler, FileAnalyzer, HexViewOverlay, QuickInfo)
- UI integration (UIRegistry, ThemeService, FocusContext)
- Sandbox IPC protocol
- MSBuild targets for plugin packaging

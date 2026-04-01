# WpfHexEditor.Plugins.SolutionLoader.VS

**Type:** Plugin (`net8.0-windows`) | **Load Priority:** 90
**Role:** Loads Visual Studio `.sln`, `.csproj`, and `.vbproj` files into the WpfHexEditor project model.

---

## Responsibility

Bridges the Visual Studio solution/project format to the IDE's internal project model, enabling opening, browsing, and building VS projects directly.

---

## Key Classes

| Class | Responsibility |
|-------|---------------|
| `VsSolutionLoaderPlugin` | Plugin entry point — registers loader + 4 project templates |
| `VsSolutionLoader` | Implements `ISolutionLoader`; parses `.sln` files and constructs project trees |
| `VSProjectParser` | Parses `.csproj` / `.vbproj` XML (ItemGroups, References, Properties) |
| `VsSolution` | Root model for a VS solution |
| `VsProject` | VS project node with build metadata |
| `VsProjectItem` | File/item node within a project |
| `VsVirtualFolder` | Solution folder (virtual grouping) |
| `ConsoleAppTemplate` | Built-in project template |
| `ClassLibraryTemplate` | Built-in project template |
| `WpfAppTemplate` | Built-in project template |
| `AspNetApiTemplate` | Built-in project template |

---

## Features

- Opens `.sln` files including nested solution folders
- Parses `.csproj` / `.vbproj` item groups (Compile, Content, EmbeddedResource)
- Registers 4 built-in project templates for New Project dialog
- Full integration with MSBuild plugin (build triggered via `IBuildAdapter`)

---

## Dependencies

| Project | Role |
|---------|------|
| `WpfHexEditor.SDK` | `ISolutionLoader`, `IExtensionRegistry` |
| `WpfHexEditor.Editor.Core` | Shared types |
| `WpfHexEditor.ProjectSystem` | Template registry |

---

## Design Patterns Used

Adapter + Extension Point + Registry — loads VS solution format via `ISolutionLoader` contract; registers templates via `IProjectTemplateRegistry`.

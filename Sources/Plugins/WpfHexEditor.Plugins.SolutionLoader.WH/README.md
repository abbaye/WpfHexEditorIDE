# WpfHexEditor.Plugins.SolutionLoader.WH

**Type:** Plugin (`net8.0-windows`) | **Load Priority:** 95
**Role:** Loads native WpfHexEditor `.whsln` and `.whproj` solution/project files.

---

## Responsibility

Minimal adapter plugin that registers the native WH solution loader. Delegates all parsing logic to the existing `SolutionManager` in `WpfHexEditor.ProjectSystem`.

---

## Key Classes

| Class | Responsibility |
|-------|---------------|
| `WHSolutionLoaderPlugin` | Plugin entry point — registers `WHSolutionLoader` with `IExtensionRegistry` |
| `WHSolutionLoader` | Implements `ISolutionLoader`; delegates to `SolutionManager.LoadAsync()` |

---

## Features

- Handles `.whsln` (solution) and `.whproj` (project) files
- Highest load priority (95) — loaded after VS and Folder loaders to ensure infrastructure is ready
- Zero-overhead design: 2 classes, delegates fully to ProjectSystem

---

## Dependencies

| Project | Role |
|---------|------|
| `WpfHexEditor.SDK` | `ISolutionLoader` |
| `WpfHexEditor.ProjectSystem` | `SolutionManager` |

---

## Design Patterns Used

Adapter — wraps `SolutionManager` behind `ISolutionLoader` for registration-based discovery.

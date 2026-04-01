# WpfHexEditor.Plugins.SolutionLoader.Folder

**Type:** Plugin (`net8.0-windows`) | **Load Priority:** 85
**Role:** Opens any directory on disk as a VS Code–style folder session with gitignore-aware auto-refresh.

---

## Responsibility

Allows opening a plain folder (no `.whsln` / `.sln` required) as a project session in the IDE. The folder is displayed in Solution Explorer as a virtual project with live file-system watching.

---

## Key Classes

| Class | Responsibility |
|-------|---------------|
| `FolderSolutionLoaderPlugin` | Plugin entry point — registers `FolderSolutionLoader` with `IExtensionRegistry` |
| `FolderSolutionLoader` | Implements `ISolutionLoader`; creates folder session from directory path |
| `FolderFileEnumerator` | Recursively enumerates directory tree; respects `.gitignore` patterns |
| `FolderFileWatcher` | `FileSystemWatcher` with 500ms debounce for live file change notifications |
| `FolderMarkerFile` | `.whfolder` JSON marker file (persists folder session config) |
| `FolderSolution` | Root solution model for a folder session |
| `FolderProject` | Virtual project node containing folder items |
| `FolderVirtualFolder` | Virtual sub-folder node |
| `FolderItem` | Leaf file node |
| `GitIgnoreFilter` | Parses `.gitignore` rules and applies them to enumeration |

---

## Features

- Auto-detects and applies `.gitignore` rules — hidden/ignored files excluded from Solution Explorer
- 500ms debounce on file system events — no UI flooding on mass file operations
- No `.whsln` file required — the `.whfolder` marker is optional
- Registered via `IExtensionRegistry` — no hard coupling to `MainWindow`

---

## Dependencies

| Project | Role |
|---------|------|
| `WpfHexEditor.SDK` | `ISolutionLoader`, `IExtensionRegistry` |
| `WpfHexEditor.Editor.Core` | Shared editor types |

---

## Design Patterns Used

Adapter + Extension Point — `FolderSolutionLoader` adapts the file system to the `ISolutionLoader` contract registered through `IExtensionRegistry`.

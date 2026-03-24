# WpfHexEditor.BuildSystem

**Type:** Class Library (`net8.0-windows`, UseWPF: false)
**Role:** Core build orchestration engine — dependency resolution, configuration management, and pluggable build adapter contracts.

---

## Responsibility

`WpfHexEditor.BuildSystem` orchestrates multi-project builds without containing any MSBuild-specific logic. Actual compilation is delegated to `IBuildAdapter` implementations (e.g., `WpfHexEditor.Plugins.Build.MSBuild`).

---

## Key Classes

| Class | Responsibility |
|-------|---------------|
| `BuildSystem` | Main facade — resolves build order, delegates to adapters, publishes Build* events |
| `BuildDependencyResolver` | Kahn's topological sort for project dependencies; detects circular references |
| `ConfigurationManager` | Registry of `BuildConfiguration` objects (Debug/Release/custom); active config with change events |
| `BuildConfiguration` | Name, Platform (AnyCPU/…), MSBuildProperties dictionary |
| `StartupProjectRunner` | Executes the designated startup project after a successful build |

---

## Key Interfaces

| Interface | Contract |
|-----------|---------|
| `IBuildSystem` | `BuildSolutionAsync()` · `BuildProjectAsync()` · `RebuildSolutionAsync()` · `CleanSolutionAsync()` · `CancelBuild()` · `HasActiveBuild` |
| `IBuildAdapter` | `AdapterId` · `CanBuild(path) → bool` · `BuildAsync(path, config, progress, ct) → Task` · `CleanAsync()` |
| `IBuildConfiguration` | `Name` · `Platform` · `MSBuildProperties` |

---

## Event Flow

```
BuildSolutionAsync()
  → GetVsSolutionFilePath() or GetAllProjectPaths()
  → BuildDependencyResolver.Sort() (Kahn's algorithm)
  → For each project in dependency order:
      → FindAdapter(path) → adapter.BuildAsync()
  → Publish: BuildStartedEvent
  → Publish: BuildOutputLineEvent (per output line)
  → Publish: BuildProgressUpdatedEvent (progress %)
  → Publish: BuildSucceededEvent or BuildFailedEvent
```

---

## Execution Model

- Orchestration runs on the caller thread (typically a background Task)
- Build execution is async via `IBuildAdapter.BuildAsync()` with `CancellationToken`
- Progress reported via `IProgress<string>` callback (output lines)
- `HasActiveBuild` prevents concurrent builds; `CancelBuild()` stops in-flight adapters

---

## Dependencies

| Project | Role |
|---------|------|
| `WpfHexEditor.Editor.Core` | Shared types |
| `WpfHexEditor.SDK` | Extension point contracts |
| `WpfHexEditor.Events` | Build event publishing |

---

## Design Patterns Used

| Pattern | Where |
|---------|-------|
| **Facade** | `BuildSystem` hides orchestration complexity |
| **Adapter** | `IBuildAdapter` decouples MSBuild/Gradle/etc. |
| **Topological Sort** | `BuildDependencyResolver` (Kahn's algorithm) |
| **Observer** | `ConfigurationManager` fires change events |

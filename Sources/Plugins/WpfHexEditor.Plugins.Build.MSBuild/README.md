# WpfHexEditor.Plugins.Build.MSBuild

**Type:** Plugin (`net8.0-windows`) | **Load Priority:** 85
**Role:** MSBuild adapter — compiles `.csproj`, `.vbproj`, and `.fsproj` projects by invoking `dotnet build` as an external process.

---

## Responsibility

Implements `IBuildAdapter` for the `WpfHexEditor.BuildSystem` build orchestration engine. Delegates compilation to the `dotnet` CLI — avoiding in-process `Microsoft.Build.*` assembly loading conflicts.

---

## Key Classes

| Class | Responsibility |
|-------|---------------|
| `MSBuildPlugin` | Plugin entry point — registers `MSBuildAdapter` with `IBuildAdapterRegistry` |
| `MSBuildAdapter` | Implements `IBuildAdapter`; spawns `dotnet build` process, streams output lines |

---

## Features

- Handles `.csproj` / `.vbproj` / `.fsproj` / `.sln` files (`CanBuild()`)
- Streams build output line-by-line to `IProgress<string>` (no buffering)
- Respects `CancellationToken` — kills `dotnet build` process on cancel
- Passes active `BuildConfiguration.MSBuildProperties` as `/p:Key=Value` args
- No `Microsoft.Build.*` NuGet dependency — zero version conflict risk

---

## Build Command Format

```
dotnet build <projectPath> -c <Configuration> /p:Platform=<Platform> [extra props]
```

---

## Dependencies

| Project | Role |
|---------|------|
| `WpfHexEditor.SDK` | `IBuildAdapter` contract |
| `WpfHexEditor.BuildSystem` | `IBuildAdapterRegistry` |
| `WpfHexEditor.Editor.Core` | Shared types |

---

## Design Patterns Used

Adapter — wraps `dotnet build` CLI behind `IBuildAdapter` for pluggable build backend support.

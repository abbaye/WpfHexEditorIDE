# SRP boundaries (project-specific)

Each WpfHexEditor module has a documented responsibility. SRP at the **class
level** is the skill's heuristic; SRP at the **module level** is encoded by
`code-analysis/references/module-boundaries.md`.

This document complements that file with the responsibility narrative each
module owns.

## Module responsibilities

| Module                              | Single responsibility                                  |
|-------------------------------------|--------------------------------------------------------|
| `WpfHexEditor.App`                  | IDE shell — composition root, host context, top-level UI |
| `WpfHexEditor.SDK`                  | Public extensibility surface for plugins/editors       |
| `WpfHexEditor.Editor.Core`          | Shared editor primitives (services, dialogs, undo)     |
| `WpfHexEditor.Editor.<X>`           | One editor type (Document, Code, Image, Audio, ...)    |
| `WPFHexaEditor`                     | Autonomous Hex Editor control                          |
| `WpfHexEditor.Plugins.<X>`          | One plugin (Git, ParsedFields, ClassDiagram, ...)      |
| `WpfHexEditor.Docking.Wpf`          | Docking / tab / panel layout system                    |
| `WpfHexEditor.Shell`                | Theme dictionaries, panel chrome                       |

A class crossing module boundaries (e.g., a Plugin class doing App-level
work) is a more serious SRP violation than a long-but-cohesive class.
`code-analysis`'s scope-impact script catches the cross-boundary case;
`solid-guard` catches the broad-class case.

## Common SRP-clean patterns in the repo

- **Service + Adapter**: `FileMonitorService` (IO) + an Adapter that
  marshals events to the Dispatcher. Each is single-responsibility.
- **Module + sub-services**: `CodeAnalysisModule` (composition root,
  exempted from `dip-newing-services`) wires several collectors and
  calculators (each single-responsibility).
- **Editor + Pane**: each editor (e.g., `DocumentEditor`) has one editor
  class and per-feature panes (StructurePane, MiniMap), each with its
  own responsibility.

## When to ignore the warning

Some classes legitimately do "more than one thing" because they implement
a coordination pattern:

- **Composition roots** (`*Module.cs`, `IDEHostContext`, `App.xaml.cs`):
  exempted by path.
- **File watchers / IO bridges**: `srp-mixed-concerns` exempted by the
  `*FileWatcher*` / `*FileMonitor*` regex.
- **Adapters** (`*Adapter.cs`): exempted from `srp-mixed-concerns`. Their
  job is to translate between two layers.
- **Bridges** (`*Bridge.cs`): same exemption.

Add new exemption regexes sparingly. Each exemption weakens the signal.

## Memory anchors

- ADR-009 (themed MessageBox / `IDialogService`) — clean DIP example.
- ADR-010 / ADR-011 — plugin → core App migration kept SDK extensibility,
  preserving the original SRP boundaries.
- `feedback_no_avalonedit` — CodeEditor renders itself, doesn't import
  AvalonEdit; SRP for the rendering layer is owned in-house.

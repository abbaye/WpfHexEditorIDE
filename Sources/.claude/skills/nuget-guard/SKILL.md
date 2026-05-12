---
name: nuget-guard
description: |
  INTERNAL DEV WORKFLOW for WpfHexEditor — Claude self-invokes after editing
  any *.cs or *.csproj that belongs to one of the 13 packages published on
  nuget.org by abbaye (WpfHexEditor.Core.ByteProvider, .BinaryAnalysis,
  WPFHexaEditor, WpfCodeEditor, WpfDocking, WpfColorPicker, WpfTerminal,
  whfmt.Analysis/.Backfill/.CodeGen/.Fuzz/.Validate/.FileFormatCatalog).
  Protects the standalone-mode contract when IDE features are added:
  detects IDE-only references leaking into a NuGet package, TFM drift,
  UseWPF leak on core-xplat packages, WPF/WinForms usings in core-xplat,
  and public-API regressions vs git HEAD (api-removed, api-renamed).
  Extending the API is always allowed; removing/renaming is not. Skip on
  files outside protected packages, Tests/, Samples/, *.Designer.cs, *.g.cs.
---

# nuget-guard (internal)

Protects the **standalone** contract of every package abbaye publishes on
nuget.org. The IDE shell (`WpfHexEditor.App`, the various `Editor.*`
modules, the `Plugins.*` modules) is a consumer of these packages — they
must remain usable without it.

## Protected packages (13)

| PackageId                              | Category     | Path                                                |
|----------------------------------------|--------------|-----------------------------------------------------|
| WpfHexEditor.Core.ByteProvider         | core-xplat   | `Sources/Core/WpfHexEditor.Core.ByteProvider`       |
| WpfHexEditor.Core.BinaryAnalysis       | core-xplat   | `Sources/Core/WpfHexEditor.Core.BinaryAnalysis`     |
| whfmt.FileFormatCatalog                | core-xplat   | `Sources/Core/WpfHexEditor.Core.Definitions`        |
| whfmt.Analysis                         | core-xplat   | `Sources/Tools/whfmt.Analysis`                      |
| whfmt.Backfill                         | core-xplat   | `Sources/Tools/whfmt.Backfill`                      |
| whfmt.CodeGen                          | core-xplat   | `Sources/Tools/whfmt.CodeGen`                       |
| whfmt.Fuzz                             | core-xplat   | `Sources/Tools/whfmt.Fuzz`                          |
| whfmt.Validate                         | core-xplat   | `Sources/Tools/whfmt.Validate`                      |
| WPFHexaEditor                          | wpf-control  | `Sources/Editors/WpfHexEditor.HexEditor`            |
| WpfCodeEditor                          | wpf-control  | `Sources/Editors/WpfHexEditor.Editor.CodeEditor`    |
| WpfDocking                             | wpf-control  | `Sources/Docking/WpfHexEditor.Docking.Wpf`          |
| WpfColorPicker                         | wpf-control  | `Sources/Controls/WpfHexEditor.ColorPicker`         |
| WpfTerminal                            | wpf-control  | `Sources/Controls/WpfHexEditor.Terminal`            |

**Not in solution / not protected here:** `WpfCaret` (lives in another
repo). Any csproj whose `<PackageId>` does not match the table above is
silently ignored.

## Categories

- **core-xplat** — net8.0, zero WPF, zero WinForms, no IDE refs.
  Anything `System.Windows.*` or `Microsoft.Web.WebView2.*` is a hard fail.
- **wpf-control** — net8.0-windows + WPF allowed, but no reference to
  `WpfHexEditor.App.*`, `WpfHexEditor.Editor.*` (non-`.Core`), or
  `WpfHexEditor.Plugins.*`.

## When I invoke

| Situation                                                              | Run? |
|------------------------------------------------------------------------|------|
| Edit a `*.cs` under a protected package                                | yes  |
| Edit a `*.csproj` of a protected package                               | yes  |
| Edit a `*.cs` under a non-protected csproj                             | no   |
| Edit `Tests/`, `Samples/`, `*.Designer.cs`, `*.g.cs`                   | no   |
| Edit XAML / resx / docs                                                | no (other skills cover those) |

## Pipeline

1. Resolve each edited file's owning csproj. If its `<PackageId>` is not
   in the policy table → skip.
2. Run `scripts/nuget-guard.ps1 -Files <paths>`.
3. Output: one `ERR|WARN <rule> <file>:<line> <detail>` per finding.
4. Exit code = number of ERR findings (capped at 100). WARN-only ⇒ exit 0.

## Rules

| Rule                          | Severity | Detects                                                                                                              |
|-------------------------------|----------|----------------------------------------------------------------------------------------------------------------------|
| `nuget-api-removed`           | error    | A `public` type/member that was in `git HEAD` no longer exists in the working tree of a protected package's `.cs`.    |
| `nuget-api-renamed`           | error    | A `public` signature was removed AND a similar-named signature was added in the same file (rename/sig-edit breaks consumers). |
| `nuget-tfm-drift`             | error    | `<TargetFramework>` doesn't match the package's policy (e.g. ByteProvider must stay `net8.0`, never `net8.0-windows`). |
| `nuget-usewpf-leak`           | error    | `<UseWPF>true` or `<UseWindowsForms>true` on a `core-xplat` package.                                                  |
| `nuget-ide-projref`           | error    | `<ProjectReference>` added pointing to an IDE-only assembly (`WpfHexEditor.App*`, `WpfHexEditor.Editor.*`, `WpfHexEditor.Plugins.*`). `Editor.*.Core` is allowed (contracts only). |
| `nuget-ide-using`             | error    | Code references an IDE-only type (`IDEHostContext`, `IdeMessageBox`, `IDialogService`, `DockManager`, …) or `using` a forbidden IDE namespace prefix. |
| `nuget-wpf-using-in-xplat`    | error    | `using System.Windows.*` / `System.Windows.Forms` / `Microsoft.Web.WebView2.*` in a `core-xplat` package.             |
| `nuget-version-regression`    | error    | `<Version>` numerically lower than `git HEAD`. Bumps are allowed, regressions never.                                  |
| `nuget-release-notes-stale`   | warn     | `<Version>` bumped but `<PackageReleaseNotes>` unchanged vs `git HEAD`.                                               |

## What this skill explicitly allows

- **Extending** the public API (new types, new public members, new
  overloads) — never flagged.
- Changing `internal`/`private` members freely — invisible to consumers.
- Bumping `<Version>` upward (with updated release notes).
- Adding `PackageReference`s — package deps grow normally over time.

## What this skill does NOT do

- Does not run `dotnet pack` or validate the produced .nupkg layout.
- Does not enforce SemVer rigorously (no AST diff) — heuristic line-level
  signature comparison only. Major API surgery should still get a manual
  review.
- Does not block adding XAML/resx files (other skills cover those).

## Suppression marker

To silence a specific line (e.g. a deliberate cross-cutting reference
that's safe in this context), append `// nuget-ignore: <reason>` on that
line. The reason is mandatory for traceability.

## Maintenance

- New package published on nuget.org → add an entry in
  `data/package-policy.json`.
- IDE-only type added that shouldn't leak → add it to `ide_only_types`.
- New TFM rollout (e.g. net9.0) → update `tfm` arrays per package and bump
  in a single coordinated commit.

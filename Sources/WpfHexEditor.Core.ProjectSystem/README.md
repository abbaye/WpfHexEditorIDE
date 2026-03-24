# WpfHexEditor.ProjectSystem

Workspace and project model for the WpfHexEditor IDE — `.whsln` solution files, `.whproj` project files, serialization, migration, and runtime management services.

**License:** GNU Affero General Public License v3.0
**Target:** net8.0-windows · WPF

---

## Architecture / Modules

### Domain Model (`Models/`)

- **`Solution`** — implements `ISolution`; holds ordered `ObservableCollection<Project>` and `ObservableCollection<SolutionFolder>`; tracks `IsModified`, `StartupProject`, `SourceFormatVersion`, and `FormatUpgradeRequired`.
- **`Project`** — implements `IProject`; holds a collection of `ProjectItem` instances, build configuration, and project-level properties.
- **`ProjectItem`** — file or virtual item within a project; carries display name, file path, item type, and metadata dictionary.
- **`SolutionFolder`** — logical folder within the solution tree (equivalent to VS solution folders).
- **`VirtualFolder`** — in-project folder for grouping items without a corresponding file system directory.

### Serialization (`Serialization/`)

- **`SolutionSerializer`** — reads and writes `.whsln` files (JSON format).
- **`ProjectSerializer`** — reads and writes `.whproj` files (JSON format).
- **`SolutionUserSerializer`** — persists user-specific solution preferences (`.whsln.user`) such as open documents, panel layout, and last-used window positions.
- **`Migration/IFormatMigrator`** — contract for version-to-version format upgrades.
- **`Migration/MigrationPipeline`** — executes registered migrators in sequence; exposes `CurrentVersion` constant.
- **`Migration/V1ToV2Migrator`** — concrete migrator for the v1 → v2 format change.

### Services (`Services/`)

- **`SolutionManager`** — opens, closes, saves, and manages the active `Solution`; raises `SolutionOpened` / `SolutionClosed` / `SolutionModified` events consumed by the IDE shell.
- **`ChangesetService`** — tracks unsaved modifications across open projects; drives the "modified" dot on solution explorer nodes.
- **`MruService`** — persists and retrieves the Most Recently Used solution/file list for the File menu.

### Additional

- **`Dto/`** — JSON data-transfer objects used by the serializers.
- **`Languages/`** — language descriptor records (for syntax highlighting association per project item).
- **`Templates/`** — new project/item templates.
- **`Themes/`** — WPF resource dictionaries for Solution Explorer node styles.
- **`Dialogs/`** — New Solution and New Project dialogs.
- **`ProjectItemPropertyProvider`** — supplies property grid data for the selected project item (consumed by `PropertiesPanel`).

---

## File Formats

| Extension | Description |
|---|---|
| `.whsln` | Solution file — lists projects, solution folders, and solution-level settings |
| `.whproj` | Project file — lists items, references, and build configuration |
| `.whsln.user` | User-specific layout (not committed to source control) |

---

## Usage

```csharp
var manager = new SolutionManager();
await manager.OpenAsync(@"C:\Projects\MyApp\MyApp.whsln");

// Access the active solution
ISolution solution = manager.CurrentSolution!;
foreach (IProject project in solution.Projects) { /* ... */ }

// Save changes
await manager.SaveAsync();
```

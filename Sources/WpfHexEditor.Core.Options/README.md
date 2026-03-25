# WpfHexEditor.Options

IDE settings persistence and VS-style Options editor — `AppSettingsService`, `OptionsEditorControl`, and the page registry for plugin-contributed settings pages.

**License:** GNU Affero General Public License v3.0
**Target:** net8.0-windows · WPF

---

## Architecture / Modules

### Settings Persistence

- **`AppSettings`** — flat POCO model for all IDE-level settings (themes, editor defaults, behaviour flags, etc.).
- **`AppSettingsService`** — singleton that loads/saves `AppSettings` to `%AppData%\WpfHexEditor\settings.json` using `System.Text.Json`. Auto-creates the file on first save; silently resets to defaults on deserialization failure. Exposes `Instance`, `Current`, `FilePath`, `Load()`, and `Save()`.
- **`HexEditorDefaultSettings`** — strongly-typed defaults for the hex editor (byte grouping, column count, font, colours).

### Options Editor (`OptionsEditorControl`)

- **`OptionsEditorControl.xaml` / `.cs`** — VS2022-style Options dialog opened as a document tab in the docking area.
  - Left pane: tree of category nodes (populated from `OptionsPageRegistry`).
  - Right pane: selected page `UserControl` (lazily instantiated and cached in `_pageCache`).
  - Filter combo box for quick page search.
  - Changes are auto-saved immediately on any control-value change (`SettingsChanged` event).
  - "Edit JSON" button raises `EditJsonRequested(filePath)` so the caller can open the raw settings file in the code editor.

### Page Registry

- **`IOptionsPage`** — contract for settings pages: `Title`, `Category`, `BuildControl()` → `UserControl`, `Apply()`, `Reset()`.
- **`OptionsPageDescriptor`** — metadata record (title, category, icon, sort order) used to populate the tree without instantiating the page.
- **`OptionsPageRegistry`** — static registry; pages are registered at application startup or by plugins via `IPluginWithOptions`.

### Plugin Integration (`Pages/`)

Built-in options pages:

- General IDE appearance
- Hex editor display defaults
- Terminal settings
- Theme selection

Plugins contribute additional pages by implementing `IPluginWithOptions`; `WpfPluginHost` auto-registers them via `PluginOptionsRegistry` on plugin load and unregisters them on unload.

---

## Usage

```csharp
// Load at startup
AppSettingsService.Instance.Load();
var theme = AppSettingsService.Instance.Current.ThemeName;

// Save after a change
AppSettingsService.Instance.Current.ThemeName = "Dark";
AppSettingsService.Instance.Save();

// Register a custom options page
OptionsPageRegistry.Register(new OptionsPageDescriptor
{
    Title    = "My Plugin",
    Category = "Plugins",
    Factory  = () => new MyPluginOptionsPage()
});
```

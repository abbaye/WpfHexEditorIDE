# WpfHexEditor.PluginSandbox

Out-of-process plugin execution host for sandbox-isolated plugins (Phase 5 stub).

**License:** GNU Affero General Public License v3.0
**Target:** net8.0-windows · Console executable (OutputType=Exe)

---

## Architecture / Modules

This project is a standalone executable invoked by `WpfPluginHost` when a plugin's `manifest.json` declares `"isolationMode": "Sandbox"`. Running the plugin in a separate process provides fault isolation: a crashing plugin cannot destabilize the IDE.

- **`Program`** — entry point. Accepts three arguments: `<pipeName> <assemblyPath> <entryType>`.
  1. Connects to the host via a `NamedPipeClientStream` (bidirectional, async).
  2. Loads the plugin assembly with `Assembly.LoadFrom` and creates the `IWpfHexEditorPlugin` instance.
  3. Runs an IPC message loop dispatching `SandboxMessage` frames (`Method` + `Payload`).
- **`IpcChannel`** — wraps the named pipe with typed JSON serialization for `SandboxMessage` frames (`SendAsync` / `ReceiveAsync`).
- **`SandboxMessage`** — data record exchanged over the pipe (`Method`, `Payload`).

### Current state (Phase 5 stub)

The message loop currently handles only the `Shutdown` method. Full IPC proxying of `IIDEHostContext` service calls (menu registration, hex editor access, event bus, etc.) is planned for Phase 5. `WpfPluginHost` raises `NotSupportedException` if a plugin with `IsolationMode.Sandbox` is loaded before Phase 5 is complete.

### IPC Protocol (planned)

```
Host  →  Sandbox : { "Method": "Initialize", "Payload": "<serialized context>" }
Sandbox → Host   : { "Method": "Initialized" }
Host  →  Sandbox : { "Method": "Shutdown" }
Sandbox → Host   : (process exits 0)
```

---

## Usage

```
WpfHexEditor.PluginSandbox.exe <pipeName> <assemblyPath> <entryType>
```

Not invoked directly — `WpfPluginHost` spawns this process automatically for sandbox-mode plugins.

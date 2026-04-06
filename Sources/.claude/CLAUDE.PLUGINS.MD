# Module: Plugins

Plugins must be: isolated, reloadable (hot reload), safe (no crash propagation).

Each plugin MUST include: editor integration, context menu, toolbar actions, search/replace support, status bar updates, settings panel.

Lifecycle: Load → Initialize → Execute → Unload (clean).

Modes: in-process (fast) vs sandbox (safe). Choose based on performance vs isolation tradeoff.

Validation: plugin reload works, no memory leaks, no UI freeze. ❌ Missing = incomplete.

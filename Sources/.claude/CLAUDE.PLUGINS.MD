\# 🧩 claude.plugins.md — WpfHexEditor Edition



\## Plugin Architecture



Plugins must be:



\* Isolated

\* Reloadable (hot reload)

\* Safe (no crash propagation)



\## Required Integration



Each plugin MUST include:



\* Editor integration

\* Context menu

\* Toolbar actions

\* Search / Replace support

\* Status bar updates

\* Settings panel



\## Lifecycle



\* Load

\* Initialize

\* Execute

\* Unload (clean)



\## Modes



\* In-process → fast

\* Sandbox → safe



Claude must choose mode based on:



\* performance vs isolation



\## Validation



\* Plugin reload works

\* No memory leaks

\* No UI freeze



❌ Missing = incomplete




# Module boundaries (WpfHexEditor)

Source of truth for who-may-reference-whom. Used by `pre-edit-check.ps1` and
`scope-impact.ps1` to flag boundary crossings.

## Layers (top = high level, bottom = low level)

```
WpfHexEditor.App                (UI shell, IDE host, modules)
    │
    ├─ Editors / Plugins        (DocumentEditor, AssemblyExplorer, Debugger, ...)
    │       │
    │       └─ WpfHexEditor.SDK (extensibility surface)
    │              │
    │              └─ Editor.Core  (services, dialogs, primitives)
    │
    ├─ HexEditor / CodeEditor    (independent rich editors)
    │
    └─ WPFHexaEditor             (autonomous control, zero App ref)
```

## Allowed references

| From            | May reference                                          |
|-----------------|--------------------------------------------------------|
| App             | everything                                             |
| Editors         | SDK, Editor.Core, HexEditor (host), CodeEditor (host)  |
| Plugins/*       | SDK, Editor.Core only                                  |
| SDK             | Editor.Core only                                       |
| Editor.Core     | nothing project-internal (leaf)                        |
| HexEditor       | Editor.Core, WPFHexaEditor                             |
| CodeEditor      | Editor.Core                                            |
| WPFHexaEditor   | nothing (autonomous control)                           |
| Services        | Editor.Core only                                       |

## Forbidden crossings (auto-flag)

- Plugin -> App : never. Plugins must not assume the host.
- WPFHexaEditor -> any sibling : autonomy is contractual.
- HexEditor <-> CodeEditor : the editors are siblings, not collaborators.
- Editor.Core -> any : leaf module.

## Memory anchors

- ADR-010 (debugger plugin -> core App module): legitimate exception, App
  *absorbed* the plugin; SDK extensibility preserved through interfaces.
- ADR-011 (assembly-explorer plugin -> core App module): same pattern.
- These ADRs are precedents, not licenses to repeat — discuss before merging
  another plugin into App.

## Practical use

When `scope-impact.ps1` reports `CrossBoundary=true`, that is a signal, not a
verdict. The next step is to check whether the crossing follows the allowed
table. If it does not, the change needs a PLAN that justifies it.

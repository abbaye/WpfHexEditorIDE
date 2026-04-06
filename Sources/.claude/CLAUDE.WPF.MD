# Module: WPF UI

- Global theme system mandatory. ResourceDictionaries required. No hardcoded styles.
- All panels must integrate with docking manager (dock/float/tab groups).
- Each panel: toolbar + ViewModel binding + strict MVVM.
- Virtualization required for large data. No full buffer rendering.

Validation: theme applied, MVVM respected, docking functional. ❌ Otherwise incomplete.

// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: Panels/DebugConsolePanel.xaml.cs
// Description:
//     Debug Console panel — output log, REPL input, and multi-session tab strip.
//     DataContext = DebugConsolePanelViewModel (output log).
//     Sessions = DebugSessionManagerViewModel (session tab strip).
// ==========================================================

using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Plugins.Debugger.ViewModels;

namespace WpfHexEditor.Plugins.Debugger.Panels;

public partial class DebugConsolePanel : UserControl
{
    private DebugSessionManagerViewModel? _sessionMgr;

    public DebugConsolePanel() => InitializeComponent();

    /// <summary>
    /// Binds the session tab strip to the given view-model.
    /// Shows the strip only when more than one session is active.
    /// </summary>
    public void SetSessionManager(DebugSessionManagerViewModel vm)
    {
        if (_sessionMgr is not null)
            _sessionMgr.Sessions.CollectionChanged -= OnSessionsChanged;

        _sessionMgr = vm;
        SessionTabs.ItemsSource = vm.Sessions;
        vm.Sessions.CollectionChanged += OnSessionsChanged;
        RefreshStripVisibility();
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnReplKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (sender is not TextBox tb || DataContext is not DebugConsolePanelViewModel vm) return;

        var expr = tb.Text.Trim();
        if (string.IsNullOrEmpty(expr)) return;

        vm.Append("console", $"> {expr}\n");
        tb.Clear();
        e.Handled = true;
    }

    private void OnSessionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RefreshStripVisibility();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RefreshStripVisibility()
    {
        // Show strip only when ≥2 concurrent sessions exist.
        SessionStripBorder.Visibility = (_sessionMgr?.Sessions.Count ?? 0) >= 2
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}

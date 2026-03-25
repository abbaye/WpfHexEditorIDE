// ==========================================================
// Project: WpfHexEditor.Plugins.ScriptRunner
// File: Panels/ScriptRunnerPanel.xaml.cs
// Description:
//     Code-behind for the ScriptRunner dockable panel.
//     Handles F5/Ctrl+F5 keyboard shortcuts and auto-scrolls output.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Plugins.ScriptRunner.ViewModels;

namespace WpfHexEditor.Plugins.ScriptRunner.Panels;

/// <summary>
/// Dockable panel providing a code input area and script output pane.
/// </summary>
public partial class ScriptRunnerPanel : UserControl
{
    private readonly ScriptRunnerViewModel _vm;

    public ScriptRunnerPanel(ScriptRunnerViewModel vm)
    {
        _vm         = vm;
        DataContext = vm;
        InitializeComponent();

        // Auto-scroll output when text changes.
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ScriptRunnerViewModel.Output))
                Dispatcher.InvokeAsync(() => OutputBox.ScrollToEnd());
        };
    }

    // ── Keyboard shortcuts ────────────────────────────────────────────────────

    private void OnCodeBoxKeyDown(object sender, KeyEventArgs e)
    {
        // F5 → Run
        if (e.Key == Key.F5 && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (_vm.RunCommand.CanExecute(null))
                _vm.RunCommand.Execute(null);
            e.Handled = true;
        }
        // Escape → Cancel
        else if (e.Key == Key.Escape)
        {
            if (_vm.CancelCommand.CanExecute(null))
                _vm.CancelCommand.Execute(null);
            e.Handled = true;
        }
    }
}

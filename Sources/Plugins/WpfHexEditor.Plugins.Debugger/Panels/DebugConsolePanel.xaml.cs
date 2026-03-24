using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Plugins.Debugger.ViewModels;

namespace WpfHexEditor.Plugins.Debugger.Panels;

public partial class DebugConsolePanel : UserControl
{
    public DebugConsolePanel() => InitializeComponent();

    private void OnReplKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (sender is not TextBox tb || DataContext is not DebugConsolePanelViewModel vm) return;

        var expr = tb.Text.Trim();
        if (string.IsNullOrEmpty(expr)) return;

        vm.Append("console", $"> {expr}\n");
        tb.Clear();
        e.Handled = true;
        // Actual evaluation is handled by the plugin via IDebuggerService.EvaluateAsync
    }
}

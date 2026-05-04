// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: Dialogs/QuickTracepointDialog.xaml.cs
// Description: Code-behind for lightweight "Set Tracepoint" dialog.
// Architecture: ThemedDialog; macro picker inserts text at caret; result via LogMessage property.
// ==========================================================

using System.Windows;
using System.Windows.Input;
using WpfHexEditor.Editor.Core.Views;

namespace WpfHexEditor.Plugins.Debugger.Dialogs;

public partial class QuickTracepointDialog : ThemedDialog
{
    /// <summary>The log message entered by the user. Empty if the dialog was cancelled.</summary>
    public string LogMessage { get; private set; } = string.Empty;

    public QuickTracepointDialog() => InitializeComponent();

    /// <summary>
    /// Opens the dialog for the given source location.
    /// Returns the log message string, or <c>null</c> if cancelled.
    /// </summary>
    public static string? Show(Window? owner, string filePath, int line)
    {
        var dlg = new QuickTracepointDialog();
        if (owner is not null) dlg.Owner = owner;
        dlg.ShowDialog();
        return dlg.DialogResult == true ? dlg.LogMessage : null;
    }

    private void OnOkClicked(object sender, RoutedEventArgs e)
    {
        LogMessage   = MessageBox.Text;
        DialogResult = true;
        Close();
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)  { OnOkClicked(sender, e);     e.Handled = true; }
        if (e.Key == Key.Escape) { OnCancelClicked(sender, e); e.Handled = true; }
    }

    private void OnMacroClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn) return;

        var macro = btn.Tag is string tag ? tag : string.Empty;
        var text  = macro.Length == 0 ? "{expression}" : macro;

        var caret = MessageBox.CaretIndex;
        MessageBox.Text = MessageBox.Text.Insert(caret, text);
        MessageBox.CaretIndex = caret + text.Length;
        MessageBox.Focus();
    }
}

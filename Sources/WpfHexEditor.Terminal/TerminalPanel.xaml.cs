//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WpfHexEditor.Terminal;

/// <summary>
/// VS-Like dockable Terminal panel.
/// Hosts toolbar, scrollable output, and command input row.
/// Keyboard: Enter = run, Up/Down = history navigation, Escape = cancel.
/// </summary>
public sealed partial class TerminalPanel : UserControl
{
    public TerminalPanelViewModel? ViewModel => DataContext as TerminalPanelViewModel;

    public TerminalPanel()
    {
        InitializeComponent();
        SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");

        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is IDisposable d) d.Dispose();
        Unloaded -= OnUnloaded;
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                ViewModel?.RunCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Up:
                ViewModel?.NavigateHistoryUp();
                e.Handled = true;
                break;
            case Key.Down:
                ViewModel?.NavigateHistoryDown();
                e.Handled = true;
                break;
            case Key.Escape:
                ViewModel?.CancelCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}

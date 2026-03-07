// ==========================================================
// Project: WpfHexEditor.Sample.Docking
// File: Panels/OutputPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-07
// Description:
//     Code-behind for OutputPanel. Exposes Log(message) to append
//     timestamped lines to the RichTextBox, and OnClear to empty it.
//
// Architecture Notes:
//     Theme: inherits DockBackgroundBrush, DockTabTextBrush from active theme.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace WpfHexEditor.Sample.Docking.Panels;

public partial class OutputPanel : UserControl
{
    public OutputPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Appends a timestamped message to the output log.
    /// </summary>
    public void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var paragraph = new Paragraph(new Run($"[{timestamp}]  {message}"))
        {
            Margin = new Thickness(0)
        };

        LogBox.Document.Blocks.Add(paragraph);

        // Auto-scroll to the latest line
        LogBox.ScrollToEnd();
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        LogBox.Document.Blocks.Clear();
    }
}

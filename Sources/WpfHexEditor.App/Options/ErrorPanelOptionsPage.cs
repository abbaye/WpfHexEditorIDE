// ==========================================================
// Project: WpfHexEditor.App
// File: Options/ErrorPanelOptionsPage.cs
// Description:
//     Options page for the Error List panel default visibility toggles.
//     Exposes ShowErrors, ShowWarnings, ShowMessages as checkboxes.
// Architecture Notes:
//     Code-behind-only UserControl implementing IOptionsPage.
//     Registered at startup via OptionsPageRegistry.RegisterDynamic.
//     Load/Flush read and write directly to AppSettings.ErrorPanel.
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core.Options;

namespace WpfHexEditor.App.Options;

/// <summary>
/// IDE options page — Error List panel default visibility.
/// </summary>
public sealed class ErrorPanelOptionsPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;

    private readonly CheckBox _showErrors;
    private readonly CheckBox _showWarnings;
    private readonly CheckBox _showMessages;

    public ErrorPanelOptionsPage()
    {
        Padding = new Thickness(16);

        var stack = new StackPanel { Orientation = Orientation.Vertical };

        stack.Children.Add(new TextBlock
        {
            Text       = "Error List",
            FontSize   = 16,
            FontWeight = FontWeights.SemiBold,
            Margin     = new Thickness(0, 0, 0, 12),
        });

        stack.Children.Add(MakeSectionHeader("DEFAULT VISIBILITY"));

        _showErrors   = MakeCheckBox("Show errors",   OnAnyChanged);
        _showWarnings = MakeCheckBox("Show warnings", OnAnyChanged);
        _showMessages = MakeCheckBox("Show messages", OnAnyChanged);

        stack.Children.Add(_showErrors);
        stack.Children.Add(_showWarnings);
        stack.Children.Add(_showMessages);

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = stack,
        };
    }

    public void Load(AppSettings settings)
    {
        var s = settings.ErrorPanel;
        _showErrors.IsChecked   = s.ShowErrors;
        _showWarnings.IsChecked = s.ShowWarnings;
        _showMessages.IsChecked = s.ShowMessages;
    }

    public void Flush(AppSettings settings)
    {
        var s = settings.ErrorPanel;
        s.ShowErrors   = _showErrors.IsChecked   == true;
        s.ShowWarnings = _showWarnings.IsChecked == true;
        s.ShowMessages = _showMessages.IsChecked == true;
    }

    private void OnAnyChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    private static CheckBox MakeCheckBox(string label, EventHandler handler)
    {
        var cb = new CheckBox { Content = label, Margin = new Thickness(0, 3, 0, 3) };
        cb.Checked   += (s, e) => handler(s, e);
        cb.Unchecked += (s, e) => handler(s, e);
        return cb;
    }

    private static TextBlock MakeSectionHeader(string text)
    {
        var tb = new TextBlock
        {
            Text       = text,
            FontSize   = 10,
            FontWeight = FontWeights.Bold,
            Margin     = new Thickness(0, 0, 0, 6),
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");
        return tb;
    }
}

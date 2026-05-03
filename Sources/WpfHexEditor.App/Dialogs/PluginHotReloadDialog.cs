// ==========================================================
// Project: WpfHexEditor.App
// File: Dialogs/PluginHotReloadDialog.cs
// Description:
//     Simple modal dialog that lets the user choose which loaded plugin
//     to hot-reload. Invoked by MainWindow.OnPluginHotReload.
// Architecture: Code-only WPF Window — no XAML dependency.
// ==========================================================

using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.App.Dialogs;

/// <summary>
/// Modal dialog presenting a list of loaded plugins and returning the
/// user's selection via <see cref="SelectedPluginId"/>.
/// </summary>
internal sealed class PluginHotReloadDialog : Window
{
    private readonly ComboBox _combo = new() { Margin = new Thickness(0, 0, 0, 12) };

    /// <summary>Plugin ID selected by the user, or <see langword="null"/> if cancelled.</summary>
    public string? SelectedPluginId { get; private set; }

    public PluginHotReloadDialog(IReadOnlyList<(string Id, string Name)> plugins)
    {
        Title           = "Hot-Reload Plugin";
        Width           = 380;
        SizeToContent   = SizeToContent.Height;
        ResizeMode      = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        foreach (var (id, name) in plugins)
            _combo.Items.Add(new ComboBoxItem { Content = name, Tag = id });

        if (_combo.Items.Count > 0)
            _combo.SelectedIndex = 0;

        var reloadBtn = new Button
        {
            Content  = "Reload",
            Width    = 90,
            IsDefault = true,
            Margin   = new Thickness(0, 0, 8, 0),
        };
        reloadBtn.Click += (_, _) =>
        {
            SelectedPluginId = (_combo.SelectedItem as ComboBoxItem)?.Tag as string;
            DialogResult = true;
        };

        var cancelBtn = new Button { Content = "Cancel", Width = 90, IsCancel = true };

        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { reloadBtn, cancelBtn },
        };

        var panel = new StackPanel
        {
            Margin   = new Thickness(16),
            Children =
            {
                new TextBlock
                {
                    Text       = "Choose a plugin to hot-reload:",
                    Margin     = new Thickness(0, 0, 0, 8),
                    FontWeight = FontWeights.SemiBold,
                },
                _combo,
                btnRow,
            },
        };

        Content = panel;
    }
}

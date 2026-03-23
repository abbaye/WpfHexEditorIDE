// ==========================================================
// Project: WpfHexEditor.PluginDev
// File: UI/PluginDevToolbar.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Plugin Developer Toolbar — code-behind UserControl, no XAML.
//     Provides Run / Rebuild / Hot-Reload / Stop / Package actions
//     plus plugin and configuration selectors.
//
// Architecture Notes:
//     Pattern: View + ViewModel (MVVM). Toolbar is data-bound to
//     PluginDevToolbarViewModel via WPF bindings on the ToolBar items.
//     Keyboard shortcuts: F5 = Run, Ctrl+Shift+R = Hot-Reload.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.PluginDev.Panels;

namespace WpfHexEditor.PluginDev.UI;

/// <summary>
/// Plugin developer toolbar with Run / Rebuild / Hot-Reload / Stop / Package actions.
/// </summary>
public sealed class PluginDevToolbar : UserControl
{
    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------

    private readonly PluginDevToolbarViewModel _vm;
    private readonly ComboBox _cbPlugin;
    private readonly ComboBox _cbConfig;

    // -----------------------------------------------------------------------
    // Construction
    // -----------------------------------------------------------------------

    public PluginDevToolbar(PluginDevLogViewModel log)
    {
        _vm = new PluginDevToolbarViewModel(log);

        DataContext = _vm;

        var toolbar = new ToolBar
        {
            Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30)),
        };

        // ── Run ──
        var btnRun = MakeButton("\u25B6  Run", _vm.RunCommand, "Run plugin (F5)");
        toolbar.Items.Add(btnRun);

        // ── Rebuild ──
        var btnRebuild = MakeButton("\u27F3  Rebuild", _vm.RebuildCommand, "Clean and rebuild plugin");
        toolbar.Items.Add(btnRebuild);

        // ── Hot-Reload ──
        var btnHotReload = MakeButton("\uD83D\uDD04  Hot-Reload", _vm.HotReloadCommand, "Rebuild and reload without restarting (Ctrl+Shift+R)");
        toolbar.Items.Add(btnHotReload);

        // ── Stop ──
        var btnStop = MakeButton("\u25A0  Stop", _vm.StopCommand, "Unload the plugin");
        toolbar.Items.Add(btnStop);

        toolbar.Items.Add(new Separator());

        // ── Package ──
        var btnPackage = MakeButton("\uD83D\uDCE6  Package", _vm.PackageCommand, "Create distributable .whxplugin package");
        toolbar.Items.Add(btnPackage);

        toolbar.Items.Add(new Separator());

        // ── Plugin selector label ──
        toolbar.Items.Add(new TextBlock
        {
            Text              = "Plugin:",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground        = Brushes.WhiteSmoke,
            Margin            = new Thickness(4, 0, 4, 0),
        });

        // ── Plugin ComboBox ──
        _cbPlugin = new ComboBox
        {
            Width               = 200,
            VerticalAlignment   = VerticalAlignment.Center,
            Margin              = new Thickness(0, 0, 8, 0),
        };
        _cbPlugin.SetBinding(ItemsControl.ItemsSourceProperty,
            new System.Windows.Data.Binding(nameof(PluginDevToolbarViewModel.AvailableProjects)));
        _cbPlugin.SetBinding(ComboBox.SelectedItemProperty,
            new System.Windows.Data.Binding(nameof(PluginDevToolbarViewModel.ActiveProjectPath)));
        toolbar.Items.Add(_cbPlugin);

        // ── Config label ──
        toolbar.Items.Add(new TextBlock
        {
            Text              = "Config:",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground        = Brushes.WhiteSmoke,
            Margin            = new Thickness(4, 0, 4, 0),
        });

        // ── Config ComboBox ──
        _cbConfig = new ComboBox
        {
            Width               = 90,
            VerticalAlignment   = VerticalAlignment.Center,
        };
        _cbConfig.SetBinding(ItemsControl.ItemsSourceProperty,
            new System.Windows.Data.Binding(nameof(PluginDevToolbarViewModel.Configurations)));
        _cbConfig.SetBinding(ComboBox.SelectedItemProperty,
            new System.Windows.Data.Binding(nameof(PluginDevToolbarViewModel.SelectedConfig))
            {
                Mode = System.Windows.Data.BindingMode.TwoWay,
            });
        toolbar.Items.Add(_cbConfig);

        Content = toolbar;

        // Register keyboard shortcuts at the UserControl level.
        InputBindings.Add(new KeyBinding(_vm.RunCommand,       Key.F5,    ModifierKeys.None));
        InputBindings.Add(new KeyBinding(_vm.HotReloadCommand, Key.R,     ModifierKeys.Control | ModifierKeys.Shift));
    }

    // -----------------------------------------------------------------------
    // Public access to ViewModel
    // -----------------------------------------------------------------------

    public PluginDevToolbarViewModel ViewModel => _vm;

    // -----------------------------------------------------------------------
    // Helper
    // -----------------------------------------------------------------------

    private static Button MakeButton(string label, ICommand command, string tooltip)
        => new()
        {
            Content             = label,
            Command             = command,
            ToolTip             = tooltip,
            Padding             = new Thickness(6, 2, 6, 2),
            Margin              = new Thickness(0, 0, 2, 0),
            Foreground          = Brushes.WhiteSmoke,
        };
}

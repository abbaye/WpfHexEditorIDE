// ==========================================================
// Project: WpfHexEditor.Plugins.UnitTesting
// File: Options/UnitTestingOptionsPage.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-24
// Updated: 2026-03-24 (ADR-UT-07 — removed GroupByClass/SortBy)
// Description:
//     Code-behind-only options page for the Unit Testing plugin.
//     Registered in IDE Options under "Testing > Unit Testing".
// ==========================================================

using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Plugins.UnitTesting.Options;

/// <summary>
/// Options page for the Unit Testing panel. Built entirely in code-behind
/// (no XAML file) for consistency with other plugin options pages.
/// </summary>
public sealed class UnitTestingOptionsPage : UserControl
{
    private readonly CheckBox _chkAutoRun    = new() { Content = "Automatically run tests after a successful build", Margin = new Thickness(0, 0, 0, 8) };
    private readonly CheckBox _chkAutoExpand = new() { Content = "Auto-expand detail pane when a test fails",       Margin = new Thickness(0, 0, 0, 8) };
    private readonly CheckBox _chkRatioBar   = new() { Content = "Show pass/fail/skip ratio bar",                   Margin = new Thickness(0, 0, 0, 8) };

    public UnitTestingOptionsPage()
    {
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock
        {
            Text       = "Run Behavior",
            FontWeight = FontWeights.SemiBold,
            Margin     = new Thickness(0, 0, 0, 8),
        });
        panel.Children.Add(_chkAutoRun);
        panel.Children.Add(_chkAutoExpand);

        panel.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 12) });

        panel.Children.Add(new TextBlock
        {
            Text       = "Display",
            FontWeight = FontWeights.SemiBold,
            Margin     = new Thickness(0, 0, 0, 8),
        });
        panel.Children.Add(_chkRatioBar);

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = panel,
        };

        Load();
    }

    // ── Public surface ────────────────────────────────────────────────────────

    public void Load()
    {
        var opts = UnitTestingOptions.Instance;
        _chkAutoRun.IsChecked    = opts.AutoRunOnBuild;
        _chkAutoExpand.IsChecked = opts.AutoExpandDetailOnFailure;
        _chkRatioBar.IsChecked   = opts.ShowRatioBar;
    }

    public void Save()
    {
        var opts = UnitTestingOptions.Instance;
        opts.AutoRunOnBuild           = _chkAutoRun.IsChecked    == true;
        opts.AutoExpandDetailOnFailure = _chkAutoExpand.IsChecked == true;
        opts.ShowRatioBar             = _chkRatioBar.IsChecked   == true;
        opts.Save();
    }
}

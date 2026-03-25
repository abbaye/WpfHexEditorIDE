// ==========================================================
// Project: WpfHexEditor.App
// File: Options/DebuggerOptionsPage.cs
// Description:
//     Options page for the integrated debugger.
//     Category: Debugger › General
//     Sections: Adapter, Launch Defaults.
//
// Architecture Notes:
//     Code-behind-only UserControl (no XAML) implementing IOptionsPage.
//     Reads/writes AppSettings.Debugger.
//     Browse button uses Microsoft.Win32.OpenFileDialog (no extra dependency).
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WpfHexEditor.Core.Options;

namespace WpfHexEditor.App.Options;

/// <summary>
/// IDE options page — Debugger › General.
/// Configures the debug adapter path and session launch defaults.
/// </summary>
public sealed class DebuggerOptionsPage : UserControl, IOptionsPage
{
    // ── IOptionsPage ─────────────────────────────────────────────────────────

    public event EventHandler? Changed;

    // ── UI fields ────────────────────────────────────────────────────────────

    private readonly TextBox  _adapterPathBox;
    private readonly CheckBox _stopAtEntryCheck;
    private readonly CheckBox _showReturnValuesCheck;

    private bool _loading;

    // ── Constructor ──────────────────────────────────────────────────────────

    public DebuggerOptionsPage()
    {
        // ── Adapter path ─────────────────────────────────────────────────────
        _adapterPathBox = new TextBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 4, 0),
        };
        _adapterPathBox.TextChanged += OnChanged;

        var browseButton = new Button
        {
            Content = "Browse…",
            Padding = new Thickness(8, 2, 8, 2),
        };
        browseButton.Click += OnBrowseAdapter;

        var adapterRow = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        adapterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        adapterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_adapterPathBox, 0);
        Grid.SetColumn(browseButton, 1);
        adapterRow.Children.Add(_adapterPathBox);
        adapterRow.Children.Add(browseButton);

        var pathHint = new TextBlock
        {
            Text       = "Leave empty to auto-detect netcoredbg / vsdbg.",
            FontStyle  = FontStyles.Italic,
            Opacity    = 0.6,
            Margin     = new Thickness(0, 2, 0, 4),
            TextWrapping = TextWrapping.Wrap,
        };

        // ── Launch defaults ───────────────────────────────────────────────────
        _stopAtEntryCheck = new CheckBox
        {
            Content = "Stop at program entry point (Main)",
            Margin = new Thickness(0, 4, 0, 4),
        };
        _stopAtEntryCheck.Checked   += OnChanged;
        _stopAtEntryCheck.Unchecked += OnChanged;

        _showReturnValuesCheck = new CheckBox
        {
            Content = "Show return values in Locals after each step",
            Margin = new Thickness(0, 4, 0, 4),
        };
        _showReturnValuesCheck.Checked   += OnChanged;
        _showReturnValuesCheck.Unchecked += OnChanged;

        // ── Root layout ───────────────────────────────────────────────────────
        var root = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin      = new Thickness(12, 8, 12, 8),
        };

        root.Children.Add(MakeSectionHeader("Adapter"));
        root.Children.Add(new TextBlock
        {
            Text   = "Debug adapter executable path:",
            Margin = new Thickness(0, 4, 0, 2),
        });
        root.Children.Add(adapterRow);
        root.Children.Add(pathHint);

        root.Children.Add(MakeSectionHeader("Launch Defaults"));
        root.Children.Add(_stopAtEntryCheck);
        root.Children.Add(_showReturnValuesCheck);

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = root,
        };
    }

    // ── IOptionsPage ─────────────────────────────────────────────────────────

    public void Load(AppSettings settings)
    {
        _loading = true;
        try
        {
            var s = settings.Debugger;
            _adapterPathBox.Text              = s.NetCoreDbgPath;
            _stopAtEntryCheck.IsChecked       = s.StopAtEntry;
            _showReturnValuesCheck.IsChecked  = s.ShowReturnValues;
        }
        finally
        {
            _loading = false;
        }
    }

    public void Flush(AppSettings settings)
    {
        var s = settings.Debugger;
        s.NetCoreDbgPath   = _adapterPathBox.Text.Trim();
        s.StopAtEntry      = _stopAtEntryCheck.IsChecked  == true;
        s.ShowReturnValues = _showReturnValuesCheck.IsChecked == true;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void OnChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnChanged(object sender, EventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnBrowseAdapter(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Select debug adapter executable",
            Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
        };

        if (!string.IsNullOrWhiteSpace(_adapterPathBox.Text))
            dlg.InitialDirectory = System.IO.Path.GetDirectoryName(_adapterPathBox.Text);

        if (dlg.ShowDialog() == true)
            _adapterPathBox.Text = dlg.FileName;
    }

    private static TextBlock MakeSectionHeader(string title) => new()
    {
        Text       = title,
        FontWeight = FontWeights.SemiBold,
        Margin     = new Thickness(0, 8, 0, 4),
    };
}

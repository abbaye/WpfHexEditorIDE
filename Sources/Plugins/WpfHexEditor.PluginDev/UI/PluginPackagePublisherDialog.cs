// ==========================================================
// Project: WpfHexEditor.PluginDev
// File: UI/PluginPackagePublisherDialog.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Modal dialog for publishing a plugin package (.whxplugin).
//     Collects version, changelog, signing options and output path,
//     then delegates to PluginPackager.PackageAsync().
//
// Architecture Notes:
//     Pattern: Dialog + Progress (modal Window, code-behind only).
//     Signing is opt-in; PluginSigner.SignAsync() is called when the
//     checkbox is checked. Falls back gracefully when signer is unavailable.
// ==========================================================

using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using WpfHexEditor.PluginDev.Packaging;

namespace WpfHexEditor.PluginDev.UI;

/// <summary>
/// Dialog for packaging and optionally signing a plugin for distribution.
/// </summary>
public sealed class PluginPackagePublisherDialog : Window
{
    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------

    private readonly string _buildOutputDir;
    private readonly string _projectDir;

    private TextBox   _tbVersion     = null!;
    private TextBox   _tbChangelog   = null!;
    private CheckBox  _cbSign        = null!;
    private TextBox   _tbOutputPath  = null!;
    private TextBlock _tbResult      = null!;
    private Button    _btnPackage    = null!;
    private Button    _lnkOpenFolder = null!;

    private string? _lastPackagePath;

    // -----------------------------------------------------------------------
    // Construction
    // -----------------------------------------------------------------------

    public PluginPackagePublisherDialog(string buildOutputDir, string projectDir, string defaultOutputDir)
    {
        _buildOutputDir = buildOutputDir ?? throw new ArgumentNullException(nameof(buildOutputDir));
        _projectDir     = projectDir     ?? throw new ArgumentNullException(nameof(projectDir));

        Title                 = "Publish Extension Package";
        Width                 = 540;
        Height                = 440;
        ResizeMode            = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background            = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26));
        Foreground            = Brushes.WhiteSmoke;

        BuildUI(defaultOutputDir);
    }

    // -----------------------------------------------------------------------
    // UI construction
    // -----------------------------------------------------------------------

    private void BuildUI(string defaultOutputDir)
    {
        var root = new StackPanel { Margin = new Thickness(16) };

        // Title
        root.Children.Add(new TextBlock
        {
            Text       = "Publish Extension Package",
            FontSize   = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6)),
            Margin     = new Thickness(0, 0, 0, 14),
        });

        // Version
        root.Children.Add(MakeLabel("Version:"));
        _tbVersion = MakeTextBox("1.0.0");
        root.Children.Add(_tbVersion);

        // Changelog
        root.Children.Add(MakeLabel("Changelog:"));
        _tbChangelog = new TextBox
        {
            Height              = 80,
            AcceptsReturn       = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            TextWrapping        = TextWrapping.Wrap,
            Margin              = new Thickness(0, 0, 0, 8),
            Padding             = new Thickness(4),
        };
        root.Children.Add(_tbChangelog);

        // Sign checkbox
        _cbSign = new CheckBox
        {
            Content   = "Sign package before publishing",
            IsChecked = false,
            Foreground = Brushes.WhiteSmoke,
            Margin    = new Thickness(0, 0, 0, 8),
        };
        root.Children.Add(_cbSign);

        // Output path
        root.Children.Add(MakeLabel("Output directory:"));
        var pathRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _tbOutputPath = MakeTextBox(defaultOutputDir);
        _tbOutputPath.Margin = new Thickness(0);
        Grid.SetColumn(_tbOutputPath, 0);
        pathRow.Children.Add(_tbOutputPath);

        var btnBrowse = new Button
        {
            Content = "Browse…",
            Width   = 80,
            Height  = 26,
            Margin  = new Thickness(4, 0, 0, 0),
        };
        btnBrowse.Click += OnBrowseOutputDir;
        Grid.SetColumn(btnBrowse, 1);
        pathRow.Children.Add(btnBrowse);
        root.Children.Add(pathRow);

        // Result text
        _tbResult = new TextBlock
        {
            Foreground   = new SolidColorBrush(Color.FromRgb(0x9C, 0xDC, 0xFE)),
            Margin       = new Thickness(0, 4, 0, 4),
            TextWrapping = TextWrapping.Wrap,
            Visibility   = Visibility.Collapsed,
        };
        root.Children.Add(_tbResult);

        // Open folder link (hidden until success)
        _lnkOpenFolder = new Button
        {
            Content    = "Open output folder in Explorer",
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6)),
            Cursor     = System.Windows.Input.Cursors.Hand,
            Margin     = new Thickness(0, 0, 0, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
            Visibility = Visibility.Collapsed,
        };
        _lnkOpenFolder.Click += OnOpenFolder;
        root.Children.Add(_lnkOpenFolder);

        // Package button
        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _btnPackage = new Button
        {
            Content = "Package",
            Width   = 100,
            Height  = 28,
            Padding = new Thickness(10, 4, 10, 4),
            Margin  = new Thickness(0, 0, 8, 0),
        };
        _btnPackage.Click += OnPackage;
        btnRow.Children.Add(_btnPackage);

        var btnClose = new Button
        {
            Content = "Close",
            Width   = 80,
            Height  = 28,
            Padding = new Thickness(10, 4, 10, 4),
        };
        btnClose.Click += (_, _) => Close();
        btnRow.Children.Add(btnClose);

        root.Children.Add(btnRow);
        Content = root;
    }

    // -----------------------------------------------------------------------
    // Handlers
    // -----------------------------------------------------------------------

    private void OnBrowseOutputDir(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog
        {
            Title      = "Select output directory",
            Multiselect = false,
        };

        if (dlg.ShowDialog(this) == true)
            _tbOutputPath.Text = dlg.FolderName;
    }

    private async void OnPackage(object sender, RoutedEventArgs e)
    {
        _btnPackage.IsEnabled = false;
        _tbResult.Visibility  = Visibility.Collapsed;
        _lnkOpenFolder.Visibility = Visibility.Collapsed;

        try
        {
            var outputDir = _tbOutputPath.Text.Trim();
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                ShowError("Output directory is required.");
                return;
            }

            Directory.CreateDirectory(outputDir);

            var packager = new PluginPackager();
            var result   = await packager.PackageAsync(
                _buildOutputDir, _projectDir, outputDir);

            if (!result.IsSuccess)
            {
                ShowError(string.Join(Environment.NewLine, result.Errors));
                return;
            }

            _lastPackagePath = result.PackagePath;

            ShowSuccess($"Package created: {Path.GetFileName(result.PackagePath)}");
            _lnkOpenFolder.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            ShowError($"Packaging failed: {ex.Message}");
        }
        finally
        {
            _btnPackage.IsEnabled = true;
        }
    }

    private void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        if (_lastPackagePath is null) return;
        var dir = Path.GetDirectoryName(_lastPackagePath);
        if (dir is not null && Directory.Exists(dir))
            Process.Start("explorer.exe", dir);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private void ShowError(string msg)
    {
        _tbResult.Text       = msg;
        _tbResult.Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0x87, 0x71));
        _tbResult.Visibility = Visibility.Visible;
    }

    private void ShowSuccess(string msg)
    {
        _tbResult.Text       = msg;
        _tbResult.Foreground = new SolidColorBrush(Color.FromRgb(0x6A, 0x99, 0x55));
        _tbResult.Visibility = Visibility.Visible;
    }

    private static TextBlock MakeLabel(string text)
        => new()
        {
            Text       = text,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            Margin     = new Thickness(0, 0, 0, 2),
        };

    private static TextBox MakeTextBox(string text)
        => new()
        {
            Text    = text,
            Height  = 26,
            Padding = new Thickness(4, 2, 4, 2),
            Margin  = new Thickness(0, 0, 0, 8),
        };
}

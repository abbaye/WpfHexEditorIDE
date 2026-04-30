// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Views/ExportProjectDialog.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Code-behind-only modal dialog for "Export as C# Project".
//     Lets the user pick an output folder, then calls AssemblyExportService
//     with progress reporting and cancellation support.
//
// Architecture Notes:
//     Pattern: Thin dialog view — delegates to AssemblyExportService.
//     No XAML file: all UI is constructed in the constructor (codebase convention).
// ==========================================================

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Core.AssemblyAnalysis.Models;
using WpfHexEditor.Plugins.AssemblyExplorer.Services;
using WpfHexEditor.Plugins.AssemblyExplorer.Properties;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Views;

/// <summary>
/// Modal dialog for exporting a loaded assembly to a C# project on disk.
/// Provides output-path picker, progress bar, and Cancel button.
/// </summary>
public sealed class ExportProjectDialog : Window
{
    private readonly AssemblyModel         _model;
    private readonly IDecompilerBackend    _backend;
    private readonly AssemblyExportService _exportService;

    private CancellationTokenSource? _cts;

    private readonly TextBox      _pathBox;
    private readonly WpfHexEditor.ProgressBar.Controls.LinearProgressBar _progressBar;
    private readonly TextBlock    _statusLabel;
    private readonly Button       _exportButton;
    private readonly Button       _cancelButton;

    public ExportProjectDialog(AssemblyModel model, IDecompilerBackend backend)
    {
        _model         = model   ?? throw new ArgumentNullException(nameof(model));
        _backend       = backend ?? throw new ArgumentNullException(nameof(backend));
        _exportService = new AssemblyExportService();

        Title               = $"Export '{model.Name}' as C# Project";
        Width               = 520;
        SizeToContent       = SizeToContent.Height;
        ResizeMode          = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar       = false;

        var root = new StackPanel { Margin = new Thickness(16), Orientation = Orientation.Vertical };

        // ── Output path row ────────────────────────────────────────────────
        root.Children.Add(new TextBlock
        {
            Text   = "Output folder:",
            Margin = new Thickness(0, 0, 0, 4),
            FontSize = 12
        });

        var pathRow = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        _pathBox = new TextBox
        {
            Text    = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Decompiled", model.Name),
            Padding = new Thickness(6, 4, 6, 4),
            FontSize = 12
        };
        DockPanel.SetDock(_pathBox, Dock.Left);

        var browseBtn = new Button
        {
            Content = "…",
            Width   = 36,
            Margin  = new Thickness(6, 0, 0, 0),
            Padding = new Thickness(4, 3, 4, 3)
        };
        browseBtn.Click += OnBrowseClick;
        DockPanel.SetDock(browseBtn, Dock.Right);

        pathRow.Children.Add(browseBtn);
        pathRow.Children.Add(_pathBox);
        root.Children.Add(pathRow);

        // ── Progress bar ───────────────────────────────────────────────────
        _progressBar = new WpfHexEditor.ProgressBar.Controls.LinearProgressBar
        {
            Height   = 18,
            BarHeight = 18,
            Progress = 0,
            Margin   = new Thickness(0, 0, 0, 6)
        };
        root.Children.Add(_progressBar);

        // ── Status label ───────────────────────────────────────────────────
        _statusLabel = new TextBlock
        {
            Text       = $"Ready to export {model.Types.Count:N0} types.",
            FontSize   = 11,
            Foreground = Brushes.Gray,
            Margin     = new Thickness(0, 0, 0, 16),
            TextWrapping = TextWrapping.Wrap
        };
        root.Children.Add(_statusLabel);

        // ── Buttons ────────────────────────────────────────────────────────
        var buttonRow = new StackPanel
        {
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        _exportButton = new Button
        {
            Content  = "Export",
            Width    = 90,
            Height   = 28,
            Margin   = new Thickness(0, 0, 8, 0),
            IsDefault = true
        };
        _exportButton.Click += OnExportClick;

        _cancelButton = new Button
        {
            Content  = "Close",
            Width    = 90,
            Height   = 28,
            IsCancel = true
        };
        _cancelButton.Click += OnCancelClick;

        buttonRow.Children.Add(_exportButton);
        buttonRow.Children.Add(_cancelButton);
        root.Children.Add(buttonRow);

        Content = root;
        Closed  += (_, _) => _cts?.Cancel();
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        // WPF-native folder picker: use SaveFileDialog with a fake "Select Folder" file
        // in the target directory and strip the file name on accept (common WPF pattern
        // when System.Windows.Forms is not referenced).
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title            = "Choose Output Folder",
            Filter           = "Folder|*.ThisIsNotAFile",
            FileName         = "Select Folder",
            CheckFileExists  = false,
            CheckPathExists  = true,
            ValidateNames    = false,
            InitialDirectory = _pathBox.Text.Trim()
        };

        if (dlg.ShowDialog(this) == true)
        {
            var selectedFolder = System.IO.Path.GetDirectoryName(dlg.FileName);
            if (!string.IsNullOrEmpty(selectedFolder))
                _pathBox.Text = selectedFolder;
        }
    }

    private async void OnExportClick(object sender, RoutedEventArgs e)
    {
        var outputDir = _pathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(outputDir))
        {
            MessageBox.Show("Please choose an output folder.", Title,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SetExporting(true);

        _cts = new CancellationTokenSource();
        var progress = new Progress<double>(value =>
        {
            _progressBar.Progress = value;
            _statusLabel.Text  = $"Exporting… {value * 100:F0}% of {_model.Types.Count:N0} types";
        });

        try
        {
            await _exportService.ExportToCSharpProjectAsync(
                _model, outputDir, _backend, progress, _cts.Token);

            _statusLabel.Foreground = Brushes.LimeGreen;
            _statusLabel.Text       = $"Export complete. Project written to:\n{outputDir}";
            _progressBar.Progress   = 1.0;
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Foreground = Brushes.OrangeRed;
            _statusLabel.Text       = AssemblyExplorerResources.AsmExplorer_ExportCancelled;
        }
        catch (Exception ex)
        {
            _statusLabel.Foreground = Brushes.OrangeRed;
            _statusLabel.Text       = $"Export failed: {ex.Message}";
        }
        finally
        {
            SetExporting(false);
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        DialogResult = false;
    }

    // ── UI state helpers ──────────────────────────────────────────────────────

    private void SetExporting(bool exporting)
    {
        _exportButton.IsEnabled = !exporting;
        _pathBox.IsEnabled      = !exporting;
        _cancelButton.Content   = exporting ? "Cancel" : "Close";
        _cancelButton.IsCancel  = !exporting;
    }
}

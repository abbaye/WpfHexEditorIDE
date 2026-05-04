// ==========================================================
// Project: WpfHexEditor.Plugins.StringExtractor
// File: Views/StringExtractorPanel.xaml.cs
// Description: Code-behind for the String Extractor panel.
//              Wires ViewModel, handles UI events, drives extraction.
// Architecture Notes:
//     Standalone-safe: SetContext() is optional. When null, navigate-to
//     and file-read operations are silently skipped.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using WpfHexEditor.Plugins.StringExtractor.Models;
using WpfHexEditor.Plugins.StringExtractor.Properties;
using WpfHexEditor.Plugins.StringExtractor.ViewModels;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.Plugins.StringExtractor.Views;

public sealed partial class StringExtractorPanel : UserControl
{
    private readonly StringExtractorViewModel _vm = new();
    private IIDEHostContext?                   _context;

    public StringExtractorPanel()
    {
        InitializeComponent();
        DataContext = _vm;

        // Wire up bindings that can't be expressed in pure XAML (imperative UI state).
        _vm.PropertyChanged += OnVmPropertyChanged;
        Unloaded            += (_, _) => _vm.PropertyChanged -= OnVmPropertyChanged;

        UpdateEmptyState();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StringExtractorViewModel.IsExtracting))
        {
            ProgressOverlay.Visibility = _vm.IsExtracting ? Visibility.Visible : Visibility.Collapsed;
            UpdateEmptyState();
        }
        else if (e.PropertyName is nameof(StringExtractorViewModel.HasResults)
                                or nameof(StringExtractorViewModel.HasFile))
        {
            UpdateEmptyState();
        }
    }

    public void SetContext(IIDEHostContext context)
    {
        _context = context;
        _vm.SetNavigateCallback(offset => context.HexEditor.SetSelection(offset, offset));
    }

    public void OnFileOpened()
    {
        _vm.Clear();
        _vm.HasFile = _context?.HexEditor.IsActive ?? false;
    }

    private async void OnExtractClick(object sender, RoutedEventArgs e)
    {
        if (_context is null || !_context.HexEditor.IsActive) return;

        SyncOptions();

        var fileSize = _context.HexEditor.FileSize;
        if (fileSize <= 0) return;

        // ReadBytes.length is int — cap at int.MaxValue (2 GB) to avoid overflow.
        const long MaxBytes = 512L * 1024 * 1024;
        long readLen = Math.Min(fileSize, MaxBytes);
        if (readLen > int.MaxValue)
        {
            _context.Output.Error("StringExtractor: file too large (max 2 GB).");
            return;
        }

        var data = _context.HexEditor.ReadBytes(0, (int)readLen);
        await _vm.ExtractAsync(data);
    }

    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        if (!_vm.HasResults) return;

        var dlg = new SaveFileDialog
        {
            Title  = StringExtractorResources.StringExtractor_ExportTitle,
            Filter = StringExtractorResources.StringExtractor_ExportFilter
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            if (dlg.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                _vm.ExportToCsv(dlg.FileName);
            else
                _vm.ExportToTxt(dlg.FileName);

            _context?.Output.Info(string.Format(
                StringExtractorResources.StringExtractor_ExportSuccess,
                _vm.Results.Count,
                dlg.FileName));
        }
        catch (Exception ex)
        {
            _context?.Output.Error($"StringExtractor export failed: {ex.Message}");
        }
    }

    private void OnFilterChanged(object sender, TextChangedEventArgs e)
        => _vm.FilterText = FilterBox.Text;

    private void OnResultDoubleClick(object sender, MouseButtonEventArgs e)
        => NavigateToSelected();

    private void OnNavigateTo(object sender, RoutedEventArgs e)
        => NavigateToSelected();

    private void OnCopyValue(object sender, RoutedEventArgs e)
    {
        if (ResultsList.SelectedItem is ExtractedString s)
            Clipboard.SetText(s.Value);
    }

    private void OnCopyOffset(object sender, RoutedEventArgs e)
    {
        if (ResultsList.SelectedItem is ExtractedString s)
            Clipboard.SetText(s.OffsetHex);
    }

    private void NavigateToSelected()
        => _vm.NavigateTo(ResultsList.SelectedItem as ExtractedString);

    private void SyncOptions()
    {
        if (int.TryParse(MinLengthBox.Text, out int min) && min >= 1)
            _vm.Options.MinLength = min;
        _vm.Options.ScanAscii   = AsciiCheck.IsChecked   ?? true;
        _vm.Options.ScanUtf8    = Utf8Check.IsChecked    ?? true;
        _vm.Options.ScanUtf16Le = Utf16LeCheck.IsChecked ?? true;
    }

    private void UpdateEmptyState()
    {
        if (_vm.IsExtracting || _vm.HasResults)
        {
            EmptyStateText.Visibility = Visibility.Collapsed;
            return;
        }
        EmptyStateText.Visibility = Visibility.Visible;
        EmptyStateText.Text = !_vm.HasFile
            ? StringExtractorResources.StringExtractor_NoFile
            : StringExtractorResources.StringExtractor_NoResults;
    }
}

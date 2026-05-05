// ==========================================================
// Project: WpfHexEditor.Plugins.EntropyVisualizer
// File: Views/EntropyVisualizerPanel.xaml.cs
// Description: Code-behind for the Entropy Visualizer panel.
//              Wires ViewModel, handles UI events, drives analysis.
// Architecture Notes:
//     Standalone-safe: SetContext() is optional. When null, navigate-to
//     and file-read operations are silently skipped.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Plugins.EntropyVisualizer.Models;
using WpfHexEditor.Plugins.EntropyVisualizer.Properties;
using WpfHexEditor.Plugins.EntropyVisualizer.ViewModels;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.Plugins.EntropyVisualizer.Views;

public sealed partial class EntropyVisualizerPanel : UserControl
{
    private readonly EntropyVisualizerViewModel _vm = new();
    private IIDEHostContext?                     _context;

    public EntropyVisualizerPanel()
    {
        InitializeComponent();
        DataContext = _vm;

        _vm.PropertyChanged     += OnVmPropertyChanged;
        Graph.NavigateRequested += OnGraphNavigateRequested;
        Graph.HoverChanged      += OnGraphHoverChanged;

        Unloaded += (_, _) =>
        {
            _vm.PropertyChanged     -= OnVmPropertyChanged;
            Graph.NavigateRequested -= OnGraphNavigateRequested;
            Graph.HoverChanged      -= OnGraphHoverChanged;
        };

        UpdateEmptyState();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EntropyVisualizerViewModel.IsAnalyzing)
                           or nameof(EntropyVisualizerViewModel.HasResults)
                           or nameof(EntropyVisualizerViewModel.HasFile))
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

    // ── Toolbar events ────────────────────────────────────────────────────────

    private async void OnAnalyzeClick(object sender, RoutedEventArgs e)
    {
        if (_context is null || !_context.HexEditor.IsActive) return;

        SyncOptions();

        var fileSize = _context.HexEditor.FileSize;
        if (fileSize <= 0) return;

        const long MaxBytes = 512L * 1024 * 1024;
        long readLen = Math.Min(fileSize, MaxBytes);
        if (readLen > int.MaxValue) return;

        var data = _context.HexEditor.ReadBytes(0, (int)readLen);
        await _vm.AnalyzeAsync(data);
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => _vm.Cancel();

    // ── Graph events ──────────────────────────────────────────────────────────

    private void OnGraphNavigateRequested(EntropyChunk chunk) => _vm.NavigateTo(chunk);

    private void OnGraphHoverChanged(EntropyChunk? chunk) => _vm.HoveredChunk = chunk;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SyncOptions()
    {
        if (int.TryParse(ChunkSizeBox.Text, out int cs) && cs >= 16)
            _vm.ChunkSize = cs;
    }

    private void UpdateEmptyState()
    {
        if (_vm.IsAnalyzing || _vm.HasResults)
        {
            EmptyStateText.Visibility = Visibility.Collapsed;
            return;
        }
        EmptyStateText.Visibility = Visibility.Visible;
        EmptyStateText.Text = !_vm.HasFile
            ? EntropyVisualizerResources.EntropyVisualizer_NoFile
            : EntropyVisualizerResources.EntropyVisualizer_NoResults;
    }
}

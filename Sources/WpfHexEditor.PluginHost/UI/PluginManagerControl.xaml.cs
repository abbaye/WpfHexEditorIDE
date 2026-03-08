//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace WpfHexEditor.PluginHost.UI;

/// <summary>Converts a null/non-null object to Visibility. Invert=true → null=Visible, non-null=Collapsed.</summary>
[ValueConversion(typeof(object), typeof(Visibility))]
public sealed class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isNull = value is null;
        bool visible = Invert ? !isNull : isNull;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>
/// Converts a count (int) to Visibility.
/// Default (no parameter): count == 0 → Visible, count > 0 → Collapsed (shows empty-state).
/// Parameter "invert": count == 0 → Collapsed, count > 0 → Visible (shows list).
/// </summary>
[ValueConversion(typeof(int), typeof(Visibility))]
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isEmpty = value is int count && count == 0;
        bool invert = parameter is string s && s == "invert";
        bool visible = invert ? !isEmpty : isEmpty;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>
/// Plugin Manager document tab — lists all plugins with live metrics and lifecycle actions.
/// Supports deferred DataContext wiring (layout restoration before plugin system is ready).
/// </summary>
public sealed partial class PluginManagerControl : UserControl
{
    /// <summary>
    /// Parameterless constructor — used when the layout is restored before the plugin system
    /// is initialised. The DataContext (PluginManagerViewModel) must be set afterwards.
    /// </summary>
    public PluginManagerControl()
    {
        InitializeComponent();
        SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");
        Unloaded += OnUnloaded;
    }

    public PluginManagerControl(PluginManagerViewModel viewModel) : this()
    {
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is IDisposable d) d.Dispose();
        Unloaded -= OnUnloaded;
    }

    // --- Context menu handlers (bound via XAML ContextMenu on ListBox items) ---

    private void OnContextEnable(object sender, RoutedEventArgs e)
        => GetSelectedItemVm()?.EnableCommand.Execute(null);

    private void OnContextDisable(object sender, RoutedEventArgs e)
        => GetSelectedItemVm()?.DisableCommand.Execute(null);

    private void OnContextReload(object sender, RoutedEventArgs e)
        => GetSelectedItemVm()?.ReloadCommand.Execute(null);

    private void OnContextUninstall(object sender, RoutedEventArgs e)
    {
        var vm = GetSelectedItemVm();
        if (vm is null) return;
        var result = System.Windows.MessageBox.Show(
            $"Uninstall '{vm.Name}'?\nThis action cannot be undone.",
            "Uninstall Plugin",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (result == System.Windows.MessageBoxResult.Yes)
            vm.UninstallCommand.Execute(null);
    }

    private void OnContextCopyId(object sender, RoutedEventArgs e)
    {
        var vm = GetSelectedItemVm();
        if (vm is not null) System.Windows.Clipboard.SetText(vm.Id);
    }

    private void OnContextCopyFault(object sender, RoutedEventArgs e)
    {
        var vm = GetSelectedItemVm();
        if (vm?.FaultMessage is not null) System.Windows.Clipboard.SetText(vm.FaultMessage);
    }

    private void OnContextOpenMonitor(object sender, RoutedEventArgs e)
    {
        // Bubble up to MainWindow via routed command
        var win = System.Windows.Window.GetWindow(this);
        if (win is null) return;
        // Raise the Tools > Plugin Monitor menu handler by walking the logical tree
        win.GetType().GetMethod("OnOpenPluginMonitor",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(win, [sender, e]);
    }

    // --- Drag-drop install (.whxplugin) ---

    private void OnPanelDragOver(object sender, DragEventArgs e)
    {
        if (IsValidPluginDrop(e))
        {
            e.Effects = DragDropEffects.Copy;
            DropHintOverlay.Visibility = Visibility.Visible;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnPanelDragLeave(object sender, DragEventArgs e)
    {
        DropHintOverlay.Visibility = Visibility.Collapsed;
    }

    private async void OnPanelDrop(object sender, DragEventArgs e)
    {
        DropHintOverlay.Visibility = Visibility.Collapsed;

        if (!IsValidPluginDrop(e)) return;
        if (DataContext is not PluginManagerViewModel vm) return;

        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (files is null) return;

        var packagePath = files.FirstOrDefault(f =>
            string.Equals(Path.GetExtension(f), ".whxplugin", StringComparison.OrdinalIgnoreCase));

        if (packagePath is not null)
            await vm.InstallFromDropAsync(packagePath);
    }

    private static bool IsValidPluginDrop(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return false;
        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        return files?.Any(f => string.Equals(
            Path.GetExtension(f), ".whxplugin", StringComparison.OrdinalIgnoreCase)) == true;
    }

    // --- Export context menu handlers ---

    private void OnContextExportDiagnostics(object sender, RoutedEventArgs e)
    {
        if (DataContext is PluginManagerViewModel vm)
            vm.ExportDiagnosticsCommand.Execute(null);
    }

    private void OnContextExportCrashReport(object sender, RoutedEventArgs e)
    {
        if (DataContext is PluginManagerViewModel vm)
            vm.ExportCrashReportCommand.Execute(null);
    }

    private PluginListItemViewModel? GetSelectedItemVm()
        => (DataContext as PluginManagerViewModel)?.SelectedPlugin;
}

//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.SDK.UI;

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
/// Implements <see cref="IEditorToolbarContributor"/> so the IDE's contextual toolbar pod
/// shows per-plugin actions (Enable / Disable / Reload / Uninstall / Cascade) only when
/// this tab is active — identical VS-like behaviour to TblEditor and ChangesetEditor.
/// </summary>
public sealed partial class PluginManagerControl : UserControl, IEditorToolbarContributor
{
    private ToolbarOverflowManager _overflowManager = null!;

    // -- IEditorToolbarContributor -----------------------------------------------

    private readonly ObservableCollection<EditorToolbarItem> _toolbarItems = [];
    public ObservableCollection<EditorToolbarItem> ToolbarItems => _toolbarItems;

    private EditorToolbarItem _tbEnable        = null!;
    private EditorToolbarItem _tbDisable       = null!;
    private EditorToolbarItem _tbReload        = null!;
    private EditorToolbarItem _tbUninstall     = null!;
    private EditorToolbarItem _tbCascadeUnload = null!;
    private EditorToolbarItem _tbCascadeReload = null!;

    // Minimal ICommand used exclusively for contextual toolbar items.
    private sealed class ToolbarCmd : ICommand
    {
        private readonly Action _execute;
        public event EventHandler? CanExecuteChanged;
        public ToolbarCmd(Action execute) => _execute = execute;
        public bool CanExecute(object? p) => true;
        public void Execute(object? p) => _execute();
    }

    // ─────────────────────────────────────────────────────────────────────────────

    public PluginManagerControl()
    {
        InitializeComponent();
        SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;

        Loaded += (_, _) =>
        {
            _overflowManager = new ToolbarOverflowManager(
                toolbarContainer:      ToolbarBorder,
                alwaysVisiblePanel:    ToolbarRightPanel,
                overflowButton:        ToolbarOverflowButton,
                overflowMenu:          OverflowContextMenu,
                groupsInCollapseOrder: new FrameworkElement[]
                {
                    TbgPluginDiag,    // [0] first to collapse
                    TbgPluginInstall, // [1] last to collapse
                });
            Dispatcher.InvokeAsync(_overflowManager.CaptureNaturalWidths, DispatcherPriority.Loaded);
        };
    }

    public PluginManagerControl(PluginManagerViewModel viewModel) : this()
    {
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is PluginManagerViewModel vm)
            vm.PropertyChanged -= OnViewModelPropertyChanged;
        if (DataContext is IDisposable d) d.Dispose();
        Unloaded -= OnUnloaded;
    }

    // -- IEditorToolbarContributor helpers ---------------------------------------

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is PluginManagerViewModel oldVm)
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        if (e.NewValue is PluginManagerViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
            BuildToolbarItems();
        }
    }

    private void BuildToolbarItems()
    {
        _tbEnable = new EditorToolbarItem
        {
            Icon    = "\uE768",
            Tooltip = "Enable plugin",
            IsEnabled = false,
            Command = new ToolbarCmd(() => GetSelectedItemVm()?.EnableCommand.Execute(null))
        };
        _tbDisable = new EditorToolbarItem
        {
            Icon    = "\uE769",
            Tooltip = "Disable plugin",
            IsEnabled = false,
            Command = new ToolbarCmd(() => GetSelectedItemVm()?.DisableCommand.Execute(null))
        };
        _tbReload = new EditorToolbarItem
        {
            Icon    = "\uE72C",
            Tooltip = "Reload plugin",
            IsEnabled = false,
            Command = new ToolbarCmd(() => GetSelectedItemVm()?.ReloadCommand.Execute(null))
        };
        _tbUninstall = new EditorToolbarItem
        {
            Icon    = "\uE74D",
            Tooltip = "Uninstall plugin",
            IsEnabled = false,
            Command = new ToolbarCmd(ExecuteToolbarUninstall)
        };
        _tbCascadeUnload = new EditorToolbarItem
        {
            Icon    = "\uE8BB",
            Label   = "Cascade Unload",
            Tooltip = "Unload this plugin and all dependents",
            IsEnabled = false,
            Command = new ToolbarCmd(() => GetSelectedItemVm()?.CascadeUnloadCommand.Execute(null))
        };
        _tbCascadeReload = new EditorToolbarItem
        {
            Icon    = "\uE72C",
            Label   = "Cascade Reload",
            Tooltip = "Reload this plugin and all dependents",
            IsEnabled = false,
            Command = new ToolbarCmd(() => GetSelectedItemVm()?.CascadeReloadCommand.Execute(null))
        };

        _toolbarItems.Clear();
        _toolbarItems.Add(_tbEnable);
        _toolbarItems.Add(_tbDisable);
        _toolbarItems.Add(new EditorToolbarItem { IsSeparator = true });
        _toolbarItems.Add(_tbReload);
        _toolbarItems.Add(new EditorToolbarItem { IsSeparator = true });
        _toolbarItems.Add(_tbUninstall);
        _toolbarItems.Add(new EditorToolbarItem { IsSeparator = true });
        _toolbarItems.Add(_tbCascadeUnload);
        _toolbarItems.Add(_tbCascadeReload);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PluginManagerViewModel.SelectedPlugin)) return;
        var hasSelection = GetSelectedItemVm() is not null;
        _tbEnable.IsEnabled        = hasSelection;
        _tbDisable.IsEnabled       = hasSelection;
        _tbReload.IsEnabled        = hasSelection;
        _tbUninstall.IsEnabled     = hasSelection;
        _tbCascadeUnload.IsEnabled = hasSelection;
        _tbCascadeReload.IsEnabled = hasSelection;
    }

    private void ExecuteToolbarUninstall()
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

    /// <summary>
    /// Pre-selects the plugin matching <paramref name="pluginId"/> in the list.
    /// Called by MainWindow after the Plugin Monitor requests "Open in Plugin Manager".
    /// </summary>
    public void SelectPlugin(string pluginId)
    {
        if (DataContext is not PluginManagerViewModel vm) return;
        vm.SelectedPlugin = vm.Plugins.FirstOrDefault(p => p.Id == pluginId);
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

    // ── Toolbar overflow ─────────────────────────────────────────────────────

    private void OnToolbarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged) _overflowManager?.Update();
    }

    private void OnOverflowButtonClick(object sender, RoutedEventArgs e)
    {
        OverflowContextMenu.PlacementTarget = ToolbarOverflowButton;
        OverflowContextMenu.Placement       = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        OverflowContextMenu.IsOpen          = true;
    }

    private void OnOverflowMenuOpened(object sender, RoutedEventArgs e)
    {
        _overflowManager?.SyncMenuVisibility();
    }

    private void OvfExportDiag_Click(object sender, RoutedEventArgs e)
        => (DataContext as PluginManagerViewModel)?.ExportDiagnosticsCommand.Execute(null);

    private void OvfInstall_Click(object sender, RoutedEventArgs e)
        => (DataContext as PluginManagerViewModel)?.InstallFromFileCommand.Execute(null);
}

// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: BindingInspectorPanel.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-18
// Description:
//     Dockable panel that shows all data bindings on the currently
//     selected design canvas element. Each row exposes:
//     Property | Path | Mode | Source | Status columns.
//
// Architecture Notes:
//     Observer — wired to XamlDesignerSplitHost.SelectedElementChanged.
//     Delegates reflection to BindingInspectorService (pure service, no WPF rendering).
//     VS-Like Panel Pattern — 26px toolbar + filter TextBox + ListView.
//     MEMORY.md rule: _vm is never nulled on OnUnloaded; OnLoaded re-subscribes.
//
// Theme: Global theme via XD_* and DockBackgroundBrush tokens (DynamicResource)
// ResourceDictionaries: WpfHexEditor.Shell/Themes/{Theme}/Colors.xaml
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WpfHexEditor.Editor.XamlDesigner.Services;
using WpfHexEditor.SDK.UI;

namespace WpfHexEditor.Editor.XamlDesigner.Panels;

/// <summary>
/// Binding Inspector dockable panel — displays all active bindings on the
/// currently selected element in the design canvas.
/// </summary>
public partial class BindingInspectorPanel : UserControl
{
    // ── State ─────────────────────────────────────────────────────────────────

    private readonly BindingInspectorPanelViewModel _vm = new();
    private ToolbarOverflowManager?                 _overflowManager;

    // ── Constructor ───────────────────────────────────────────────────────────

    public BindingInspectorPanel()
    {
        InitializeComponent();
        DataContext = _vm;

        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Exposes the ViewModel for external wiring by the plugin.</summary>
    public BindingInspectorPanelViewModel ViewModel => _vm;

    /// <summary>
    /// Updates the panel to show bindings on <paramref name="obj"/>.
    /// Pass null to clear the panel when no element is selected.
    /// </summary>
    public void SetTarget(DependencyObject? obj) => _vm.SetTarget(obj);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Safe re-subscribe on every load (panel lifecycle rule from MEMORY.md).
        TbxFilter.TextChanged -= OnFilterChanged;
        TbxFilter.TextChanged += OnFilterChanged;

        BtnRefresh.Click -= OnRefreshClick;
        BtnRefresh.Click += OnRefreshClick;

        _overflowManager ??= new ToolbarOverflowManager(
            ToolbarBorder,
            ToolbarRightPanel,
            ToolbarOverflowButton,
            null,
            new FrameworkElement[] { TbgFilter },
            leftFixedElements: null);

        Dispatcher.InvokeAsync(
            () => _overflowManager.CaptureNaturalWidths(),
            DispatcherPriority.Loaded);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Per MEMORY.md rule: never null _vm on unload — OnLoaded re-subscribes.
        TbxFilter.TextChanged -= OnFilterChanged;
        BtnRefresh.Click      -= OnRefreshClick;
    }

    // ── Toolbar handlers ──────────────────────────────────────────────────────

    private void OnFilterChanged(object sender, TextChangedEventArgs e)
        => _vm.FilterText = TbxFilter.Text;

    private void OnRefreshClick(object sender, RoutedEventArgs e)
        => _vm.Refresh();

    // ── Size changes ──────────────────────────────────────────────────────────

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (sizeInfo.WidthChanged)
            _overflowManager?.Update();
    }
}

// ==========================================================
// BindingInspectorPanelViewModel
// ==========================================================

/// <summary>
/// ViewModel for <see cref="BindingInspectorPanel"/>.
/// Calls <see cref="BindingInspectorService"/> to retrieve live binding data
/// and populates <see cref="Entries"/> for display.
/// </summary>
public sealed class BindingInspectorPanelViewModel : INotifyPropertyChanged
{
    // ── State ─────────────────────────────────────────────────────────────────

    private DependencyObject? _currentTarget;
    private string            _contextLabel = "No selection";
    private string            _filterText   = string.Empty;

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>All binding entries for the current target (unfiltered source).</summary>
    public ObservableCollection<BindingEntryViewModel> Entries { get; } = new();

    /// <summary>Filtered view of <see cref="Entries"/> applied to the ListView.</summary>
    public ObservableCollection<BindingEntryViewModel> FilteredEntries { get; } = new();

    /// <summary>Label shown in the panel header describing the current target.</summary>
    public string ContextLabel
    {
        get => _contextLabel;
        private set { _contextLabel = value; OnPropertyChanged(); }
    }

    /// <summary>Text used to filter entries by property name or path.</summary>
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (_filterText == value) return;
            _filterText = value;
            OnPropertyChanged();
            ApplyFilter();
        }
    }

    // ── INPC ──────────────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the target element and rebuilds the binding list.
    /// Pass null to display "No selection".
    /// </summary>
    public void SetTarget(DependencyObject? obj)
    {
        _currentTarget = obj;
        Rebuild();
    }

    /// <summary>Re-reads bindings from the current target (e.g. after a property change).</summary>
    public void Refresh() => Rebuild();

    // ── Private helpers ───────────────────────────────────────────────────────

    private void Rebuild()
    {
        Entries.Clear();
        FilteredEntries.Clear();

        if (_currentTarget is null)
        {
            ContextLabel = "No selection";
            return;
        }

        ContextLabel = _currentTarget.GetType().Name;

        var service  = new BindingInspectorService();
        var bindings = service.GetAllBindings(_currentTarget);

        foreach (var (dp, info) in bindings)
        {
            Entries.Add(new BindingEntryViewModel(dp.Name, info));
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredEntries.Clear();

        var filter = _filterText.Trim();

        foreach (var entry in Entries)
        {
            if (string.IsNullOrEmpty(filter)
                || entry.PropertyName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || entry.Path.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                FilteredEntries.Add(entry);
            }
        }
    }
}

// ==========================================================
// BindingEntryViewModel
// ==========================================================

/// <summary>
/// Display model for a single binding row in the Binding Inspector panel.
/// Maps <see cref="BindingInfo"/> record fields to user-readable strings.
/// </summary>
public sealed class BindingEntryViewModel
{
    /// <summary>Name of the DependencyProperty that carries this binding.</summary>
    public string PropertyName { get; }

    /// <summary>Binding Path value; "(none)" when empty.</summary>
    public string Path { get; }

    /// <summary>BindingMode as a display string.</summary>
    public string Mode { get; }

    /// <summary>
    /// Human-readable source description: ElementName, RelativeSource, Source type,
    /// or "(DataContext)" when no explicit source is set.
    /// </summary>
    public string Source { get; }

    /// <summary>Binding validity indicator: "OK" or "Error".</summary>
    public string Status { get; }

    public BindingEntryViewModel(string propertyName, BindingInfo info)
    {
        PropertyName = propertyName;
        Path         = string.IsNullOrEmpty(info.Path)        ? "(none)"        : info.Path;
        Mode         = info.Mode.ToString();
        Source       = ResolveSourceLabel(info);
        Status       = info.IsValid ? "OK" : "Error";
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static string ResolveSourceLabel(BindingInfo info)
    {
        if (!string.IsNullOrEmpty(info.ElementName))
            return $"Element: {info.ElementName}";

        if (!string.IsNullOrEmpty(info.RelativeSource))
            return $"Relative: {info.RelativeSource}";

        if (!string.IsNullOrEmpty(info.Source))
            return $"Source: {info.Source}";

        return "(DataContext)";
    }
}

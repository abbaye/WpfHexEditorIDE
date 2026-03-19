// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: BindingInspectorPanel.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-18
// Updated: 2026-03-19 — Overkill upgrade: StatusIcon/Color/Tooltip, TriggerBadge,
//                        ConverterDisplay, FallbackDisplay, TargetNullDisplay,
//                        ShowValidation, IsMultiBinding, ChildEntries, CopyExpression,
//                        context menu handlers, NavigateToSourceRequested event.
// Description:
//     Dockable panel that shows all data bindings on the currently
//     selected design canvas element. Each row exposes:
//     Status | Property | Path | Mode | Source | Converter | Trigger | Validation columns.
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
using System.Linq;
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

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when the user chooses "Go to Source" on a binding row.
    /// Argument is the element name or type name of the binding source.
    /// </summary>
    public event EventHandler<string?>? NavigateToSourceRequested;

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
        TbxFilter.TextChanged   -= OnFilterChanged;
        TbxFilter.TextChanged   += OnFilterChanged;
        BtnRefresh.Click        -= OnRefreshClick;
        BtnRefresh.Click        += OnRefreshClick;
        BtnCopyExpr.Click       -= OnCopyExpressionClick;
        BtnCopyExpr.Click       += OnCopyExpressionClick;
        BtnErrorsOnly.Checked   -= OnErrorsOnlyToggled;
        BtnErrorsOnly.Checked   += OnErrorsOnlyToggled;
        BtnErrorsOnly.Unchecked -= OnErrorsOnlyToggled;
        BtnErrorsOnly.Unchecked += OnErrorsOnlyToggled;

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
        TbxFilter.TextChanged   -= OnFilterChanged;
        BtnRefresh.Click        -= OnRefreshClick;
        BtnCopyExpr.Click       -= OnCopyExpressionClick;
        BtnErrorsOnly.Checked   -= OnErrorsOnlyToggled;
        BtnErrorsOnly.Unchecked -= OnErrorsOnlyToggled;
    }

    // ── Toolbar handlers ──────────────────────────────────────────────────────

    private void OnFilterChanged(object sender, TextChangedEventArgs e)
        => _vm.FilterText = TbxFilter.Text;

    private void OnRefreshClick(object sender, RoutedEventArgs e)
        => _vm.Refresh();

    private void OnErrorsOnlyToggled(object sender, RoutedEventArgs e)
        => _vm.ShowErrorsOnly = BtnErrorsOnly.IsChecked == true;

    private void OnValidateAll(object sender, RoutedEventArgs e)
        => _vm.Refresh();

    private void OnClearFilter(object sender, RoutedEventArgs e)
        => TbxFilter.Text = string.Empty;

    private void OnCopyExpressionClick(object sender, RoutedEventArgs e)
    {
        // Build a combined expression string for all currently selected / visible entries.
        var selected = BindingList.SelectedItem as BindingEntryViewModel
                       ?? (BindingList.Items.Count == 1 ? BindingList.Items[0] as BindingEntryViewModel : null);

        if (selected is not null)
        {
            Clipboard.SetText(selected.BuildBindingExpression());
            return;
        }

        // Nothing selected — copy all visible entries as multi-line.
        var lines = _vm.FilteredEntries
                       .Select(e => $"{e.PropertyName}: {e.BuildBindingExpression()}");
        Clipboard.SetText(string.Join(Environment.NewLine, lines));
    }

    // ── Context menu handlers ─────────────────────────────────────────────────

    private void OnCtxCopyBindingExpression(object sender, RoutedEventArgs e)
    {
        if (ResolveContextMenuEntry(sender) is { } entry)
            Clipboard.SetText(entry.BuildBindingExpression());
    }

    private void OnCtxGoToSource(object sender, RoutedEventArgs e)
    {
        if (ResolveContextMenuEntry(sender) is { } entry)
            NavigateToSourceRequested?.Invoke(this, entry.Source);
    }

    private void OnCtxInspectConverter(object sender, RoutedEventArgs e)
    {
        var entry = ResolveContextMenuEntry(sender);
        if (entry is { HasConverter: true })
            NavigateToSourceRequested?.Invoke(this, entry.ConverterDisplay);
    }

    private void OnBindingListContextMenuOpening(object sender, System.Windows.Controls.ContextMenuEventArgs e)
    {
        // Enable/disable "Inspect Converter" based on whether the selected row has a converter.
        if (CtxInspectConverter is not null)
            CtxInspectConverter.IsEnabled = (BindingList.SelectedItem as BindingEntryViewModel)?.HasConverter == true;
    }

    // ── Size changes ──────────────────────────────────────────────────────────

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (sizeInfo.WidthChanged)
            _overflowManager?.Update();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the <see cref="BindingEntryViewModel"/> from a context menu click.
    /// ContextMenu is on the ListView itself, so uses SelectedItem directly.
    /// </summary>
    private BindingEntryViewModel? ResolveContextMenuEntry(object menuItemSender)
    {
        if (menuItemSender is not MenuItem) return null;
        return BindingList.SelectedItem as BindingEntryViewModel;
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
    private string            _contextLabel   = "No selection";
    private string            _filterText     = string.Empty;
    private bool              _showErrorsOnly = false;

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

    /// <summary>When true, only binding entries with status Error are shown.</summary>
    public bool ShowErrorsOnly
    {
        get => _showErrorsOnly;
        set
        {
            if (_showErrorsOnly == value) return;
            _showErrorsOnly = value;
            OnPropertyChanged();
            ApplyFilter();
        }
    }

    /// <summary>Returns "{N} errors" when there are broken bindings, otherwise empty string.</summary>
    public string ErrorCountLabel
    {
        get
        {
            int count = Entries.Count(e => e.Status == "Error");
            return count > 0 ? $"{count} error{(count == 1 ? "" : "s")}" : string.Empty;
        }
    }

    /// <summary>True when at least one binding entry has an error status.</summary>
    public bool HasErrors => Entries.Any(e => e.Status == "Error");

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
            Entries.Add(new BindingEntryViewModel(dp.Name, info));

        ApplyFilter();

        // Refresh computed error-count properties after a full rebuild.
        OnPropertyChanged(nameof(ErrorCountLabel));
        OnPropertyChanged(nameof(HasErrors));
    }

    private void ApplyFilter()
    {
        FilteredEntries.Clear();

        var filter = _filterText.Trim();

        foreach (var entry in Entries)
        {
            // When ShowErrorsOnly is active, skip non-error entries.
            if (_showErrorsOnly && entry.Status != "Error") continue;

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
/// Maps <see cref="BindingInfo"/> record fields to user-readable strings
/// including status icons, trigger badges, converter and validation metadata.
/// </summary>
public sealed class BindingEntryViewModel
{
    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Name of the DependencyProperty that carries this binding.</summary>
    public string PropertyName { get; }

    /// <summary>Binding Path value; "(none)" when empty; "(MultiBinding)" for multi-bindings.</summary>
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

    // ── Status icon / color / tooltip ─────────────────────────────────────────

    /// <summary>
    /// Segoe MDL2 glyph for the status column:
    /// \uE73E (check) for OK, \uEA39 (error badge) for Error, \uE7BA for MultiBinding.
    /// </summary>
    public string StatusIcon { get; }

    /// <summary>
    /// DynamicResource key string for the status icon foreground brush.
    /// Consumers must resolve with FindResource / DynamicResource in XAML.
    /// </summary>
    public string StatusColorKey { get; }

    /// <summary>Human-readable tooltip: "Binding active" or "Binding failed: {expression status}".</summary>
    public string StatusTooltip { get; }

    // ── Trigger badge ─────────────────────────────────────────────────────────

    /// <summary>Short badge text for the UpdateSourceTrigger: "PC", "LF", "Ex", or "".</summary>
    public string TriggerBadge { get; }

    /// <summary>DynamicResource key for the trigger badge background brush.</summary>
    public string TriggerColorKey { get; }

    // ── Converter ─────────────────────────────────────────────────────────────

    /// <summary>"(none)" or converter class name with arrow decoration "→ MyConverter".</summary>
    public string ConverterDisplay { get; }

    /// <summary>True when a converter is present.</summary>
    public bool HasConverter { get; }

    // ── Fallback / null ───────────────────────────────────────────────────────

    /// <summary>"(not set)" or the actual FallbackValue string.</summary>
    public string FallbackDisplay { get; }

    /// <summary>"(not set)" or the actual TargetNullValue string.</summary>
    public string TargetNullDisplay { get; }

    // ── Validation ────────────────────────────────────────────────────────────

    /// <summary>True when either ValidatesOnDataErrors or ValidatesOnNotifyDataErrors is set.</summary>
    public bool ShowValidation { get; }

    // ── MultiBinding ──────────────────────────────────────────────────────────

    /// <summary>True for MultiBinding root rows.</summary>
    public bool IsMultiBinding { get; }

    /// <summary>Child binding view models for MultiBinding rows; empty list for normal bindings.</summary>
    public IReadOnlyList<BindingEntryViewModel> ChildEntries { get; }

    // ── Constructor ───────────────────────────────────────────────────────────

    public BindingEntryViewModel(string propertyName, BindingInfo info)
    {
        PropertyName = propertyName;
        IsMultiBinding = info.IsMultiBinding;

        Path   = BuildPathDisplay(info);
        Mode   = info.Mode.ToString();
        Source = ResolveSourceLabel(info);
        Status = info.IsValid ? "OK" : "Error";

        // Status icon / color / tooltip.
        if (IsMultiBinding)
        {
            StatusIcon      = "\uE7BA";  // Segoe MDL2: People
            StatusColorKey  = "XD_BindingStatusOkBrush";
            StatusTooltip   = "MultiBinding active";
        }
        else if (info.IsValid)
        {
            StatusIcon     = "\uE73E";   // Segoe MDL2: Accept (checkmark)
            StatusColorKey = "XD_BindingStatusOkBrush";
            StatusTooltip  = "Binding active";
        }
        else
        {
            StatusIcon     = "\uEA39";   // Segoe MDL2: ErrorBadge
            StatusColorKey = "XD_BindingStatusErrorBrush";
            StatusTooltip  = $"Binding failed: {Status}";
        }

        // Trigger badge.
        (TriggerBadge, TriggerColorKey) = ResolveTriggerBadge(info.UpdateSourceTrigger);

        // Converter.
        HasConverter     = !string.IsNullOrEmpty(info.Converter);
        ConverterDisplay = HasConverter ? $"\u2192 {info.Converter}" : "(none)";

        // Fallback / null values.
        FallbackDisplay   = string.IsNullOrEmpty(info.FallbackValue)   ? "(not set)" : info.FallbackValue;
        TargetNullDisplay = string.IsNullOrEmpty(info.TargetNullValue) ? "(not set)" : info.TargetNullValue;

        // Validation.
        ShowValidation = info.ValidatesOnDataErrors || info.ValidatesOnNotifyDataErrors;

        // Child entries for MultiBinding.
        ChildEntries = info.ChildBindings is { Count: > 0 }
            ? info.ChildBindings.Select((c, i) => new BindingEntryViewModel($"[{i}]", c)).ToList()
            : Array.Empty<BindingEntryViewModel>();
    }

    // ── Public helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a XAML-style binding expression string suitable for clipboard export:
    /// "{Binding Path=X, Mode=Y, Converter=Z, ...}".
    /// </summary>
    public string BuildBindingExpression()
    {
        if (IsMultiBinding)
            return $"{{MultiBinding Mode={Mode}, Converter={ConverterDisplay}}}";

        var parts = new List<string>();

        if (Path != "(none)")
            parts.Add($"Path={Path}");

        parts.Add($"Mode={Mode}");

        if (!string.IsNullOrEmpty(Source) && Source != "(DataContext)")
            parts.Add($"Source={Source}");

        if (HasConverter)
            parts.Add($"Converter={{StaticResource {ConverterDisplay.TrimStart('\u2192', ' ')}}}");

        if (FallbackDisplay != "(not set)")
            parts.Add($"FallbackValue={FallbackDisplay}");

        if (TargetNullDisplay != "(not set)")
            parts.Add($"TargetNullValue={TargetNullDisplay}");

        return $"{{{string.Join(", ", parts)}}}";
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static string BuildPathDisplay(BindingInfo info)
    {
        if (info.IsMultiBinding) return "(MultiBinding)";
        return string.IsNullOrEmpty(info.Path) ? "(none)" : info.Path;
    }

    private static string ResolveSourceLabel(BindingInfo info)
    {
        if (!string.IsNullOrEmpty(info.ElementName))
            return $"Element: {info.ElementName}";

        if (!string.IsNullOrEmpty(info.RelativeSource))
            return $"Relative: {info.RelativeSource}";

        if (!string.IsNullOrEmpty(info.Source) && info.Source != "(MultiBinding)")
            return $"Source: {info.Source}";

        if (info.IsMultiBinding)
            return "(MultiBinding)";

        return "(DataContext)";
    }

    private static (string Badge, string ColorKey) ResolveTriggerBadge(
        System.Windows.Data.UpdateSourceTrigger trigger) => trigger switch
    {
        System.Windows.Data.UpdateSourceTrigger.PropertyChanged
            => ("PC", "XD_BindingTriggerPropertyBrush"),
        System.Windows.Data.UpdateSourceTrigger.LostFocus
            => ("LF", "XD_BindingTriggerLostFocusBrush"),
        System.Windows.Data.UpdateSourceTrigger.Explicit
            => ("Ex", "XD_BindingTriggerExplicitBrush"),
        _
            => (string.Empty, "XD_BindingTriggerPropertyBrush"),
    };
}

// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Views/AssemblyDiffPanel.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     Code-behind for the assembly diff / version compare panel (Phase 5).
//     Also defines DiffKindToBrushConverter (local converter used only in this panel).
//
// Architecture Notes:
//     Theme: all brushes via DynamicResource (PFP_* tokens).
//     Pattern: MVVM — delegates to AssemblyDiffViewModel.
//     Panel is bottom-docked, default auto-hide, PreferredHeight=250.
//     DiffKindToBrushConverter maps DiffKind → semi-transparent accent brush so
//     the row background is visually distinct regardless of theme.
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.Core.AssemblyAnalysis.Services;
using WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.UI;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Views;

// ── DiffKind → Brush converter ────────────────────────────────────────────────

/// <summary>
/// Converts a <see cref="DiffKind"/> enum value to a semi-transparent background brush.
/// Added = green, Removed = red, Changed = amber.
/// Uses a low alpha (30) so the theme row text remains readable.
/// </summary>
public sealed class DiffKindToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush AddedBrush   = Freeze(Color.FromArgb(40,  80, 200,  80));
    private static readonly SolidColorBrush RemovedBrush = Freeze(Color.FromArgb(40, 220,  60,  60));
    private static readonly SolidColorBrush ChangedBrush = Freeze(Color.FromArgb(40, 220, 180,  40));
    private static readonly SolidColorBrush NeutralBrush = Freeze(Colors.Transparent);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is DiffKind kind
            ? kind switch
            {
                DiffKind.Added   => AddedBrush,
                DiffKind.Removed => RemovedBrush,
                DiffKind.Changed => ChangedBrush,
                _                => NeutralBrush
            }
            : NeutralBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    private static SolidColorBrush Freeze(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}

// ── Panel code-behind ─────────────────────────────────────────────────────────

/// <summary>
/// Dockable panel for assembly diff / version comparison.
/// </summary>
public partial class AssemblyDiffPanel : UserControl
{
    private ToolbarOverflowManager? _overflowManager;

    // ── Constructor ───────────────────────────────────────────────────────────

    public AssemblyDiffPanel(AssemblyExplorerViewModel explorerViewModel)
    {
        InitializeComponent();

        ViewModel   = new AssemblyDiffViewModel(explorerViewModel);
        DataContext = ViewModel;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public AssemblyDiffViewModel ViewModel { get; }

    /// <summary>Injects the IDE host context for theme registration.</summary>
    public void SetContext(IIDEHostContext context)
    {
        context.Theme.RegisterThemeAwareControl(this);
        Unloaded += (_, _) => context.Theme.UnregisterThemeAwareControl(this);
    }

    /// <summary>Pre-selects the baseline assembly by name (e.g. from a context menu).</summary>
    public void PreSelectBaseline(string assemblyName)
        => ViewModel.SelectedBaselineName = assemblyName;

    // ── Loaded ────────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _overflowManager = new ToolbarOverflowManager(
            toolbarContainer:      ToolbarBorder,
            alwaysVisiblePanel:    ToolbarRightPanel,
            overflowButton:        ToolbarOverflowButton,
            overflowMenu:          OverflowContextMenu,
            groupsInCollapseOrder: [TbgSelectors]);

        Dispatcher.InvokeAsync(
            _overflowManager.CaptureNaturalWidths,
            DispatcherPriority.Loaded);
    }

    // ── Toolbar ───────────────────────────────────────────────────────────────

    private void OnOverflowButtonClick(object sender, RoutedEventArgs e)
        => OverflowContextMenu.IsOpen = true;

    private void OnOverflowMenuOpened(object sender, RoutedEventArgs e)
        => _overflowManager?.SyncMenuVisibility();

    // ── DataGrid double-click ────────────────────────────────────────────────

    private void OnDiffRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DiffGrid.SelectedItem is DiffEntryViewModel row)
            row.NavigateTargetCommand.Execute(null);
    }
}

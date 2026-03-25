// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: Documents/ProjectPropertiesDocument.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Code-behind for the VS-Like Project Properties document tab.
//     Handles left-nav section switching, browse dialogs, save toast,
//     and provides StyleSelector + Converter used in the XAML.
//
// Architecture Notes:
//     Pattern: MVVM with code-behind for section visibility toggling,
//     Win32 browse dialog integration, and toast notification.
//     NavItemStyleSelector selects PP_NavHeaderStyle vs PP_NavItemStyle.
//     NullToCollapsedConverter hides validation errors when null.
// ==========================================================

using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;

namespace WpfHexEditor.Core.ProjectSystem.Documents;

/// <summary>
/// VS-Like project properties opened as a document tab.
/// DataContext must be a <see cref="ProjectPropertiesViewModel"/>.
/// </summary>
public partial class ProjectPropertiesDocument : UserControl
{
    // Maps sectionId → the StackPanel that displays that section.
    private readonly Dictionary<string, StackPanel> _sections = new();

    // Stored so we can unsubscribe before re-binding to a new VM (prevents handler accumulation).
    private PropertyChangedEventHandler? _vmPropertyChangedHandler;
    private ProjectPropertiesViewModel?  _boundVm;

    public ProjectPropertiesDocument()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded             += OnLoaded;
    }

    // -----------------------------------------------------------------------
    // Initialisation
    // -----------------------------------------------------------------------

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Register all section panels (must match sectionIds in BuildNavItems).
        // This is the only place _sections is populated — BindViewModel needs Loaded to have
        // fired before ShowSection() works, so we call BindViewModel here if the DataContext
        // was already set before Loaded (which is the normal WPF lifecycle order).
        _sections["app-general"]      = SectionAppGeneral;
        _sections["build"]            = SectionBuild;
        _sections["app-dependencies"] = SectionDependencies;
        _sections["global-usings"]    = SectionGlobalUsings;
        _sections["items"]            = SectionItems;
        _sections["references"]       = SectionReferences;
        _sections["app-win32"]        = SectionWin32Resources;
        _sections["package"]          = SectionPackage;
        _sections["debug"]            = SectionDebug;
        _sections["code-analysis"]    = SectionCodeAnalysis;

        if (DataContext is ProjectPropertiesViewModel vm)
            BindViewModel(vm);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Only call BindViewModel after Loaded so _sections is populated.
        // Before Loaded, OnLoaded will call BindViewModel itself.
        if (e.NewValue is ProjectPropertiesViewModel vm && _sections.Count > 0)
            BindViewModel(vm);
    }

    private void BindViewModel(ProjectPropertiesViewModel vm)
    {
        // Guard: avoid re-binding the same VM instance (prevents duplicate subscription
        // when both DataContextChanged and Loaded fire for the same VM).
        if (ReferenceEquals(vm, _boundVm)) return;

        // Unsubscribe the previous handler to prevent accumulation across context switches.
        if (_boundVm is not null && _vmPropertyChangedHandler is not null)
            _boundVm.PropertyChanged -= _vmPropertyChangedHandler;

        _boundVm = vm;

        // Title bar text
        TitleText.Text = $"{vm.ProjectName} — Propriétés";

        _vmPropertyChangedHandler = (_, args) =>
        {
            if (args.PropertyName is nameof(vm.ProjectName))
                TitleText.Text = $"{vm.ProjectName} — Propriétés";
            else if (args.PropertyName is nameof(vm.SaveCompleted) && vm.SaveCompleted)
                ShowSaveToast();
        };
        vm.PropertyChanged += _vmPropertyChangedHandler;

        // Populate read-only ListViews
        ItemsListView.ItemsSource        = vm.Items;
        ItemCountLabel.Text              = vm.ItemCountText;
        DepsListView.ItemsSource         = vm.References;
        RefsListView.ItemsSource         = vm.References;
        GlobalUsingsListView.ItemsSource = vm.GlobalUsings;

        // Disable nav group headers (not selectable)
        NavListBox.Loaded += (_, _) =>
        {
            foreach (NavItem item in vm.NavigationItems.Where(n => n.IsHeader))
            {
                if (NavListBox.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem c)
                {
                    c.IsEnabled = false;
                    c.Focusable = false;
                }
            }
        };

        // Show first leaf section
        ShowSection(vm.ActiveSection);
    }

    // -----------------------------------------------------------------------
    // Navigation
    // -----------------------------------------------------------------------

    private void OnNavSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavListBox.SelectedItem is NavItem { IsHeader: false } item)
            ShowSection(item.SectionId);
        else if (NavListBox.SelectedItem is NavItem { IsHeader: true })
            NavListBox.SelectedItem = null;
    }

    private void ShowSection(string sectionId)
    {
        foreach (var panel in _sections.Values)
            panel.Visibility = Visibility.Collapsed;

        if (_sections.TryGetValue(sectionId, out var target))
            target.Visibility = Visibility.Visible;
    }

    // -----------------------------------------------------------------------
    // Browse dialogs
    // -----------------------------------------------------------------------

    private void OnBrowseOutputPath(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Sélectionner le répertoire de sortie" };
        if (dlg.ShowDialog() == true && DataContext is ProjectPropertiesViewModel vm)
            vm.OutputPath = dlg.FolderName;
    }

    private void OnBrowseAppIcon(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Sélectionner une icône",
            Filter = "Icônes (*.ico)|*.ico|Tous les fichiers (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true && DataContext is ProjectPropertiesViewModel vm)
            vm.AppIconPath = dlg.FileName;
    }

    // -----------------------------------------------------------------------
    // Save toast (2 s auto-dismiss)
    // -----------------------------------------------------------------------

    private async void ShowSaveToast()
    {
        SaveToast.Visibility = Visibility.Visible;
        await Task.Delay(2000);
        SaveToast.Visibility = Visibility.Collapsed;
    }
}

// ---------------------------------------------------------------------------
// NavItemStyleSelector — selects PP_NavHeaderStyle vs PP_NavItemStyle
// ---------------------------------------------------------------------------

/// <summary>
/// Selects the appropriate <see cref="Style"/> for nav ListBox items
/// based on whether a <see cref="NavItem"/> is a group header or a leaf.
/// </summary>
public sealed class NavItemStyleSelector : StyleSelector
{
    /// <summary>Style applied to group-header items (non-selectable labels).</summary>
    public Style? HeaderStyle { get; set; }

    /// <summary>Style applied to selectable leaf items.</summary>
    public Style? LeafStyle { get; set; }

    public override Style? SelectStyle(object item, DependencyObject container)
        => item is NavItem { IsHeader: true } ? HeaderStyle : LeafStyle;
}

// ---------------------------------------------------------------------------
// NullToCollapsedConverter — collapses element when binding value is null/empty
// ---------------------------------------------------------------------------

/// <summary>
/// Returns <see cref="Visibility.Collapsed"/> when the value is <c>null</c> or empty string;
/// <see cref="Visibility.Visible"/> otherwise.
/// Used for inline validation error TextBlocks.
/// </summary>
public sealed class NullToCollapsedConverter : IValueConverter
{
    public static readonly NullToCollapsedConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null || (value is string s && string.IsNullOrEmpty(s))
            ? Visibility.Collapsed
            : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

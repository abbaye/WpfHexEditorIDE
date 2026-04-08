// ==========================================================
// Project: WpfHexEditor.PluginDev
// File: UI/PluginMarketplacePanel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     In-IDE plugin marketplace panel (code-behind UserControl, no XAML).
//     3 tabs: Browse / Installed / Updates.
//     Content-Id: "panel-marketplace"
//
// Architecture Notes:
//     Pattern: View — delegates all data operations to IPluginMarketplaceService.
//     Uses async/await to avoid blocking the UI thread during searches.
//     ListView items are refreshed in bulk (ObservableCollection not needed —
//     we reassign ItemsSource for simplicity).
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.PluginDev.Services;

namespace WpfHexEditor.PluginDev.UI;

/// <summary>
/// Content ID used to dock and identify this panel in the IDE layout.
/// </summary>
public static class MarketplacePanelIds
{
    public const string ContentId = "panel-marketplace";
}

/// <summary>
/// 3-tab panel: Browse / Installed / Updates.
/// </summary>
public sealed class PluginMarketplacePanel : UserControl
{
    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------

    private readonly IPluginMarketplaceService _svc;

    // Browse tab
    private TextBox  _tbSearch    = null!;
    private ListView _lvBrowse    = null!;

    // Installed tab
    private ListView _lvInstalled = null!;

    // Updates tab
    private ListView _lvUpdates   = null!;

    // -----------------------------------------------------------------------
    // Construction
    // -----------------------------------------------------------------------

    public PluginMarketplacePanel(IPluginMarketplaceService service)
    {
        _svc = service ?? throw new ArgumentNullException(nameof(service));
        BuildUI();
        _ = LoadFeaturedAsync();
    }

    // -----------------------------------------------------------------------
    // UI
    // -----------------------------------------------------------------------

    private void BuildUI()
    {
        Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26));

        var tabs = new TabControl
        {
            Background  = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
        };

        tabs.Items.Add(BuildBrowseTab());
        tabs.Items.Add(BuildInstalledTab());
        tabs.Items.Add(BuildUpdatesTab());

        Content = tabs;
    }

    // ── Browse tab ──────────────────────────────────────────────────────────

    private TabItem BuildBrowseTab()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Search bar
        var searchRow = new DockPanel { Margin = new Thickness(8, 8, 8, 4) };
        _tbSearch = new TextBox { Height = 26, Padding = new Thickness(4, 2, 4, 2) };
        _tbSearch.KeyDown += async (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Return)
                await SearchAsync(_tbSearch.Text);
        };
        DockPanel.SetDock(_tbSearch, Dock.Left);
        searchRow.Children.Add(_tbSearch);

        var btnSearch = new Button { Content = "Search", Width = 70, Height = 26, Margin = new Thickness(4, 0, 0, 0) };
        btnSearch.Click += async (_, _) => await SearchAsync(_tbSearch.Text);
        DockPanel.SetDock(btnSearch, Dock.Right);
        searchRow.Children.Add(btnSearch);

        Grid.SetRow(searchRow, 0);
        grid.Children.Add(searchRow);

        // Results list
        _lvBrowse = BuildPackageListView(OnInstallRequested);
        Grid.SetRow(_lvBrowse, 1);
        grid.Children.Add(_lvBrowse);

        return new TabItem { Header = "Browse", Content = grid };
    }

    // ── Installed tab ────────────────────────────────────────────────────────

    private TabItem BuildInstalledTab()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var toolbar = new ToolBar { Margin = new Thickness(0, 4, 0, 4) };
        var btnRefresh = new Button { Content = "⟳  Refresh" };
        btnRefresh.Click += async (_, _) => await RefreshInstalledAsync();
        toolbar.Items.Add(btnRefresh);
        Grid.SetRow(toolbar, 0);
        grid.Children.Add(toolbar);

        _lvInstalled = BuildPackageListView(OnUninstallRequested, actionLabel: "Uninstall");
        Grid.SetRow(_lvInstalled, 1);
        grid.Children.Add(_lvInstalled);

        return new TabItem { Header = "Installed", Content = grid };
    }

    // ── Updates tab ──────────────────────────────────────────────────────────

    private TabItem BuildUpdatesTab()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var toolbar = new ToolBar { Margin = new Thickness(0, 4, 0, 4) };
        var btnUpdateAll = new Button { Content = "Update All" };
        btnUpdateAll.Click += async (_, _) => await UpdateAllAsync();
        toolbar.Items.Add(btnUpdateAll);
        Grid.SetRow(toolbar, 0);
        grid.Children.Add(toolbar);

        _lvUpdates = BuildPackageListView(OnUpdateRequested, actionLabel: "Update");
        Grid.SetRow(_lvUpdates, 1);
        grid.Children.Add(_lvUpdates);

        return new TabItem { Header = "Updates", Content = grid };
    }

    // -----------------------------------------------------------------------
    // Data loading
    // -----------------------------------------------------------------------

    private async Task LoadFeaturedAsync()
    {
        try
        {
            var packages = await _svc.GetFeaturedAsync();
            PopulateListView(_lvBrowse, packages, "Install");
        }
        catch { /* best effort — panel remains empty */ }
    }

    private async Task SearchAsync(string query)
    {
        try
        {
            var results = await _svc.SearchAsync(query);
            PopulateListView(_lvBrowse, results, "Install");
        }
        catch { /* silently ignore search errors */ }
    }

    private async Task RefreshInstalledAsync()
    {
        // The local service returns packages from the local cache.
        // We re-use SearchAsync("") as a stand-in for "list all installed".
        try
        {
            var installed = await _svc.SearchAsync(string.Empty);
            PopulateListView(_lvInstalled, installed, "Uninstall");
        }
        catch { }
    }

    private async Task UpdateAllAsync()
    {
        var packages = (_lvUpdates.ItemsSource as IEnumerable<PackageRow>)?.ToList() ?? [];
        foreach (var row in packages)
        {
            try
            {
                var progress = new Progress<double>();
                await _svc.InstallAsync(row.Package, progress);
            }
            catch { /* best effort */ }
        }
    }

    // -----------------------------------------------------------------------
    // Action handlers
    // -----------------------------------------------------------------------

    private async void OnInstallRequested(MarketplacePackage pkg)
    {
        try
        {
            var progress = new Progress<double>();
            var ok = await _svc.InstallAsync(pkg, progress);
            MessageBox.Show(
                ok ? $"'{pkg.Name}' installed successfully." : $"Install failed for '{pkg.Name}'.",
                "Extension Marketplace",
                MessageBoxButton.OK,
                ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Install error: {ex.Message}", "Extension Marketplace",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnUninstallRequested(MarketplacePackage pkg)
    {
        var confirm = MessageBox.Show(
            $"Uninstall '{pkg.Name}'?", "Extension Marketplace",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            await _svc.UninstallAsync(pkg.Id);
            await RefreshInstalledAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Uninstall error: {ex.Message}", "Extension Marketplace",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnUpdateRequested(MarketplacePackage pkg)
    {
        _ = Task.Run(async () =>
        {
            var progress = new Progress<double>();
            await _svc.InstallAsync(pkg, progress);
        });
    }

    // -----------------------------------------------------------------------
    // List view builder
    // -----------------------------------------------------------------------

    private static ListView BuildPackageListView(
        Action<MarketplacePackage> onAction,
        string actionLabel = "Install")
    {
        var lv = new ListView
        {
            Background    = Brushes.Transparent,
            BorderBrush   = Brushes.Transparent,
            SelectionMode = SelectionMode.Single,
        };

        var gv = new GridView();
        gv.Columns.Add(new GridViewColumn { Header = "Name",     Width = 180, DisplayMemberBinding = new System.Windows.Data.Binding("Name") });
        gv.Columns.Add(new GridViewColumn { Header = "Author",   Width = 120, DisplayMemberBinding = new System.Windows.Data.Binding("Author") });
        gv.Columns.Add(new GridViewColumn { Header = "Version",  Width = 70,  DisplayMemberBinding = new System.Windows.Data.Binding("Version") });
        gv.Columns.Add(new GridViewColumn { Header = "Rating",   Width = 60,  DisplayMemberBinding = new System.Windows.Data.Binding("Rating") });
        gv.Columns.Add(new GridViewColumn { Header = "Downloads",Width = 80,  DisplayMemberBinding = new System.Windows.Data.Binding("Downloads") });
        gv.Columns.Add(new GridViewColumn
        {
            Header = actionLabel,
            Width  = 80,
            CellTemplate = BuildActionCellTemplate(actionLabel, lv, onAction),
        });
        lv.View = gv;
        return lv;
    }

    private static DataTemplate BuildActionCellTemplate(
        string label, ListView owner, Action<MarketplacePackage> onAction)
    {
        // Build via FrameworkElementFactory since we have no XAML.
        var btnFactory = new FrameworkElementFactory(typeof(Button));
        btnFactory.SetValue(Button.ContentProperty, label);
        btnFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(2));
        btnFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler((s, _) =>
        {
            // DataContext of the cell is the PackageRow from ListView.ItemsSource.
            if (s is Button { DataContext: PackageRow row })
                onAction(row.Package);
        }));

        return new DataTemplate { VisualTree = btnFactory };
    }

    private static void PopulateListView(
        ListView lv,
        IReadOnlyList<MarketplacePackage> packages,
        string _)
    {
        lv.ItemsSource = packages
            .Select(p => new PackageRow(p))
            .ToList();
    }

    // -----------------------------------------------------------------------
    // Row view-model
    // -----------------------------------------------------------------------

    private sealed class PackageRow(MarketplacePackage pkg)
    {
        public MarketplacePackage Package   { get; } = pkg;
        public string             Name      => pkg.Name;
        public string             Author    => pkg.Author;
        public string             Version   => pkg.Version;
        public string             Rating    => $"{pkg.StarRating:F1}★";
        public string             Downloads => pkg.DownloadCount.ToString("N0");
    }
}

// ==========================================================
// Project: WpfHexEditor.Core.Options
// File: Pages/MarketplaceOptionsPage.xaml.cs
// Description:
//     Options page for the Plugin Marketplace.
//     Category: Plugins, Page: Marketplace.
//     Covers: GitHub PAT, auto-update toggle, update interval.
// Architecture Notes:
//     Pattern: IOptionsPage code-behind (same as PluginSystemOptionsPage).
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Core.Options.Pages;

/// <summary>
/// Options page for the Plugin Marketplace (GitHub token + auto-update settings).
/// </summary>
public sealed partial class MarketplaceOptionsPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;
    private bool _loading;

    public MarketplaceOptionsPage() => InitializeComponent();

    // ── IOptionsPage ──────────────────────────────────────────────────────────

    public void Load(AppSettings s)
    {
        _loading = true;
        try
        {
            BoxGitHubToken.Password   = s.Marketplace.GitHubToken;
            CheckAutoUpdate.IsChecked = s.Marketplace.AutoCheckUpdates;
            BoxInterval.Text          = s.Marketplace.UpdateCheckIntervalHours.ToString();
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings s)
    {
        s.Marketplace.GitHubToken            = BoxGitHubToken.Password.Trim();
        s.Marketplace.AutoCheckUpdates       = CheckAutoUpdate.IsChecked == true;
        s.Marketplace.UpdateCheckIntervalHours =
            int.TryParse(BoxInterval.Text, out var h) ? Math.Max(1, h) : 24;
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }
}

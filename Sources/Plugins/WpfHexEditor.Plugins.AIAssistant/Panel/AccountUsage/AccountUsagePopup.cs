// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: AccountUsagePopup.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-04-02
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     VS-style Account & Usage popup showing provider account info
//     and rate-limit usage bars. Code-behind Window (no XAML).
// ==========================================================
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using WpfHexEditor.Plugins.AIAssistant.Connection;
using WpfTextBlock = System.Windows.Controls.TextBlock;

namespace WpfHexEditor.Plugins.AIAssistant.Panel.AccountUsage;

public sealed class AccountUsagePopup : Window
{
    private static readonly Brush s_greenBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0)));
    private static readonly Brush s_orangeBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xD7, 0xA8, 0x4E)));
    private static readonly Brush s_redBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xD7, 0x4E, 0x4E)));
    private static readonly Brush s_barBgBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x40, 0x80, 0x80, 0x80)));

    private readonly StackPanel _contentStack;
    private bool _closing;

    public AccountUsagePopup(
        string providerId,
        Window? owner = null,
        Point? anchor = null)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Width = 380;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 520;

        if (owner is not null)
        {
            Owner = owner;
            WindowStartupLocation = WindowStartupLocation.Manual;

            if (anchor.HasValue)
            {
                Left = anchor.Value.X - Width / 2;
                Top = anchor.Value.Y;
            }
            else
            {
                Left = owner.Left + (owner.Width - Width) / 2;
                Top = owner.Top + owner.Height * 0.18;
            }
        }

        Deactivated += (_, _) => SafeClose();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { SafeClose(); e.Handled = true; }
        };

        // ── Root border ─────────────────────────────────────────────────────
        var rootBorder = new Border
        {
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1.5),
            Padding = new Thickness(14),
            Effect = new DropShadowEffect
            {
                Direction = 315, ShadowDepth = 6, BlurRadius = 18, Opacity = 0.55, Color = Colors.Black
            }
        };
        rootBorder.SetResourceReference(Border.BackgroundProperty, "DockBackgroundBrush");
        rootBorder.SetResourceReference(Border.BorderBrushProperty, "CA_AccentBrandingBrush");

        var outerStack = new StackPanel();

        // ── Header ──────────────────────────────────────────────────────────
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new WpfTextBlock
        {
            Text = "Account & Usage",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        title.SetResourceReference(WpfTextBlock.ForegroundProperty, "DockMenuForegroundBrush");
        Grid.SetColumn(title, 0);
        headerGrid.Children.Add(title);

        var closeBtn = new Border
        {
            Width = 24, Height = 24,
            CornerRadius = new CornerRadius(3),
            Cursor = Cursors.Hand,
            Child = new WpfTextBlock
            {
                Text = "\uE8BB",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        ((WpfTextBlock)closeBtn.Child).SetResourceReference(WpfTextBlock.ForegroundProperty, "DockMenuForegroundBrush");
        closeBtn.MouseEnter += (s, _) => ((Border)s!).SetResourceReference(Border.BackgroundProperty, "DockTabHoverBrush");
        closeBtn.MouseLeave += (s, _) => ((Border)s!).Background = Brushes.Transparent;
        closeBtn.MouseLeftButtonDown += (_, _) => SafeClose();
        Grid.SetColumn(closeBtn, 1);
        headerGrid.Children.Add(closeBtn);
        outerStack.Children.Add(headerGrid);

        // ── Separator ───────────────────────────────────────────────────────
        outerStack.Children.Add(MakeSeparator(8));

        // ── Content (populated async) ───────────────────────────────────────
        _contentStack = new StackPanel();

        // Loading indicator
        var loadingText = new WpfTextBlock
        {
            Text = "Loading...",
            FontSize = 12,
            Opacity = 0.5,
            Margin = new Thickness(0, 8, 0, 8)
        };
        loadingText.SetResourceReference(WpfTextBlock.ForegroundProperty, "DockMenuForegroundBrush");
        _contentStack.Children.Add(loadingText);

        outerStack.Children.Add(_contentStack);
        rootBorder.Child = outerStack;
        Content = rootBorder;

        // Load data async
        Loaded += async (_, _) =>
        {
            try
            {
                var info = await UsageTracker.Instance.GetUsageAsync(providerId);
                _contentStack.Children.Clear();
                BuildContent(info);
            }
            catch
            {
                _contentStack.Children.Clear();
                var errText = MakeLabel("Failed to load usage data.", 12, 0.6);
                _contentStack.Children.Add(errText);
            }
        };
    }

    private void BuildContent(ProviderUsageInfo info)
    {
        // ── ACCOUNT section ─────────────────────────────────────────────────
        if (info.HasAccountData)
        {
            _contentStack.Children.Add(MakeSectionHeader("ACCOUNT"));

            AddKeyValueRow("Auth method", info.AuthMethod ?? "—");

            if (info.Email is not null)
                AddKeyValueRow("Email", info.Email);

            if (info.Plan is not null)
                AddKeyValueRow("Plan", info.Plan);

            _contentStack.Children.Add(MakeSeparator(10));
        }

        // ── USAGE section ───────────────────────────────────────────────────
        _contentStack.Children.Add(MakeSectionHeader("USAGE"));

        if (info.HasUsageData)
        {
            foreach (var bucket in info.Buckets)
            {
                // Label row: bucket name + reset info
                var labelGrid = new Grid { Margin = new Thickness(0, 6, 0, 2) };
                labelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                labelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var bucketLabel = MakeLabel(bucket.Label, 12, 1.0);
                Grid.SetColumn(bucketLabel, 0);
                labelGrid.Children.Add(bucketLabel);

                if (bucket.ResetsIn is not null)
                {
                    var resetLabel = MakeLabel(bucket.ResetsIn, 11, 0.5);
                    Grid.SetColumn(resetLabel, 1);
                    labelGrid.Children.Add(resetLabel);
                }

                _contentStack.Children.Add(labelGrid);

                // Progress bar + percentage
                var barGrid = new Grid { Margin = new Thickness(0, 0, 0, 2) };
                barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var progressBar = BuildProgressBar(bucket.UsedPercent);
                Grid.SetColumn(progressBar, 0);
                barGrid.Children.Add(progressBar);

                var percentText = MakeLabel($"{bucket.UsedPercent:F0}%", 11, 0.8);
                percentText.Margin = new Thickness(8, 0, 0, 0);
                percentText.FontWeight = FontWeights.SemiBold;
                Grid.SetColumn(percentText, 1);
                barGrid.Children.Add(percentText);

                _contentStack.Children.Add(barGrid);

                // Detail text (e.g. "450/500 requests")
                if (bucket.DetailText is not null)
                {
                    var detail = MakeLabel(bucket.DetailText, 10.5, 0.4);
                    detail.Margin = new Thickness(0, 0, 0, 4);
                    _contentStack.Children.Add(detail);
                }
            }

            // Last refreshed
            var refreshedText = MakeLabel(
                $"Last updated: {info.LastRefreshed:HH:mm:ss}", 10, 0.35);
            refreshedText.Margin = new Thickness(0, 6, 0, 0);
            _contentStack.Children.Add(refreshedText);
        }
        else
        {
            var noData = MakeLabel("Usage data not available for this provider.", 12, 0.5);
            noData.Margin = new Thickness(0, 6, 0, 6);
            _contentStack.Children.Add(noData);

            if (info.ProviderId == "claude-code")
            {
                var hint = MakeLabel("Usage details are available on claude.ai.", 11, 0.4);
                _contentStack.Children.Add(hint);
            }
        }

        // ── Manage link ─────────────────────────────────────────────────────
        if (info.ManageUrl is not null)
        {
            _contentStack.Children.Add(MakeSeparator(10));

            var link = new WpfTextBlock
            {
                Text = $"Manage usage on {GetDomainName(info.ManageUrl)}",
                FontSize = 12,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 2, 0, 0)
            };
            link.SetResourceReference(WpfTextBlock.ForegroundProperty, "CA_AccentBrandingBrush");
            link.MouseEnter += (s, _) => ((WpfTextBlock)s!).TextDecorations = TextDecorations.Underline;
            link.MouseLeave += (s, _) => ((WpfTextBlock)s!).TextDecorations = null;

            var url = info.ManageUrl;
            link.MouseLeftButtonDown += (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
                catch { /* ignore */ }
            };

            _contentStack.Children.Add(link);
        }
    }

    private void AddKeyValueRow(string key, string value)
    {
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var keyText = MakeLabel(key, 12, 0.5);
        Grid.SetColumn(keyText, 0);
        grid.Children.Add(keyText);

        var valText = MakeLabel(value, 12, 1.0);
        valText.HorizontalAlignment = HorizontalAlignment.Right;
        valText.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetColumn(valText, 1);
        grid.Children.Add(valText);

        _contentStack.Children.Add(grid);
    }

    private static Grid BuildProgressBar(double percent)
    {
        var container = new Grid { Height = 6, Margin = new Thickness(0, 1, 0, 0) };

        // Background track
        var bg = new Border
        {
            Background = s_barBgBrush,
            CornerRadius = new CornerRadius(3)
        };
        container.Children.Add(bg);

        // Foreground fill
        var clampedPercent = Math.Clamp(percent, 0, 100);
        var fill = new Border
        {
            Background = clampedPercent > 90 ? s_redBrush : clampedPercent > 70 ? s_orangeBrush : s_greenBrush,
            CornerRadius = new CornerRadius(3),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        fill.SetBinding(WidthProperty, new System.Windows.Data.Binding("ActualWidth")
        {
            Source = container,
            Converter = new PercentWidthConverter(clampedPercent)
        });
        container.Children.Add(fill);

        return container;
    }

    private static WpfTextBlock MakeLabel(string text, double fontSize, double opacity)
    {
        var tb = new WpfTextBlock
        {
            Text = text,
            FontSize = fontSize,
            Opacity = opacity
        };
        tb.SetResourceReference(WpfTextBlock.ForegroundProperty, "DockMenuForegroundBrush");
        return tb;
    }

    private static WpfTextBlock MakeSectionHeader(string text)
    {
        var tb = new WpfTextBlock
        {
            Text = text,
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            Opacity = 0.45,
            Margin = new Thickness(0, 2, 0, 2),
            // Letter spacing via CharacterSpacing is WinUI only; skip for WPF
        };
        tb.SetResourceReference(WpfTextBlock.ForegroundProperty, "DockMenuForegroundBrush");
        return tb;
    }

    private static Border MakeSeparator(double bottomMargin)
    {
        var sep = new Border { Height = 1, Margin = new Thickness(0, 4, 0, bottomMargin) };
        sep.SetResourceReference(Border.BackgroundProperty, "DockBorderBrush");
        return sep;
    }

    private static string GetDomainName(string url)
    {
        try { return new Uri(url).Host; }
        catch { return url; }
    }

    private void SafeClose()
    {
        if (_closing) return;
        _closing = true;
        try { Close(); } catch { }
    }

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }

    /// <summary>Converts a container's ActualWidth to a proportional width for the progress bar fill.</summary>
    private sealed class PercentWidthConverter : System.Windows.Data.IValueConverter
    {
        private readonly double _percent;
        public PercentWidthConverter(double percent) => _percent = percent;

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => value is double width ? width * _percent / 100.0 : 0.0;

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();
    }
}

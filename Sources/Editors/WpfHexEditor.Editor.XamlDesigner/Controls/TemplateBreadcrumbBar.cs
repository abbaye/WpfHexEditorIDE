// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: TemplateBreadcrumbBar.cs
// Description:
//     Overlay banner shown at the top of the design canvas when the user
//     enters template-edit scope. Displays a VS-style breadcrumb trail:
//     "UserControl > Button > ControlTemplate"
//     with an "× Exit" button on the right to leave the deepest scope level.
//
// Architecture Notes:
//     WPF Control built programmatically — no XAML file needed.
//     Driven by TemplateEditingService.ScopeStack.
//     Fires ExitRequested when the user clicks the exit button.
//     Theme-aware via XD_TemplateScopeBannerBrush token.
// ==========================================================

using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.XamlDesigner.Services;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Banner bar showing the current template-edit scope breadcrumb path.
/// </summary>
public sealed class TemplateBreadcrumbBar : Border
{
    private readonly StackPanel _breadcrumbPanel;
    private readonly Button     _exitButton;

    /// <summary>Fired when the user clicks the "× Exit" button.</summary>
    public event EventHandler? ExitRequested;

    public TemplateBreadcrumbBar()
    {
        var bannerBg = Application.Current?.TryFindResource("XD_TemplateScopeBannerBrush") as Brush
                       ?? new SolidColorBrush(Color.FromArgb(220, 30, 50, 70));

        Background      = bannerBg;
        BorderThickness = new Thickness(0, 0, 0, 1);
        BorderBrush     = new SolidColorBrush(Color.FromRgb(0x4F, 0xC1, 0xFF));
        Padding         = new Thickness(8, 4, 8, 4);
        Visibility      = Visibility.Collapsed;

        _exitButton = new Button
        {
            Content    = "× Exit Template",
            Padding    = new Thickness(8, 2, 8, 2),
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x79, 0xC6)),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x79, 0xC6)),
            Cursor      = Cursors.Hand,
            FontSize    = 10,
        };
        _exitButton.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _breadcrumbPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var row = new DockPanel();
        DockPanel.SetDock(_exitButton, Dock.Right);
        row.Children.Add(_exitButton);
        row.Children.Add(_breadcrumbPanel);
        Child = row;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Updates the breadcrumb trail from the given scope stack.
    /// Pass an empty list to hide the bar.
    /// </summary>
    public void Refresh(IReadOnlyList<TemplateScopeEntry> scopeStack)
    {
        _breadcrumbPanel.Children.Clear();

        if (scopeStack.Count == 0)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        Visibility = Visibility.Visible;

        for (int i = 0; i < scopeStack.Count; i++)
        {
            var entry = scopeStack[i];

            if (i > 0)
                _breadcrumbPanel.Children.Add(MakeSeparator());

            _breadcrumbPanel.Children.Add(MakeLabel(entry.ElementName, isBold: false));
            _breadcrumbPanel.Children.Add(MakeSeparator());
            _breadcrumbPanel.Children.Add(MakeLabel(entry.TemplateType, isBold: i == scopeStack.Count - 1));
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TextBlock MakeLabel(string text, bool isBold) => new()
    {
        Text       = text,
        Foreground = Brushes.White,
        FontSize   = 10,
        FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal,
        VerticalAlignment = VerticalAlignment.Center,
        Margin     = new Thickness(2, 0, 2, 0),
    };

    private static TextBlock MakeSeparator() => new()
    {
        Text       = " › ",
        Foreground = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)),
        FontSize   = 10,
        VerticalAlignment = VerticalAlignment.Center,
    };
}

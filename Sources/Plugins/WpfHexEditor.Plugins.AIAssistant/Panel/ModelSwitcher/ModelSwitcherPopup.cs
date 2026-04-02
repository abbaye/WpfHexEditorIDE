// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: ModelSwitcherPopup.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-04-01
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     WPF Popup-based model/provider selector. Grouped by provider,
//     shows context tokens and thinking toggle. Closes on outside click.
// ==========================================================
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using WpfHexEditor.Plugins.AIAssistant.Api;
using WpfTextBlock = System.Windows.Controls.TextBlock;

namespace WpfHexEditor.Plugins.AIAssistant.Panel.ModelSwitcher;

public sealed class ModelSwitcherPopup : Popup
{
    public string? SelectedProviderId { get; private set; }
    public string? SelectedModelId { get; private set; }
    public bool ThinkingEnabled { get; private set; }

    public event EventHandler? SelectionCommitted;

    public ModelSwitcherPopup(
        ModelRegistry registry,
        string currentProviderId,
        string currentModelId,
        bool thinkingEnabled)
    {
        SelectedProviderId = currentProviderId;
        SelectedModelId = currentModelId;
        ThinkingEnabled = thinkingEnabled;

        StaysOpen = false;
        AllowsTransparency = true;
        Placement = PlacementMode.Bottom;
        PopupAnimation = PopupAnimation.Fade;

        var rootBorder = new Border
        {
            Width = 320,
            MaxHeight = 400,
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            Effect = new DropShadowEffect
            {
                Direction = 315, ShadowDepth = 4, BlurRadius = 12, Opacity = 0.45, Color = Colors.Black
            }
        };
        rootBorder.SetResourceReference(Border.BackgroundProperty, "DockBackgroundBrush");
        rootBorder.SetResourceReference(Border.BorderBrushProperty, "DockBorderBrush");

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        var stack = new StackPanel();

        foreach (var provider in registry.Providers)
        {
            var header = new WpfTextBlock
            {
                Text = provider.DisplayName.ToUpperInvariant(),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(4, 8, 0, 4),
                Opacity = 0.6
            };
            header.SetResourceReference(WpfTextBlock.ForegroundProperty, "DockMenuForegroundBrush");
            stack.Children.Add(header);

            foreach (var modelId in provider.AvailableModels)
            {
                var isSelected = provider.ProviderId == currentProviderId && modelId == currentModelId;
                var row = new Border
                {
                    Padding = new Thickness(8, 5, 8, 5),
                    CornerRadius = new CornerRadius(3),
                    Cursor = Cursors.Hand,
                    Tag = (provider.ProviderId, modelId)
                };

                if (isSelected)
                    row.SetResourceReference(Border.BackgroundProperty, "CA_AccentBrandingBrush");

                row.MouseEnter += (s, _) => { if (!isSelected) ((Border)s!).SetResourceReference(Border.BackgroundProperty, "DockTabHoverBrush"); };
                row.MouseLeave += (s, _) => { if (!isSelected) ((Border)s!).Background = Brushes.Transparent; };
                row.MouseLeftButtonDown += (s, _) =>
                {
                    var (pid, mid) = ((string, string))((Border)s!).Tag;
                    SelectedProviderId = pid;
                    SelectedModelId = mid;
                    IsOpen = false;
                    SelectionCommitted?.Invoke(this, EventArgs.Empty);
                };

                var rowStack = new StackPanel { Orientation = Orientation.Horizontal };

                var bullet = new WpfTextBlock
                {
                    Text = isSelected ? "\u25CF" : "\u25CB",
                    FontSize = 10,
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                bullet.SetResourceReference(WpfTextBlock.ForegroundProperty, isSelected ? "White" : "DockMenuForegroundBrush");

                var label = new WpfTextBlock
                {
                    Text = modelId,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                };
                label.SetResourceReference(WpfTextBlock.ForegroundProperty, isSelected ? "White" : "DockMenuForegroundBrush");

                var ctx = new WpfTextBlock
                {
                    Text = provider.MaxContextTokens >= 1_000_000 ? "1M" : $"{provider.MaxContextTokens / 1000}K",
                    FontSize = 10,
                    Opacity = 0.5,
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                ctx.SetResourceReference(WpfTextBlock.ForegroundProperty, "DockMenuForegroundBrush");

                rowStack.Children.Add(bullet);
                rowStack.Children.Add(label);
                rowStack.Children.Add(ctx);
                row.Child = rowStack;
                stack.Children.Add(row);
            }
        }

        // Thinking toggle
        var separator = new Border { Height = 1, Margin = new Thickness(0, 8, 0, 8) };
        separator.SetResourceReference(Border.BackgroundProperty, "DockBorderBrush");
        stack.Children.Add(separator);

        var thinkingCheck = new CheckBox
        {
            Content = "Enable Thinking (Anthropic only)",
            IsChecked = thinkingEnabled,
            FontSize = 11.5,
            Margin = new Thickness(4, 0, 0, 4)
        };
        thinkingCheck.SetResourceReference(CheckBox.ForegroundProperty, "DockMenuForegroundBrush");
        thinkingCheck.Checked += (_, _) => ThinkingEnabled = true;
        thinkingCheck.Unchecked += (_, _) => ThinkingEnabled = false;
        stack.Children.Add(thinkingCheck);

        scroll.Content = stack;
        rootBorder.Child = scroll;
        Child = rootBorder;
    }
}

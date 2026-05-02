// ==========================================================
// Project: WpfHexEditor.App
// File: Dialogs/SolutionPropertyPages/SourceFilesPage.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     "Fichiers sources pour le débogage" property page.
//     Two editable path lists (source directories + exclusions) with a toolbar
//     matching the VS layout: Valider | Browse | Supprimer | Déplacer vers le bas | Vers le haut.
// ==========================================================

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using WpfHexEditor.App.Properties;

namespace WpfHexEditor.App.Dialogs.SolutionPropertyPages;

/// <summary>
/// Property page: "Fichiers sources pour le débogage".
/// In-memory path lists (Phase 1 — not yet persisted to solution file).
/// </summary>
internal sealed class SourceFilesPage : UserControl
{
    // ── Data ─────────────────────────────────────────────────────────────────

    private readonly ObservableCollection<string> _sourcePaths   = [];
    private readonly ObservableCollection<string> _excludedPaths = [];

    // ── UI refs ──────────────────────────────────────────────────────────────

    private ListBox _lbSource   = null!;
    private ListBox _lbExcluded = null!;

    // ── Constructor ──────────────────────────────────────────────────────────

    internal SourceFilesPage()
    {
        BuildUI();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    internal void Apply()
    {
        // Phase 1: stored in-memory only.
        // Phase 2: persist to solution's DebugSettings section.
    }

    // ── UI Construction ──────────────────────────────────────────────────────

    private void BuildUI()
    {
        SetResourceReference(BackgroundProperty, "DockWindowBackgroundBrush");

        // Build ListBoxes first so toolbar lambdas can reference the fields safely.
        _lbSource = new ListBox { ItemsSource = _sourcePaths, Margin = new Thickness(0, 2, 0, 8) };
        _lbSource.SetResourceReference(ListBox.BackgroundProperty,  "DockWindowBackgroundBrush");
        _lbSource.SetResourceReference(ListBox.ForegroundProperty,  "DockMenuForegroundBrush");
        _lbSource.SetResourceReference(ListBox.BorderBrushProperty, "DockBorderBrush");

        _lbExcluded = new ListBox { ItemsSource = _excludedPaths };
        _lbExcluded.SetResourceReference(ListBox.BackgroundProperty,  "DockWindowBackgroundBrush");
        _lbExcluded.SetResourceReference(ListBox.ForegroundProperty,  "DockMenuForegroundBrush");
        _lbExcluded.SetResourceReference(ListBox.BorderBrushProperty, "DockBorderBrush");

        var root = new Grid { Margin = new Thickness(8) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // source label
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // source toolbar
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });  // source list
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // exclusion label
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.5, GridUnitType.Star) }); // exclusion list

        var sourceLabel = MakeLabel("Répertoires contenant du code source :", new Thickness(0, 0, 0, 4));
        Grid.SetRow(sourceLabel, 0);

        var sourceToolbar = BuildPathToolbar(source: true);
        Grid.SetRow(sourceToolbar, 1);

        Grid.SetRow(_lbSource, 2);

        var excludeLabel = MakeLabel("Ne pas rechercher ces fichiers sources :", new Thickness(0, 0, 0, 4));
        Grid.SetRow(excludeLabel, 3);

        Grid.SetRow(_lbExcluded, 4);

        root.Children.Add(sourceLabel);
        root.Children.Add(sourceToolbar);
        root.Children.Add(_lbSource);
        root.Children.Add(excludeLabel);
        root.Children.Add(_lbExcluded);

        Content = root;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private StackPanel BuildPathToolbar(bool source)
    {
        var collection = source ? _sourcePaths : _excludedPaths;
        ListBox Lb() => source ? _lbSource : _lbExcluded;

        var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };

        bar.Children.Add(MakeToolbarButton("\uE73E", "Valider", () => { /* Phase 2 */ }));

        bar.Children.Add(MakeToolbarButton("\uED25", "Parcourir…", () =>
        {
            var dlg = new OpenFolderDialog { Title = AppResources.App_SourceFiles_SelectFolder };
            if (dlg.ShowDialog() == true && !collection.Contains(dlg.FolderName))
                collection.Add(dlg.FolderName);
        }));

        bar.Children.Add(MakeToolbarButton("\uE74D", "Supprimer", () =>
        {
            if (Lb().SelectedItem is string sel) collection.Remove(sel);
        }));

        bar.Children.Add(MakeToolbarButton("\uE74B", "Déplacer vers le bas", () =>
        {
            if (Lb().SelectedItem is string sel)
            {
                int idx = collection.IndexOf(sel);
                if (idx >= 0 && idx < collection.Count - 1)
                {
                    collection.RemoveAt(idx);
                    collection.Insert(idx + 1, sel);
                    Lb().SelectedIndex = idx + 1;
                }
            }
        }));

        bar.Children.Add(MakeToolbarButton("\uE74A", "Déplacer vers le haut", () =>
        {
            if (Lb().SelectedItem is string sel)
            {
                int idx = collection.IndexOf(sel);
                if (idx > 0)
                {
                    collection.RemoveAt(idx);
                    collection.Insert(idx - 1, sel);
                    Lb().SelectedIndex = idx - 1;
                }
            }
        }));

        return bar;
    }

    private static TextBlock MakeLabel(string text, Thickness margin)
    {
        var tb = new TextBlock { Text = text, Margin = margin };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");
        return tb;
    }

    private static Button MakeToolbarButton(string icon, string tooltip, System.Action action)
    {
        var tb = new TextBlock
        {
            Text       = icon,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize   = 12
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");

        var btn = new Button
        {
            Content = tb,
            ToolTip = tooltip,
            Padding = new Thickness(4, 2, 4, 2),
            Margin  = new Thickness(0, 0, 2, 0)
        };
        btn.SetResourceReference(Button.BackgroundProperty, "DockWindowBackgroundBrush");
        btn.Click += (_, _) => action();
        return btn;
    }
}

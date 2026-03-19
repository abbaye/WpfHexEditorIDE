// ==========================================================
// Project: WpfHexEditor.Shell
// File: TabConfigButton.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     VS2026-style gear/settings button placed at the right end of the document
//     tab bar. Shows a dropdown context menu for toggling tab placement,
//     colorization, and multi-row behavior. Mutates the shared
//     DocumentTabBarSettings instance directly — no extra event plumbing required.
//
// Architecture Notes:
//     Inherits Button. Opens a ContextMenu on click targeting the Settings DP.
//     Direct mutation pattern: changes propagate via INotifyPropertyChanged on
//     DocumentTabBarSettings, picked up by bindings in DocumentTabHost.
//
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using WpfHexEditor.Docking.Core;

namespace WpfHexEditor.Shell.Controls;

/// <summary>
/// VS2026-style settings gear button rendered at the right end of the document tab bar.
/// Shows a dropdown menu that lets the user toggle tab placement, colorization, and
/// multi-row behaviour. Mutates the shared <see cref="DocumentTabBarSettings"/> instance
/// directly so no extra event plumbing is needed.
/// </summary>
public class TabConfigButton : Button
{
    // --- Settings DP ---------------------------------------------------------

    public static readonly DependencyProperty SettingsProperty =
        DependencyProperty.Register(
            nameof(Settings),
            typeof(DocumentTabBarSettings),
            typeof(TabConfigButton),
            new PropertyMetadata(null));

    /// <summary>
    /// The shared settings object this button reads from and writes to.
    /// </summary>
    public DocumentTabBarSettings? Settings
    {
        get => (DocumentTabBarSettings?)GetValue(SettingsProperty);
        set => SetValue(SettingsProperty, value);
    }

    // --- Events --------------------------------------------------------------

    /// <summary>
    /// Raised when the user clicks "Options…" in the dropdown.
    /// The host application can subscribe to open a settings dialog.
    /// </summary>
    public event EventHandler? OptionsRequested;

    // --- Constructor ---------------------------------------------------------

    public TabConfigButton()
    {
        // Segoe MDL2 Assets gear icon
        Content = "\uE713";
        FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets");
        FontSize = 12;
        Padding = new Thickness(4, 2, 4, 2);
        Cursor = System.Windows.Input.Cursors.Hand;
        VerticalAlignment = VerticalAlignment.Center;
        ToolTip = "Configure document tab bar";
        SetResourceReference(StyleProperty, "DockTitleButtonStyle");
    }

    // --- Click handler --------------------------------------------------------

    protected override void OnClick()
    {
        base.OnClick();
        ShowConfigMenu();
    }

    private void ShowConfigMenu()
    {
        var s = Settings;
        var menu = new ContextMenu { PlacementTarget = this, Placement = PlacementMode.Bottom };

        // -- Placement group --------------------------------------------------
        menu.Items.Add(MakePlacementItem("Place tabs on left",  DocumentTabPlacement.Left,  s));
        menu.Items.Add(MakePlacementItem("Place tabs on top",   DocumentTabPlacement.Top,   s));
        menu.Items.Add(MakePlacementItem("Place tabs on right", DocumentTabPlacement.Right, s));

        menu.Items.Add(new Separator());

        // -- Color by submenu -------------------------------------------------
        var colorParent = new MenuItem { Header = "Color document tabs by" };
        colorParent.Items.Add(MakeColorModeItem("Project",        DocumentTabColorMode.Project,       s));
        colorParent.Items.Add(MakeColorModeItem("File extension", DocumentTabColorMode.FileExtension, s));
        colorParent.Items.Add(MakeColorModeItem("Regex",          DocumentTabColorMode.Regex,         s));
        colorParent.Items.Add(new Separator());
        colorParent.Items.Add(MakeColorModeItem("No colorization", DocumentTabColorMode.None,         s));
        menu.Items.Add(colorParent);

        menu.Items.Add(new Separator());

        // -- Multi-row tabs ---------------------------------------------------
        var multiRow = new MenuItem
        {
            Header = "Show tabs in multiple rows",
            IsCheckable = true,
            IsChecked = s?.MultiRowTabs ?? false
        };
        multiRow.Click += (_, _) =>
        {
            if (Settings is not null)
                Settings.MultiRowTabs = !Settings.MultiRowTabs;
        };
        menu.Items.Add(multiRow);

        // -- Multi-row with mouse wheel ----------------------------------------
        var wheelItem = new MenuItem
        {
            Header = "Enable/disable multiple rows with the mouse wheel",
            IsCheckable = true,
            IsChecked = s?.MultiRowWithMouseWheel ?? true,
            IsEnabled = s?.MultiRowTabs ?? false
        };
        wheelItem.Click += (_, _) =>
        {
            if (Settings is not null)
                Settings.MultiRowWithMouseWheel = !Settings.MultiRowWithMouseWheel;
        };
        menu.Items.Add(wheelItem);

        menu.Items.Add(new Separator());

        // -- Options… ---------------------------------------------------------
        var options = new MenuItem { Header = "Options…" };
        options.Click += (_, _) => OptionsRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(options);

        menu.IsOpen = true;
    }

    // --- Helpers -------------------------------------------------------------

    private MenuItem MakePlacementItem(string header, DocumentTabPlacement placement,
                                        DocumentTabBarSettings? s)
    {
        var item = new MenuItem
        {
            Header = header,
            IsCheckable = true,
            IsChecked = s?.TabPlacement == placement
        };
        item.Click += (_, _) =>
        {
            if (Settings is not null)
                Settings.TabPlacement = placement;
        };
        return item;
    }

    private MenuItem MakeColorModeItem(string header, DocumentTabColorMode mode,
                                        DocumentTabBarSettings? s)
    {
        var item = new MenuItem
        {
            Header = header,
            IsCheckable = true,
            IsChecked = s?.ColorMode == mode
        };
        item.Click += (_, _) =>
        {
            if (Settings is not null)
                Settings.ColorMode = mode;
        };
        return item;
    }
}

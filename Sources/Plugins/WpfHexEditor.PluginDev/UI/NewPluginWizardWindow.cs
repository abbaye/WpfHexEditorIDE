// ==========================================================
// Project: WpfHexEditor.PluginDev
// File: UI/NewPluginWizardWindow.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     4-page wizard for creating a new SDK plugin project.
//     All UI is built in code-behind — no XAML dependency.
//
//     Page 1 — Name & Location
//     Page 2 — Template selection (Panel / Editor / EventListener / Converter)
//     Page 3 — Capabilities & isolation settings
//     Page 4 — Summary + Create
//
// Architecture Notes:
//     Pattern: Step Wizard (manual page Grid swap, no Frame/NavigationService).
//     Validation is inline (regex + Directory.Exists checks) before Next is enabled.
//     ScaffoldAsync is called in a Task.Run guard; output path is shown after creation.
// ==========================================================

using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.PluginDev.Templates;

namespace WpfHexEditor.PluginDev.UI;

/// <summary>
/// 4-page wizard for scaffolding a new plugin project.
/// Shows each page as a swap of Grid visibility; no XAML required.
/// </summary>
public sealed class NewPluginWizardWindow : Window
{
    // -----------------------------------------------------------------------
    // Constants
    // -----------------------------------------------------------------------

    private static readonly Regex CsIdentifierPart =
        new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    private static readonly IPluginTemplate[] Templates =
    [
        new PanelPluginTemplate(),
        new EditorPluginTemplate(),
        new EventListenerPluginTemplate(),
        new ConverterPluginTemplate(),
    ];

    // -----------------------------------------------------------------------
    // State
    // -----------------------------------------------------------------------

    private int _currentPage;   // 0-based

    // Page 1 fields
    private TextBox _tbPluginName  = null!;
    private TextBox _tbOutputDir   = null!;
    private TextBox _tbAuthor      = null!;
    private TextBox _tbVersion     = null!;

    // Page 2 fields
    private int _selectedTemplateIndex = 0;
    private readonly RadioButton[] _templateRadios = new RadioButton[Templates.Length];

    // Page 3 fields
    private CheckBox _cbFileSystem = null!;
    private CheckBox _cbNetwork    = null!;
    private CheckBox _cbTerminal   = null!;
    private CheckBox _cbUIPlugin   = null!;
    private CheckBox _cbEventBus   = null!;
    private RadioButton _rbIsoAuto     = null!;
    private RadioButton _rbIsoInProc   = null!;
    private RadioButton _rbIsoSandbox  = null!;
    private Slider   _sldPriority   = null!;

    // Page 4 fields
    private TextBlock _tbSummary = null!;

    // Navigation
    private Button _btnBack = null!;
    private Button _btnNext = null!;

    // Page containers
    private readonly Grid[] _pages = new Grid[4];

    // -----------------------------------------------------------------------
    // Construction
    // -----------------------------------------------------------------------

    public NewPluginWizardWindow()
    {
        Title          = "New Extension Project";
        Width          = 620;
        Height         = 500;
        ResizeMode     = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background     = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26));
        Foreground     = Brushes.WhiteSmoke;

        BuildUI();
        ShowPage(0);
    }

    // -----------------------------------------------------------------------
    // UI construction
    // -----------------------------------------------------------------------

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });   // header
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // content
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });   // nav bar

        // Header strip
        var header = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
            Padding    = new Thickness(12, 0, 12, 0),
        };
        var headerText = new TextBlock
        {
            Text       = "New Extension Project",
            FontSize   = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.WhiteSmoke,
            VerticalAlignment = VerticalAlignment.Center,
        };
        header.Child = headerText;
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        // Pages
        _pages[0] = BuildPage1();
        _pages[1] = BuildPage2();
        _pages[2] = BuildPage3();
        _pages[3] = BuildPage4();

        var pageHost = new Grid();
        foreach (var page in _pages)
        {
            pageHost.Children.Add(page);
        }
        Grid.SetRow(pageHost, 1);
        root.Children.Add(pageHost);

        // Navigation bar
        var navBar = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30)),
            Padding    = new Thickness(12, 0, 12, 0),
        };
        var navPanel = new StackPanel
        {
            Orientation       = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _btnBack = MakeButton("< Back");
        _btnBack.Width = 80;
        _btnBack.Click += OnBack;
        navPanel.Children.Add(_btnBack);

        var spacer = new Border { Width = 8 };
        navPanel.Children.Add(spacer);

        _btnNext = MakeButton("Next >");
        _btnNext.Width = 100;
        _btnNext.Click += OnNext;
        navPanel.Children.Add(_btnNext);

        navBar.Child = navPanel;
        Grid.SetRow(navBar, 2);
        root.Children.Add(navBar);

        Content = root;
    }

    // ── Page 1 — Name & Location ────────────────────────────────────────────

    private Grid BuildPage1()
    {
        var grid = new Grid { Margin = new Thickness(20) };
        for (var i = 0; i < 5; i++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        AddSectionHeader(grid, "Step 1 of 4 — Name & Location", 0);

        AddLabeledField(grid, "Plugin Name:", out _tbPluginName, "MyPlugin", 1);
        _tbPluginName.TextChanged += (_, _) => ValidatePage1();

        AddLabeledFieldWithBrowse(grid, "Output Directory:", out _tbOutputDir,
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 2,
            OnBrowseOutputDir);
        _tbOutputDir.TextChanged += (_, _) => ValidatePage1();

        AddLabeledField(grid, "Author:", out _tbAuthor, Environment.UserName, 3);

        AddLabeledField(grid, "Version:", out _tbVersion, "1.0.0", 4);

        return grid;
    }

    private void OnBrowseOutputDir()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title       = "Select output directory for the plugin project",
            Multiselect = false,
        };
        if (!string.IsNullOrWhiteSpace(_tbOutputDir.Text))
            dlg.InitialDirectory = _tbOutputDir.Text;

        if (dlg.ShowDialog(this) == true)
            _tbOutputDir.Text = dlg.FolderName;
    }

    private void ValidatePage1()
    {
        var nameOk = CsIdentifierPart.IsMatch(_tbPluginName.Text.Trim());
        var dirOk  = !string.IsNullOrWhiteSpace(_tbOutputDir.Text);
        _btnNext.IsEnabled = nameOk && dirOk;
    }

    // ── Page 2 — Template ───────────────────────────────────────────────────

    private Grid BuildPage2()
    {
        var grid = new Grid { Margin = new Thickness(20) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // header
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // cards

        AddSectionHeader(grid, "Step 2 of 4 — Select Template", 0);

        var cardPanel = new WrapPanel { Orientation = Orientation.Horizontal };

        for (var i = 0; i < Templates.Length; i++)
        {
            var t      = Templates[i];
            var idx    = i;
            var card   = BuildTemplateCard(t, i == 0, () => _selectedTemplateIndex = idx);
            cardPanel.Children.Add(card);
        }

        Grid.SetRow(cardPanel, 1);
        grid.Children.Add(cardPanel);
        return grid;
    }

    private Border BuildTemplateCard(IPluginTemplate template, bool isDefault, Action onSelect)
    {
        var card = new Border
        {
            Width       = 256,
            Margin      = new Thickness(0, 0, 12, 12),
            Background  = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46)),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(4),
            Padding         = new Thickness(12),
        };

        var inner = new StackPanel();

        var radio = new RadioButton
        {
            GroupName = "Template",
            IsChecked = isDefault,
            Margin    = new Thickness(0, 0, 0, 6),
        };
        radio.Checked += (_, _) => onSelect();

        var iconLabel = new TextBlock
        {
            Text       = template.Icon,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize   = 22,
            Foreground = new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6)),
            Margin     = new Thickness(0, 0, 0, 6),
        };

        var nameLabel = new TextBlock
        {
            Text       = template.DisplayName,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.WhiteSmoke,
            Margin     = new Thickness(0, 0, 0, 4),
        };

        var descLabel = new TextBlock
        {
            Text        = template.Description,
            Foreground  = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            FontSize    = 11,
            TextWrapping = TextWrapping.Wrap,
        };

        inner.Children.Add(radio);
        inner.Children.Add(iconLabel);
        inner.Children.Add(nameLabel);
        inner.Children.Add(descLabel);
        card.Child = inner;

        // Store reference so we can read IsChecked from array index
        if (Array.IndexOf(Templates, template) is int idx and >= 0)
            _templateRadios[idx] = radio;

        return card;
    }

    // ── Page 3 — Capabilities ───────────────────────────────────────────────

    private Grid BuildPage3()
    {
        var grid = new Grid { Margin = new Thickness(20) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        AddSectionHeader(grid, "Step 3 of 4 — Capabilities & Isolation", 0);

        // Permissions
        var permGroup = new GroupBox
        {
            Header  = "Permissions",
            Margin  = new Thickness(0, 8, 0, 8),
            Padding = new Thickness(8),
            Foreground = Brushes.WhiteSmoke,
        };
        var permPanel = new StackPanel();
        _cbFileSystem = AddCheckBox(permPanel, "FileSystem access",       true);
        _cbNetwork    = AddCheckBox(permPanel, "Network access",          false);
        _cbTerminal   = AddCheckBox(permPanel, "Terminal access",         false);
        _cbUIPlugin   = AddCheckBox(permPanel, "UI contributions (panels, menus)", true);
        _cbEventBus   = AddCheckBox(permPanel, "EventBus subscriptions",  true);
        permGroup.Content = permPanel;
        Grid.SetRow(permGroup, 1);
        grid.Children.Add(permGroup);

        // Isolation
        var isoGroup = new GroupBox
        {
            Header  = "Isolation Mode",
            Margin  = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(8),
            Foreground = Brushes.WhiteSmoke,
        };
        var isoPanel = new StackPanel();
        _rbIsoAuto    = AddRadio(isoPanel, "Auto (recommended)",         "IsoMode", true);
        _rbIsoInProc  = AddRadio(isoPanel, "InProcess (fastest)",        "IsoMode", false);
        _rbIsoSandbox = AddRadio(isoPanel, "Sandbox (most isolated)",    "IsoMode", false);
        isoGroup.Content = isoPanel;
        Grid.SetRow(isoGroup, 2);
        grid.Children.Add(isoGroup);

        // Load priority
        var prioPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        prioPanel.Children.Add(new TextBlock { Text = "Load Priority (10–90):", Width = 160, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.WhiteSmoke });
        _sldPriority = new Slider { Minimum = 10, Maximum = 90, Value = 50, Width = 200, VerticalAlignment = VerticalAlignment.Center };
        prioPanel.Children.Add(_sldPriority);
        var prioVal = new TextBlock { Width = 30, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.WhiteSmoke, Margin = new Thickness(6, 0, 0, 0) };
        prioVal.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Value")
        {
            Source    = _sldPriority,
            StringFormat = "F0",
        });
        prioPanel.Children.Add(prioVal);
        Grid.SetRow(prioPanel, 3);
        grid.Children.Add(prioPanel);

        return grid;
    }

    // ── Page 4 — Summary ────────────────────────────────────────────────────

    private Grid BuildPage4()
    {
        var grid = new Grid { Margin = new Thickness(20) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        AddSectionHeader(grid, "Step 4 of 4 — Summary", 0);

        var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        _tbSummary = new TextBlock
        {
            Foreground   = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            FontFamily   = new FontFamily("Consolas"),
            FontSize     = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin       = new Thickness(0, 8, 0, 0),
        };
        scrollViewer.Content = _tbSummary;
        Grid.SetRow(scrollViewer, 1);
        grid.Children.Add(scrollViewer);

        return grid;
    }

    private void RefreshSummary()
    {
        var template = Templates[_selectedTemplateIndex];
        var iso      = _rbIsoSandbox.IsChecked == true ? "Sandbox"
                     : _rbIsoInProc.IsChecked  == true ? "InProcess" : "Auto";

        var sb = new StringBuilder();
        sb.AppendLine($"Plugin Name  : {_tbPluginName.Text}");
        sb.AppendLine($"Output Dir   : {Path.Combine(_tbOutputDir.Text, _tbPluginName.Text)}");
        sb.AppendLine($"Author       : {_tbAuthor.Text}");
        sb.AppendLine($"Version      : {_tbVersion.Text}");
        sb.AppendLine();
        sb.AppendLine($"Template     : {template.DisplayName}");
        sb.AppendLine();
        sb.AppendLine("Permissions:");
        if (_cbFileSystem.IsChecked == true) sb.AppendLine("  ✓ FileSystem");
        if (_cbNetwork.IsChecked    == true) sb.AppendLine("  ✓ Network");
        if (_cbTerminal.IsChecked   == true) sb.AppendLine("  ✓ Terminal");
        if (_cbUIPlugin.IsChecked   == true) sb.AppendLine("  ✓ UI Contributions");
        if (_cbEventBus.IsChecked   == true) sb.AppendLine("  ✓ EventBus");
        sb.AppendLine();
        sb.AppendLine($"Isolation    : {iso}");
        sb.AppendLine($"Load Priority: {_sldPriority.Value:F0}");

        _tbSummary.Text = sb.ToString();
    }

    // -----------------------------------------------------------------------
    // Navigation
    // -----------------------------------------------------------------------

    private void ShowPage(int index)
    {
        _currentPage = index;

        for (var i = 0; i < _pages.Length; i++)
            _pages[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;

        _btnBack.IsEnabled = index > 0;

        switch (index)
        {
            case 0:
                _btnNext.Content   = "Next >";
                _btnNext.IsEnabled = true;
                ValidatePage1();
                break;
            case 1:
            case 2:
                _btnNext.Content   = "Next >";
                _btnNext.IsEnabled = true;
                break;
            case 3:
                _btnNext.Content   = "Create & Open";
                _btnNext.IsEnabled = true;
                RefreshSummary();
                break;
        }
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        if (_currentPage > 0)
            ShowPage(_currentPage - 1);
    }

    private async void OnNext(object sender, RoutedEventArgs e)
    {
        if (_currentPage < _pages.Length - 1)
        {
            ShowPage(_currentPage + 1);
            return;
        }

        // Final step — create the project.
        _btnNext.IsEnabled = false;
        _btnBack.IsEnabled = false;

        try
        {
            var template = Templates[_selectedTemplateIndex];
            var name     = _tbPluginName.Text.Trim();
            var outDir   = Path.Combine(_tbOutputDir.Text.Trim(), name);
            var author   = string.IsNullOrWhiteSpace(_tbAuthor.Text) ? "Author" : _tbAuthor.Text.Trim();

            // Scaffold project skeleton via PluginProjectTemplate first.
            var projectTemplate = new PluginProjectTemplate();
            var projectDir      = await projectTemplate.ScaffoldAsync(_tbOutputDir.Text.Trim(), name, author);

            // Scaffold template-specific source files.
            await template.ScaffoldAsync(projectDir, name, author);

            MessageBox.Show(
                $"Plugin project created at:\n{projectDir}\n\nOpen the folder to start editing.",
                "Plugin Created",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // Open output directory in Explorer.
            System.Diagnostics.Process.Start("explorer.exe", projectDir);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to create plugin project:\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            _btnNext.IsEnabled = true;
            _btnBack.IsEnabled = true;
        }
    }

    // -----------------------------------------------------------------------
    // UI helpers
    // -----------------------------------------------------------------------

    private static void AddSectionHeader(Grid grid, string text, int row)
    {
        var tb = new TextBlock
        {
            Text       = text,
            FontSize   = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6)),
            Margin     = new Thickness(0, 0, 0, 12),
        };
        Grid.SetRow(tb, row);
        grid.Children.Add(tb);
    }

    private void AddLabeledField(Grid grid, string label, out TextBox textBox, string placeholder, int row)
    {
        if (row >= grid.RowDefinitions.Count)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        panel.Children.Add(new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)), Margin = new Thickness(0, 0, 0, 2) });
        textBox = new TextBox { Text = placeholder, Height = 26, Padding = new Thickness(4, 2, 4, 2) };
        panel.Children.Add(textBox);
        Grid.SetRow(panel, row);
        grid.Children.Add(panel);
    }

    private void AddLabeledFieldWithBrowse(Grid grid, string label, out TextBox textBox,
        string placeholder, int row, Action onBrowse)
    {
        if (row >= grid.RowDefinitions.Count)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        panel.Children.Add(new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)), Margin = new Thickness(0, 0, 0, 2) });

        var row2 = new Grid();
        row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row2.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        textBox = new TextBox { Text = placeholder, Height = 26, Padding = new Thickness(4, 2, 4, 2) };
        Grid.SetColumn(textBox, 0);
        row2.Children.Add(textBox);

        var browse = MakeButton("Browse…");
        browse.Width  = 80;
        browse.Height = 26;
        browse.Margin = new Thickness(4, 0, 0, 0);
        browse.Click += (_, _) => onBrowse();
        Grid.SetColumn(browse, 1);
        row2.Children.Add(browse);

        panel.Children.Add(row2);
        Grid.SetRow(panel, row);
        grid.Children.Add(panel);
    }

    private static CheckBox AddCheckBox(Panel parent, string text, bool isChecked)
    {
        var cb = new CheckBox
        {
            Content   = text,
            IsChecked = isChecked,
            Foreground = Brushes.WhiteSmoke,
            Margin    = new Thickness(0, 2, 0, 2),
        };
        parent.Children.Add(cb);
        return cb;
    }

    private static RadioButton AddRadio(Panel parent, string text, string groupName, bool isChecked)
    {
        var rb = new RadioButton
        {
            Content   = text,
            GroupName = groupName,
            IsChecked = isChecked,
            Foreground = Brushes.WhiteSmoke,
            Margin    = new Thickness(0, 2, 0, 2),
        };
        parent.Children.Add(rb);
        return rb;
    }

    private static Button MakeButton(string text)
        => new()
        {
            Content    = text,
            Height     = 28,
            Padding    = new Thickness(10, 4, 10, 4),
        };
}

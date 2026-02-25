using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Docking.Controls;
using WpfHexEditor.Docking.Model;
using WpfHexEditor.App.Panels;

namespace WpfHexEditor.App;

public partial class MainWindow : Window
{
    private int _documentCounter;
    private string _currentTheme = "Dark";

    // Keep references to anchorables for View menu toggle
    private LayoutAnchorable? _explorerAnchorable;
    private LayoutAnchorable? _propertiesAnchorable;
    private LayoutAnchorable? _outputAnchorable;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => BuildInitialLayout();
    }

    // ==================== LAYOUT CONSTRUCTION ====================

    private void BuildInitialLayout()
    {
        var layout = new LayoutRoot();

        // Root panel: Horizontal split [Left | Center+Bottom | Right]
        var rootPanel = new LayoutPanel(LayoutOrientation.Horizontal);

        // -- Left pane: Explorer --
        var leftPane = new LayoutAnchorablePane
        {
            DockSide = DockSide.Left,
            DockWidth = new System.Windows.GridLength(220)
        };
        _explorerAnchorable = new LayoutAnchorable
        {
            Title = "Explorer",
            ContentId = "Explorer",
            Content = new ExplorerPanel()
        };
        leftPane.Children.Add(_explorerAnchorable);

        // -- Center vertical split: Documents on top, Output at bottom --
        var centerPanel = new LayoutPanel(LayoutOrientation.Vertical);

        // Document pane
        var docPane = new LayoutDocumentPane();
        var firstDoc = CreateDocumentPanel();
        docPane.Children.Add(firstDoc);

        // Bottom pane: Output
        var bottomPane = new LayoutAnchorablePane
        {
            DockSide = DockSide.Bottom,
            DockHeight = new System.Windows.GridLength(160)
        };
        _outputAnchorable = new LayoutAnchorable
        {
            Title = "Output",
            ContentId = "Output",
            Content = new OutputPanel()
        };
        bottomPane.Children.Add(_outputAnchorable);

        centerPanel.Children.Add(docPane);
        centerPanel.Children.Add(bottomPane);

        // -- Right pane: Properties --
        var rightPane = new LayoutAnchorablePane
        {
            DockSide = DockSide.Right,
            DockWidth = new System.Windows.GridLength(220)
        };
        _propertiesAnchorable = new LayoutAnchorable
        {
            Title = "Properties",
            ContentId = "Properties",
            Content = new PropertiesPanel()
        };
        rightPane.Children.Add(_propertiesAnchorable);

        // Assemble root
        rootPanel.Children.Add(leftPane);
        rootPanel.Children.Add(centerPanel);
        rootPanel.Children.Add(rightPane);

        layout.RootPanel = rootPanel;
        DockManager.Layout = layout;

        StatusText.Text = "Layout initialized";
    }

    private LayoutDocument CreateDocumentPanel()
    {
        _documentCounter++;
        var title = $"Document {_documentCounter}";
        return new LayoutDocument
        {
            Title = title,
            ContentId = $"Doc{_documentCounter}",
            Content = new DocumentPanel(title)
        };
    }

    // ==================== FILE MENU ====================

    private void OnNewDocument(object sender, RoutedEventArgs e)
    {
        var docPane = DockManager.FindDocumentPane();
        if (docPane == null)
        {
            StatusText.Text = "Error: No document pane found";
            return;
        }

        var doc = CreateDocumentPanel();
        docPane.Children.Add(doc);
        docPane.SelectedContent = doc;
        StatusText.Text = $"Created: {doc.Title}";
    }

    private void OnNewDocument(object sender, ExecutedRoutedEventArgs e)
    {
        OnNewDocument(sender, (RoutedEventArgs)e);
    }

    private void OnExit(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // ==================== VIEW MENU (TOGGLE PANELS) ====================

    private void OnToggleExplorer(object sender, RoutedEventArgs e)
    {
        ToggleAnchorable(_explorerAnchorable, MenuViewExplorer.IsChecked, "Explorer");
    }

    private void OnToggleProperties(object sender, RoutedEventArgs e)
    {
        ToggleAnchorable(_propertiesAnchorable, MenuViewProperties.IsChecked, "Properties");
    }

    private void OnToggleOutput(object sender, RoutedEventArgs e)
    {
        ToggleAnchorable(_outputAnchorable, MenuViewOutput.IsChecked, "Output");
    }

    private void ToggleAnchorable(LayoutAnchorable? anchorable, bool show, string name)
    {
        if (anchorable == null) return;

        if (show)
        {
            anchorable.Show();
            StatusText.Text = $"{name} panel shown";
        }
        else
        {
            anchorable.Hide();
            StatusText.Text = $"{name} panel hidden";
        }
    }

    // ==================== WINDOW MENU ====================

    private void OnResetLayout(object sender, RoutedEventArgs e)
    {
        _documentCounter = 0;
        BuildInitialLayout();

        // Reset View menu checkboxes
        MenuViewExplorer.IsChecked = true;
        MenuViewProperties.IsChecked = true;
        MenuViewOutput.IsChecked = true;

        StatusText.Text = "Layout reset";
    }

    // ==================== THEME ====================

    private void OnThemeSelected(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string themeName)
        {
            ApplyTheme(themeName);
            SyncThemeUI(themeName);
        }
    }

    private void OnThemeComboChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeCombo.SelectedItem is ComboBoxItem item && item.Tag is string themeName)
        {
            ApplyTheme(themeName);
            SyncThemeUI(themeName);
        }
    }

    private void ApplyTheme(string themeName)
    {
        if (themeName == _currentTheme) return;

        var uri = new Uri(
            $"pack://application:,,,/WpfHexEditor.App;component/Resources/Themes/{themeName}.xaml",
            UriKind.Absolute);

        var newTheme = new ResourceDictionary { Source = uri };

        // Replace the first merged dictionary (the theme one)
        var mergedDicts = Application.Current.Resources.MergedDictionaries;
        if (mergedDicts.Count > 0)
            mergedDicts[0] = newTheme;
        else
            mergedDicts.Add(newTheme);

        _currentTheme = themeName;
        ThemeStatusText.Text = $"Theme: {themeName}";
        StatusText.Text = $"Theme changed to {themeName}";
    }

    private void SyncThemeUI(string themeName)
    {
        // Sync menu radio buttons
        MenuThemeLight.IsChecked = themeName == "Light";
        MenuThemeDark.IsChecked = themeName == "Dark";
        MenuThemeVS.IsChecked = themeName == "VisualStudio";

        // Sync combo box
        for (int i = 0; i < ThemeCombo.Items.Count; i++)
        {
            if (ThemeCombo.Items[i] is ComboBoxItem item && item.Tag is string tag && tag == themeName)
            {
                ThemeCombo.SelectionChanged -= OnThemeComboChanged;
                ThemeCombo.SelectedIndex = i;
                ThemeCombo.SelectionChanged += OnThemeComboChanged;
                break;
            }
        }
    }
}

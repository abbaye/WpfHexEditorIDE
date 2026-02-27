//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WpfHexaEditor;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;
using WpfHexEditor.Docking.Core.Serialization;
using WpfHexEditor.Docking.Wpf;

namespace WpfHexEditor.Sample.Docking;

public partial class MainWindow : Window
{
    private DockLayoutRoot _layout = null!;
    private DockEngine _engine = null!;
    private int _documentCounter;
    private bool _isLocked;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SetupDefaultLayout();
    }

    private void SetupDefaultLayout()
    {
        _layout = new DockLayoutRoot();
        _engine = new DockEngine(_layout);

        // Add a welcome document to the main host
        var welcomeDoc = new DockItem { Title = "Welcome", ContentId = "doc-welcome" };
        _layout.MainDocumentHost.AddItem(welcomeDoc);

        // Create left panel group (Solution Explorer)
        var solutionExplorer = new DockItem { Title = "Solution Explorer", ContentId = "panel-solution-explorer" };
        _engine.Dock(solutionExplorer, _layout.MainDocumentHost, DockDirection.Left);

        // Create bottom panel group (Output)
        var output = new DockItem { Title = "Output", ContentId = "panel-output" };
        _engine.Dock(output, _layout.MainDocumentHost, DockDirection.Bottom);

        // Create right panel group (Properties)
        var properties = new DockItem { Title = "Properties", ContentId = "panel-properties" };
        _engine.Dock(properties, _layout.MainDocumentHost, DockDirection.Right);

        // Set up content factory and bind to DockControl
        DockHost.ContentFactory = CreateContentForItem;
        DockHost.TabCloseRequested += OnTabCloseRequested;
        DockHost.Layout = _layout;

        UpdateStatusBar();
    }

    private object CreateContentForItem(DockItem item)
    {
        return item.ContentId switch
        {
            "panel-solution-explorer" => CreateSolutionExplorerContent(),
            "panel-properties" => CreatePropertiesContent(),
            "panel-output" => CreateOutputContent(),
            _ when item.ContentId.StartsWith("doc-hex-") => CreateHexEditorContent(),
            _ => CreateDocumentContent(item)
        };
    }

    private static UIElement CreateSolutionExplorerContent()
    {
        var treeView = new TreeView { Background = System.Windows.Media.Brushes.Transparent };
        var root = new TreeViewItem { Header = "Solution 'WpfHexEditor'" };
        root.Items.Add(new TreeViewItem { Header = "WpfHexEditor.Docking.Core" });
        root.Items.Add(new TreeViewItem { Header = "WpfHexEditor.Docking.Wpf" });
        root.Items.Add(new TreeViewItem { Header = "WpfHexEditor.Sample.Docking" });
        root.Items.Add(new TreeViewItem { Header = "WpfHexEditorCore" });
        root.IsExpanded = true;
        treeView.Items.Add(root);
        return treeView;
    }

    private static UIElement CreatePropertiesContent()
    {
        var panel = new StackPanel { Margin = new Thickness(8) };
        panel.Children.Add(new TextBlock { Text = "Properties", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });
        panel.Children.Add(new TextBlock { Text = "Name:" });
        panel.Children.Add(new TextBox { Text = "DockItem", Margin = new Thickness(0, 2, 0, 8) });
        panel.Children.Add(new TextBlock { Text = "Type:" });
        panel.Children.Add(new TextBox { Text = "Panel", Margin = new Thickness(0, 2, 0, 8) });
        panel.Children.Add(new TextBlock { Text = "CanClose:" });
        panel.Children.Add(new CheckBox { IsChecked = true, Margin = new Thickness(0, 2, 0, 8) });
        return panel;
    }

    private static UIElement CreateOutputContent()
    {
        var textBox = new TextBox
        {
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Text = $"=== Output Panel ==={Environment.NewLine}" +
                   $"[{DateTime.Now:HH:mm:ss}] Docking system initialized.{Environment.NewLine}" +
                   $"[{DateTime.Now:HH:mm:ss}] Default layout loaded.{Environment.NewLine}" +
                   $"[{DateTime.Now:HH:mm:ss}] Ready.{Environment.NewLine}",
            Background = System.Windows.Media.Brushes.Transparent,
            Foreground = System.Windows.Media.Brushes.LightGray,
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas, monospace"),
            FontSize = 12,
            BorderThickness = new Thickness(0)
        };
        return textBox;
    }

    private static UIElement CreateHexEditorContent()
    {
        var hexEditor = new HexEditor
        {
            Background = System.Windows.Media.Brushes.Transparent
        };

        // Generate some random sample data and write to a temp file
        var random = new Random();
        var data = new byte[1024];
        random.NextBytes(data);

        var tempFile = Path.Combine(Path.GetTempPath(), $"hexedit-sample-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(tempFile, data);
        hexEditor.OpenFile(tempFile);

        return hexEditor;
    }

    private static UIElement CreateDocumentContent(DockItem item)
    {
        var textBox = new TextBox
        {
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            AcceptsTab = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Text = $"This is document: {item.Title}\n\nEdit this text...",
            Background = System.Windows.Media.Brushes.Transparent,
            Foreground = System.Windows.Media.Brushes.LightGray,
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas, monospace"),
            FontSize = 13,
            BorderThickness = new Thickness(0)
        };
        return textBox;
    }

    private void OnTabCloseRequested(DockItem item)
    {
        if (_isLocked) return;

        try
        {
            _engine.Close(item);
            DockHost.RebuildVisualTree();
            UpdateStatusBar();
        }
        catch (InvalidOperationException ex)
        {
            StatusText.Text = $"Cannot close: {ex.Message}";
        }
    }

    private void OnOpenFile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            _documentCounter++;
            var item = new DockItem
            {
                Title = System.IO.Path.GetFileName(dialog.FileName),
                ContentId = $"doc-hex-{_documentCounter}"
            };
            _engine.Dock(item, _layout.MainDocumentHost, DockDirection.Center);
            DockHost.RebuildVisualTree();
            UpdateStatusBar();
            StatusText.Text = $"Opened: {dialog.FileName}";
        }
    }

    private void OnNewDocument(object sender, RoutedEventArgs e)
    {
        _documentCounter++;
        var item = new DockItem
        {
            Title = $"Document {_documentCounter}",
            ContentId = $"doc-{_documentCounter}"
        };
        _engine.Dock(item, _layout.MainDocumentHost, DockDirection.Center);
        DockHost.RebuildVisualTree();
        UpdateStatusBar();
        StatusText.Text = $"Created: {item.Title}";
    }

    private void OnNewHexDocument(object sender, RoutedEventArgs e)
    {
        _documentCounter++;
        var item = new DockItem
        {
            Title = $"Hex Editor {_documentCounter}",
            ContentId = $"doc-hex-{_documentCounter}"
        };
        _engine.Dock(item, _layout.MainDocumentHost, DockDirection.Center);
        DockHost.RebuildVisualTree();
        UpdateStatusBar();
        StatusText.Text = $"Created: {item.Title} (HexEditor)";
    }

    private void OnShowProperties(object sender, RoutedEventArgs e)
    {
        ShowOrCreatePanel("Properties", "panel-properties", DockDirection.Right);
    }

    private void OnShowSolutionExplorer(object sender, RoutedEventArgs e)
    {
        ShowOrCreatePanel("Solution Explorer", "panel-solution-explorer", DockDirection.Left);
    }

    private void OnShowOutput(object sender, RoutedEventArgs e)
    {
        ShowOrCreatePanel("Output", "panel-output", DockDirection.Bottom);
    }

    private void ShowOrCreatePanel(string title, string contentId, DockDirection direction)
    {
        var existing = _layout.FindItemByContentId(contentId);
        if (existing is not null)
        {
            // Already visible, activate it
            existing.Owner?.ActiveItem?.Equals(existing);
            StatusText.Text = $"Activated: {title}";
            return;
        }

        var item = new DockItem { Title = title, ContentId = contentId };
        _engine.Dock(item, _layout.MainDocumentHost, direction);
        DockHost.RebuildVisualTree();
        UpdateStatusBar();
        StatusText.Text = $"Opened: {title}";
    }

    private void OnSaveLayout(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON Files (*.json)|*.json",
            DefaultExt = ".json",
            FileName = "dock-layout.json"
        };

        if (dialog.ShowDialog() == true)
        {
            var json = DockLayoutSerializer.Serialize(_layout);
            File.WriteAllText(dialog.FileName, json);
            StatusText.Text = $"Layout saved: {dialog.FileName}";
        }
    }

    private void OnLoadLayout(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON Files (*.json)|*.json",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = File.ReadAllText(dialog.FileName);
                _layout = DockLayoutSerializer.Deserialize(json);
                _engine = new DockEngine(_layout);
                DockHost.ContentFactory = CreateContentForItem;
                DockHost.Layout = _layout;
                UpdateStatusBar();
                StatusText.Text = $"Layout loaded: {dialog.FileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load layout:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void OnResetLayout(object sender, RoutedEventArgs e)
    {
        SetupDefaultLayout();
        StatusText.Text = "Layout reset to default";
    }

    private void OnToggleLock(object sender, RoutedEventArgs e)
    {
        _isLocked = LockMenuItem.IsChecked;
        _layout.MainDocumentHost.LockMode = _isLocked ? DockLockMode.Full : DockLockMode.None;

        // Apply lock to all groups
        foreach (var group in _layout.GetAllGroups())
            group.LockMode = _isLocked ? DockLockMode.Full : DockLockMode.None;

        StatusText.Text = _isLocked ? "Layout LOCKED" : "Layout UNLOCKED";
        UpdateStatusBar();
    }

    private void OnDarkTheme(object sender, RoutedEventArgs e)
    {
        Application.Current.Resources.MergedDictionaries.Clear();
        Application.Current.Resources.MergedDictionaries.Add(
            new ResourceDictionary { Source = new Uri("pack://application:,,,/WpfHexEditor.Docking.Wpf;component/Themes/DarkTheme.xaml") });
        StatusText.Text = "Theme: Dark";
    }

    private void OnLightTheme(object sender, RoutedEventArgs e)
    {
        Application.Current.Resources.MergedDictionaries.Clear();
        Application.Current.Resources.MergedDictionaries.Add(
            new ResourceDictionary { Source = new Uri("pack://application:,,,/WpfHexEditor.Docking.Wpf;component/Themes/Generic.xaml") });
        StatusText.Text = "Theme: Light";
    }

    private void OnExit(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void UpdateStatusBar()
    {
        var panelCount = _layout.GetAllGroups().Sum(g => g.Items.Count);
        var lockState = _isLocked ? " [LOCKED]" : "";
        PanelCountText.Text = $"Panels: {panelCount}{lockState}";
    }
}

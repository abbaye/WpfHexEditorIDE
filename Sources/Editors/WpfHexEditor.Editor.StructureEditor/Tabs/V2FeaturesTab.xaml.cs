//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: Tabs/V2FeaturesTab.xaml.cs
// Description: Code-behind for V2 Features accordion tab.
//              Binds sub-panel ItemsControls from the root StructureEditorViewModel.
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Editor.StructureEditor.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.Tabs;

public sealed partial class V2FeaturesTab : UserControl
{
    public V2FeaturesTab() => InitializeComponent();

    private StructureEditorViewModel? VM => DataContext as StructureEditorViewModel;

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is not StructureEditorViewModel vm) return;
        AssertionsList.ItemsSource = vm.Assertions;
        ChecksumsList.ItemsSource  = vm.Checksums;
        ExportsList.ItemsSource    = vm.ExportTemplates;
        BookmarksList.ItemsSource  = vm.Navigation.Bookmarks;
        ImportsList.ItemsSource    = vm.Imports;
        BuildVersioningPanel(vm.Versioning);
        BuildForensicPanel(vm.Forensic);
        BuildInspectorPanel(vm.Inspector);
        BuildAiPanel(vm.AiHints);
    }

    // ── Add handlers ─────────────────────────────────────────────────────────

    private void OnAddAssertion(object sender, RoutedEventArgs e) => VM?.AddAssertion();
    private void OnAddChecksum(object sender, RoutedEventArgs e)  => VM?.AddChecksum();
    private void OnAddExport(object sender, RoutedEventArgs e)    => VM?.AddExportTemplate();
    private void OnAddImport(object sender, RoutedEventArgs e)    => VM?.AddImport();

    private void OnAddBookmark(object sender, RoutedEventArgs e)
    {
        if (VM is null) return;
        VM.Navigation.AddBookmarkCommand.Execute(null);
    }

    // ── Dynamic panels (Versioning, Forensic, Inspector, AiHints) ──────────────

    private void BuildVersioningPanel(VersioningViewModel vm)
    {
        VersioningPanel.Children.Clear();

        var fieldRow = MakeRow("Version Field", MakeTextBox(vm, nameof(vm.Field)));
        ((TextBox)((StackPanel)fieldRow).Children[1]).ToolTip =
            "Variable name set by a prior field block whose value drives version selection.";
        VersioningPanel.Children.Add(fieldRow);

        var mapLbl = new TextBlock
        {
            Text = "Version Map  (raw value  →  version key)",
            FontSize = 11, Margin = new Thickness(0, 8, 0, 2),
        };
        mapLbl.SetResourceReference(TextBlock.ForegroundProperty, "TE_LineNumberForeground");
        VersioningPanel.Children.Add(mapLbl);

        var ic = new ItemsControl { ItemsSource = vm.VersionMap };
        ic.ItemTemplate = MakeVersionMapTemplate();
        VersioningPanel.Children.Add(ic);

        var addBtn = new Button
        {
            Content = "+ Add Entry", FontSize = 11, Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(0, 4, 0, 4), HorizontalAlignment = HorizontalAlignment.Left,
            Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0),
            Command = vm.AddEntryCommand,
        };
        addBtn.SetResourceReference(Button.ForegroundProperty, "ET_AccentBrush");
        VersioningPanel.Children.Add(addBtn);

        var setsLbl = new TextBlock
        {
            Text = "Versioned Block Sets  (existing — edit raw JSON to modify blocks)",
            FontSize = 10, Opacity = 0.6, TextWrapping = System.Windows.TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 2),
        };
        setsLbl.SetResourceReference(TextBlock.ForegroundProperty, "TE_LineNumberForeground");
        VersioningPanel.Children.Add(setsLbl);

        var setsIc = new ItemsControl { ItemsSource = vm.VersionedSets };
        setsIc.ItemTemplate = MakeVersionedSetTemplate();
        VersioningPanel.Children.Add(setsIc);
    }

    private static DataTemplate MakeVersionMapTemplate()
    {
        var template = new DataTemplate();
        // Use a StackPanel with horizontal orientation
        var sp = new FrameworkElementFactory(typeof(StackPanel));
        sp.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        sp.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 2, 0, 2));

        var rawBox = new FrameworkElementFactory(typeof(TextBox));
        rawBox.SetValue(TextBox.MinWidthProperty, 120.0);
        rawBox.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0));
        rawBox.SetValue(TextBox.FontSizeProperty, 11.0);
        rawBox.SetValue(Control.PaddingProperty, new Thickness(4, 2, 4, 2));
        rawBox.SetValue(TextBox.ToolTipProperty, "Raw field value (e.g. 523 or 0x20b)");
        rawBox.SetBinding(TextBox.TextProperty,
            new System.Windows.Data.Binding(nameof(VersionMapEntryViewModel.RawValue))
            { UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged });
        sp.AppendChild(rawBox);

        var arrow = new FrameworkElementFactory(typeof(TextBlock));
        arrow.SetValue(TextBlock.TextProperty, " → ");
        arrow.SetValue(TextBlock.FontSizeProperty, 11.0);
        arrow.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0));
        arrow.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        sp.AppendChild(arrow);

        var keyBox = new FrameworkElementFactory(typeof(TextBox));
        keyBox.SetValue(TextBox.MinWidthProperty, 120.0);
        keyBox.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0));
        keyBox.SetValue(TextBox.FontSizeProperty, 11.0);
        keyBox.SetValue(Control.PaddingProperty, new Thickness(4, 2, 4, 2));
        keyBox.SetValue(TextBox.ToolTipProperty, "Version key (e.g. PE32+ or v2)");
        keyBox.SetBinding(TextBox.TextProperty,
            new System.Windows.Data.Binding(nameof(VersionMapEntryViewModel.VersionKey))
            { UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged });
        sp.AppendChild(keyBox);

        var remBtn = new FrameworkElementFactory(typeof(Button));
        remBtn.SetValue(Button.ContentProperty, "✕");
        remBtn.SetValue(Button.FontSizeProperty, 10.0);
        remBtn.SetValue(Button.PaddingProperty, new Thickness(4, 1, 4, 1));
        remBtn.SetValue(Button.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
        remBtn.SetValue(Button.BorderThicknessProperty, new Thickness(0));
        remBtn.SetBinding(Button.CommandProperty,
            new System.Windows.Data.Binding(nameof(VersionMapEntryViewModel.RemoveCommand)));
        sp.AppendChild(remBtn);

        template.VisualTree = sp;
        return template;
    }

    private static DataTemplate MakeVersionedSetTemplate()
    {
        var template = new DataTemplate();
        var sp = new FrameworkElementFactory(typeof(StackPanel));
        sp.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        sp.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 1, 0, 1));

        var keyTb = new FrameworkElementFactory(typeof(TextBlock));
        keyTb.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding(nameof(VersionedBlockSetInfo.VersionKey)));
        keyTb.SetValue(TextBlock.FontSizeProperty, 11.0);
        keyTb.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        keyTb.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 0));
        sp.AppendChild(keyTb);

        var cntTb = new FrameworkElementFactory(typeof(TextBlock));
        cntTb.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding(nameof(VersionedBlockSetInfo.BlockCount))
            { StringFormat = "({0} blocks)" });
        cntTb.SetValue(TextBlock.FontSizeProperty, 10.0);
        cntTb.SetValue(TextBlock.OpacityProperty, 0.6);
        cntTb.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        sp.AppendChild(cntTb);

        template.VisualTree = sp;
        return template;
    }

    private void BuildForensicPanel(ForensicViewModel vm)
    {
        ForensicPanel.Children.Clear();

        // Risk level
        var riskRow = MakeRow("Risk Level", new ComboBox
        {
            FontSize = 11,
            ItemsSource = ForensicViewModel.RiskLevelOptions,
        });
        ((ComboBox)((StackPanel)riskRow).Children[1]).SetBinding(
            ComboBox.SelectedItemProperty,
            new System.Windows.Data.Binding(nameof(vm.RiskLevel)) { Source = vm });
        ForensicPanel.Children.Add(riskRow);

        // Suspicious patterns
        var suspLbl = new TextBlock { Text = "Suspicious Patterns", FontSize = 11, Margin = new Thickness(0, 6, 0, 2) };
        suspLbl.SetResourceReference(TextBlock.ForegroundProperty, "TE_LineNumberForeground");
        ForensicPanel.Children.Add(suspLbl);
        ForensicPanel.Children.Add(MakePatternList(vm.SuspiciousPatterns, vm.AddSuspiciousCommand));

        var malLbl = new TextBlock { Text = "Malicious Patterns", FontSize = 11, Margin = new Thickness(0, 6, 0, 2) };
        malLbl.SetResourceReference(TextBlock.ForegroundProperty, "TE_LineNumberForeground");
        ForensicPanel.Children.Add(malLbl);
        ForensicPanel.Children.Add(MakePatternList(vm.MaliciousPatterns, vm.AddMaliciousCommand));
    }

    private void BuildInspectorPanel(InspectorViewModel vm)
    {
        InspectorPanel.Children.Clear();
        InspectorPanel.Children.Add(MakeRow("Badge", MakeTextBox(vm, nameof(vm.Badge))));
        InspectorPanel.Children.Add(MakeRow("Primary Field", MakeTextBox(vm, nameof(vm.PrimaryField))));
    }

    private void BuildAiPanel(AiHintsViewModel vm)
    {
        AiPanel.Children.Clear();
        var ctx = new TextBox
        {
            AcceptsReturn = true, Height = 60, FontSize = 11,
            TextWrapping  = System.Windows.TextWrapping.Wrap,
            Padding = new Thickness(4, 2, 4, 2),
        };
        ctx.SetResourceReference(TextBox.BackgroundProperty, "TE_Background");
        ctx.SetResourceReference(TextBox.ForegroundProperty, "TE_Foreground");
        ctx.SetBinding(TextBox.TextProperty,
            new System.Windows.Data.Binding(nameof(vm.AnalysisContext))
            {
                Source = vm,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
            });

        var lbl = new TextBlock { Text = "Analysis Context", FontSize = 11, Margin = new Thickness(0, 0, 0, 2) };
        lbl.SetResourceReference(TextBlock.ForegroundProperty, "TE_LineNumberForeground");
        AiPanel.Children.Add(lbl);
        AiPanel.Children.Add(ctx);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static UIElement MakePatternList(
        System.Collections.ObjectModel.ObservableCollection<ForensicPatternViewModel> col,
        System.Windows.Input.ICommand addCommand)
    {
        var sp = new StackPanel();
        var ic = new ItemsControl { ItemsSource = col };
        ic.ItemTemplate = MakeForensicPatternTemplate();
        sp.Children.Add(ic);
        var btn = new Button
        {
            Content = "＋ Add Pattern", FontSize = 11, Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(0, 4, 0, 0), HorizontalAlignment = HorizontalAlignment.Left,
            Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0),
            Command = addCommand,
        };
        btn.SetResourceReference(Button.ForegroundProperty, "ET_AccentBrush");
        sp.Children.Add(btn);
        return sp;
    }

    private static DataTemplate MakeForensicPatternTemplate()
    {
        // Minimal template — name + condition + remove button
        var template = new DataTemplate();
        var sp = new FrameworkElementFactory(typeof(StackPanel));
        sp.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        sp.SetValue(StackPanel.MarginProperty, new Thickness(0, 2, 0, 2));

        var nameBox = new FrameworkElementFactory(typeof(TextBox));
        nameBox.SetValue(TextBox.WidthProperty, 120.0);
        nameBox.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0));
        nameBox.SetBinding(TextBox.TextProperty,
            new System.Windows.Data.Binding(nameof(ForensicPatternViewModel.PatternName))
            { UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged });
        sp.AppendChild(nameBox);

        var condBox = new FrameworkElementFactory(typeof(TextBox));
        condBox.SetValue(TextBox.MinWidthProperty, 180.0);
        condBox.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0));
        condBox.SetBinding(TextBox.TextProperty,
            new System.Windows.Data.Binding(nameof(ForensicPatternViewModel.Condition))
            { UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged });
        sp.AppendChild(condBox);

        var remBtn = new FrameworkElementFactory(typeof(Button));
        remBtn.SetValue(Button.ContentProperty, "✕");
        remBtn.SetValue(Button.FontSizeProperty, 10.0);
        remBtn.SetValue(Button.PaddingProperty, new Thickness(4, 1, 4, 1));
        remBtn.SetValue(Button.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
        remBtn.SetValue(Button.BorderThicknessProperty, new Thickness(0));
        remBtn.SetBinding(Button.CommandProperty,
            new System.Windows.Data.Binding(nameof(ForensicPatternViewModel.RemoveCommand)));
        sp.AppendChild(remBtn);

        template.VisualTree = sp;
        return template;
    }

    private static UIElement MakeRow(string label, UIElement control)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        var lbl = new TextBlock { Text = label, FontSize = 11, MinWidth = 100, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        lbl.SetResourceReference(TextBlock.ForegroundProperty, "TE_LineNumberForeground");
        sp.Children.Add(lbl);
        sp.Children.Add(control);
        return sp;
    }

    private static TextBox MakeTextBox(object source, string path)
    {
        var tb = new TextBox { FontSize = 11, MinWidth = 180, Padding = new Thickness(4, 2, 4, 2), BorderThickness = new Thickness(1) };
        tb.SetResourceReference(TextBox.BackgroundProperty, "TE_Background");
        tb.SetResourceReference(TextBox.ForegroundProperty, "TE_Foreground");
        tb.SetBinding(TextBox.TextProperty,
            new System.Windows.Data.Binding(path)
            { Source = source, UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged });
        return tb;
    }
}

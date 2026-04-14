//////////////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project: WpfHexEditor.Editor.StructureEditor
// File: Tabs/BlocksTab.xaml.cs
// Description: Code-behind for Blocks tab — tree selection, AddBlockDialog wiring,
//              empty-state indicator, Raw JSON popup.
//////////////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.StructureEditor.Dialogs;
using WpfHexEditor.Editor.StructureEditor.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.Tabs;

public sealed partial class BlocksTab : UserControl
{
    public BlocksTab() => InitializeComponent();

    private BlocksViewModel? VM => DataContext as BlocksViewModel;

    private void OnAddBlock(object sender, RoutedEventArgs e)
    {
        var dlg = new AddBlockDialog { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true) return;
        VM?.AddBlock(dlg.SelectedBlockType, dlg.BlockName);
    }

    private void OnRemoveBlock(object sender, RoutedEventArgs e)
    {
        // Command already handles this — button kept for discoverability
    }

    private void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (VM is null) return;

        // Unsubscribe previous block's raw event
        if (VM.SelectedBlock is { } prev)
            prev.OpenRawRequested -= OnOpenRawRequested;

        VM.SelectedBlock = e.NewValue as BlockViewModel;

        // Subscribe new block
        if (VM.SelectedBlock is { } next)
            next.OpenRawRequested += OnOpenRawRequested;

        var hasSelection = VM.SelectedBlock is not null;
        EmptyHint.Visibility   = hasSelection ? Visibility.Collapsed : Visibility.Visible;
        BlockEditor.Visibility = hasSelection ? Visibility.Visible   : Visibility.Collapsed;
    }

    // ── Raw JSON popup ────────────────────────────────────────────────────────

    private void OnOpenRawRequested(object? sender, EventArgs e)
    {
        if (sender is not BlockViewModel vm) return;
        OpenRawJsonWindow(vm);
    }

    private void OpenRawJsonWindow(BlockViewModel vm)
    {
        var json = vm.ToRawJson();

        var textBox = new TextBox
        {
            Text             = json,
            AcceptsReturn    = true,
            AcceptsTab       = true,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            FontFamily       = new FontFamily("Consolas, Courier New"),
            FontSize         = 12,
            Padding          = new Thickness(8),
            BorderThickness  = new Thickness(0),
        };
        textBox.SetResourceReference(TextBox.BackgroundProperty, "TE_Background");
        textBox.SetResourceReference(TextBox.ForegroundProperty, "TE_Foreground");

        var applyBtn = new Button
        {
            Content = "Apply",
            Width   = 80,
            Margin  = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(6, 3, 6, 3),
        };
        applyBtn.SetResourceReference(Button.ForegroundProperty, "TE_Foreground");

        var cancelBtn = new Button
        {
            Content = "Cancel",
            Width   = 80,
            Padding = new Thickness(6, 3, 6, 3),
        };
        cancelBtn.SetResourceReference(Button.ForegroundProperty, "TE_Foreground");

        var btnRow = new StackPanel
        {
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin              = new Thickness(8),
        };
        btnRow.Children.Add(applyBtn);
        btnRow.Children.Add(cancelBtn);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(textBox, 0);
        Grid.SetRow(btnRow,  1);
        root.Children.Add(textBox);
        root.Children.Add(btnRow);

        var win = new Window
        {
            Title                 = $"Raw JSON — {vm.Name}",
            Width                 = 600,
            Height                = 480,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner                 = Window.GetWindow(this),
            ShowInTaskbar         = false,
            Content               = root,
            ResizeMode            = ResizeMode.CanResizeWithGrip,
        };
        win.SetResourceReference(Window.BackgroundProperty, "TE_Background");

        applyBtn.Click  += (_, _) => { vm.LoadFromRawJson(textBox.Text); win.Close(); };
        cancelBtn.Click += (_, _) => win.Close();

        // Ctrl+Enter also applies
        textBox.KeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                vm.LoadFromRawJson(textBox.Text);
                win.Close();
                ke.Handled = true;
            }
        };

        win.ShowDialog();
    }
}

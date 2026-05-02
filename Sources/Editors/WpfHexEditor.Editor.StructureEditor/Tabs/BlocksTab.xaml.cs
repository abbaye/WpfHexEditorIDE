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
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.StructureEditor.Dialogs;
using WpfHexEditor.Editor.StructureEditor.Properties;
using WpfHexEditor.Editor.StructureEditor.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.Tabs;

// ── Drop indicator adorner ────────────────────────────────────────────────────

file sealed class DropLineAdorner : Adorner
{
    private readonly bool _below;

    public DropLineAdorner(UIElement element, bool below) : base(element) { _below = below; IsHitTestVisible = false; }

    protected override void OnRender(DrawingContext dc)
    {
        var pen = new Pen(Brushes.CornflowerBlue, 2) { DashStyle = DashStyles.Solid };
        var y   = _below ? ((FrameworkElement)AdornedElement).ActualHeight - 1 : 1;
        dc.DrawLine(pen, new Point(0, y), new Point(((FrameworkElement)AdornedElement).ActualWidth, y));
    }
}

// ── Visual tree helper ────────────────────────────────────────────────────────

file static class BlocksTabExtensions
{
    internal static BlockViewModel? FindBlockViewModelAncestor(this DependencyObject obj)
    {
        var cur = obj;
        while (cur is not null)
        {
            if (cur is FrameworkElement fe && fe.DataContext is BlockViewModel vm)
                return vm;
            cur = VisualTreeHelper.GetParent(cur);
        }
        return null;
    }
}

public sealed partial class BlocksTab : UserControl
{
    public BlocksTab()
    {
        InitializeComponent();
        InputBindings.Add(new KeyBinding(
            new ViewModels.RelayCommand(() => SearchBox.Focus()),
            Key.F, ModifierKeys.Control));
    }

    private BlocksViewModel? VM => DataContext as BlocksViewModel;

    private void OnAddBlock(object sender, RoutedEventArgs e) => RequestAddBlock();

    /// <summary>Opens the Add Block dialog. Called from pop-toolbar and toolbar.</summary>
    internal void RequestAddBlock()
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

    // ── Drag-and-drop reordering ──────────────────────────────────────────────

    private Point          _dragStart;
    private BlockViewModel? _dragSource;
    private Adorner? _dropAdorner;

    private void OnBlockDragStart(object sender, MouseButtonEventArgs e)
    {
        _dragStart  = e.GetPosition(BlockTree);
        _dragSource = (e.OriginalSource as DependencyObject)?.FindBlockViewModelAncestor();
    }

    private void OnBlockDragMove(object sender, MouseEventArgs e)
    {
        if (_dragSource is null || e.LeftButton != MouseButtonState.Pressed) return;
        var pos = e.GetPosition(BlockTree);
        if (Math.Abs(pos.X - _dragStart.X) < 4 && Math.Abs(pos.Y - _dragStart.Y) < 4) return;

        DragDrop.DoDragDrop(BlockTree, _dragSource, DragDropEffects.Move);
        _dragSource = null;
        RemoveDropAdorner();
    }

    private void OnBlockDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(BlockViewModel)))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        // Show a drop-indicator line between items
        var targetItem = FindTreeViewItemAt(e.GetPosition(BlockTree));
        UpdateDropAdorner(targetItem, e.GetPosition(targetItem is not null ? (IInputElement)targetItem : BlockTree));
    }

    private void OnBlockDragLeave(object sender, DragEventArgs e) => RemoveDropAdorner();

    private void OnBlockDrop(object sender, DragEventArgs e)
    {
        RemoveDropAdorner();
        if (!e.Data.GetDataPresent(typeof(BlockViewModel))) return;

        var source = (BlockViewModel)e.Data.GetData(typeof(BlockViewModel));
        var vm     = VM;
        if (vm is null || source is null) return;

        var targetItem = FindTreeViewItemAt(e.GetPosition(BlockTree));
        var targetVm   = targetItem?.DataContext as BlockViewModel;
        if (targetVm is null || ReferenceEquals(source, targetVm)) return;

        var targetIdx = vm.BlockTree.IndexOf(targetVm);
        if (targetIdx >= 0)
            vm.MoveBlock(source, targetIdx);

        e.Handled = true;
    }

    // ── Drag helpers ──────────────────────────────────────────────────────────

    private TreeViewItem? FindTreeViewItemAt(Point pos)
    {
        var hit = BlockTree.InputHitTest(pos) as DependencyObject;
        while (hit is not null)
        {
            if (hit is TreeViewItem tvi) return tvi;
            hit = VisualTreeHelper.GetParent(hit);
        }
        return null;
    }

    private void UpdateDropAdorner(TreeViewItem? item, Point relPos)
    {
        RemoveDropAdorner();
        if (item is null) return;
        var layer = AdornerLayer.GetAdornerLayer(BlockTree);
        if (layer is null) return;
        var below = relPos.Y > item.ActualHeight / 2;
        _dropAdorner = new DropLineAdorner(item, below);
        layer.Add(_dropAdorner);
    }

    private void RemoveDropAdorner()
    {
        if (_dropAdorner is null) return;
        AdornerLayer.GetAdornerLayer(BlockTree)?.Remove(_dropAdorner);
        _dropAdorner = null;
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
            Content = StructureEditorResources.StructureEditor_RawJsonApply,
            Width   = 80,
            Margin  = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(6, 3, 6, 3),
        };
        applyBtn.SetResourceReference(Button.ForegroundProperty, "TE_Foreground");

        var cancelBtn = new Button
        {
            Content = StructureEditorResources.StructureEditor_RawJsonCancel,
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
            Title                 = string.Format(StructureEditorResources.StructureEditor_RawJsonTitle, vm.Name),
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

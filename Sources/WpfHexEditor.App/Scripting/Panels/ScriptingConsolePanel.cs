// Project      : WpfHexEditor.App
// File         : Scripting/Panels/ScriptingConsolePanel.cs
// Description  : Dockable Scripting Console panel — code-behind-only UserControl.
// Architecture : Multiline input + Run/Cancel/Clear toolbar + scrollable output log.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.App.Scripting.ViewModels;

namespace WpfHexEditor.App.Scripting.Panels;

public sealed class ScriptingConsolePanel : UserControl
{
    private readonly ScriptingConsolePanelViewModel _vm;
    private readonly TextBox _inputBox;

    public ScriptingConsolePanel(ScriptingConsolePanelViewModel vm)
    {
        _vm = vm;

        var runBtn    = new Button { Content = "▶ Run",   Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 4, 0) };
        var cancelBtn = new Button { Content = "■ Cancel", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 4, 0) };
        var clearBtn  = new Button { Content = "Clear",    Padding = new Thickness(8, 2, 8, 2) };

        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 4, 4, 2) };
        toolbar.Children.Add(runBtn);
        toolbar.Children.Add(cancelBtn);
        toolbar.Children.Add(clearBtn);

        _inputBox = new TextBox
        {
            AcceptsReturn  = true,
            AcceptsTab     = true,
            FontFamily     = new FontFamily("Consolas, Courier New"),
            FontSize       = 13,
            MinHeight      = 80,
            MaxHeight      = 200,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin         = new Thickness(4, 0, 4, 4),
        };
        _inputBox.SetBinding(TextBox.TextProperty, new Binding(nameof(_vm.Code)) { Source = _vm, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        _inputBox.KeyDown += OnInputKeyDown;

        var outputList = new ItemsControl { ItemsSource = _vm.Output };
        outputList.ItemTemplate = BuildOutputTemplate();

        var scroll = new ScrollViewer
        {
            Content                     = outputList,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin                      = new Thickness(4, 0, 4, 4),
        };

        NotifyCollectionChangedEventHandler scrollHandler =
            (_, _) => scroll.Dispatcher.InvokeAsync(
                scroll.ScrollToBottom,
                System.Windows.Threading.DispatcherPriority.Background);

        _vm.Output.CollectionChanged += scrollHandler;
        Unloaded += (_, _) => _vm.Output.CollectionChanged -= scrollHandler;

        var root = new DockPanel();
        DockPanel.SetDock(toolbar,   Dock.Top);
        DockPanel.SetDock(_inputBox, Dock.Top);
        root.Children.Add(toolbar);
        root.Children.Add(_inputBox);
        root.Children.Add(scroll);
        Content     = root;
        DataContext = _vm;

        runBtn.Click    += async (_, _) => await _vm.RunAsync();
        cancelBtn.Click += (_, _) => _vm.Cancel();
        clearBtn.Click  += (_, _) => _vm.ClearOutput();

        runBtn.SetBinding(UIElement.IsEnabledProperty,
            new Binding(nameof(_vm.IsBusy)) { Source = _vm, Converter = new InverseBoolConverter() });
        cancelBtn.SetBinding(UIElement.IsEnabledProperty,
            new Binding(nameof(_vm.IsBusy)) { Source = _vm });
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            e.Handled = true;
            _ = _vm.RunAsync();
            return;
        }

        if (e.Key == Key.Up && (Keyboard.Modifiers & ModifierKeys.Alt) != 0)
        {
            e.Handled = true;
            var prev = _vm.HistoryUp();
            if (prev is not null) { _vm.Code = prev; _inputBox.CaretIndex = prev.Length; }
            return;
        }

        if (e.Key == Key.Down && (Keyboard.Modifiers & ModifierKeys.Alt) != 0)
        {
            e.Handled = true;
            var next = _vm.HistoryDown();
            if (next is not null) { _vm.Code = next; _inputBox.CaretIndex = next.Length; }
        }
    }

    private static DataTemplate BuildOutputTemplate()
    {
        var factory = new FrameworkElementFactory(typeof(TextBlock));
        factory.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Consolas, Courier New"));
        factory.SetValue(TextBlock.FontSizeProperty, 12.0);
        factory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        factory.SetBinding(TextBlock.TextProperty, new Binding(nameof(OutputEntry.Text)));
        factory.SetBinding(TextBlock.ForegroundProperty,
            new Binding(nameof(OutputEntry.IsError)) { Converter = new ErrorForegroundConverter() });
        return new DataTemplate { VisualTree = factory };
    }

    private sealed class InverseBoolConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type t, object p, System.Globalization.CultureInfo c)
            => value is bool b && !b;
        public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c)
            => throw new NotSupportedException();
    }

    private sealed class ErrorForegroundConverter : System.Windows.Data.IValueConverter
    {
        private static readonly SolidColorBrush _error  = new(Color.FromRgb(255, 80,  80));
        private static readonly SolidColorBrush _normal = new(Color.FromRgb(200, 200, 200));

        public object Convert(object value, Type t, object p, System.Globalization.CultureInfo c)
            => value is true ? _error : _normal;
        public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c)
            => throw new NotSupportedException();
    }
}

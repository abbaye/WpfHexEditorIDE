//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using WpfHexEditor.App.BinaryAnalysis.ViewModels;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.App.BinaryAnalysis.Panels;

/// <summary>#120 XOR/ROT Cipher Decoder — code-behind-only panel.</summary>
public sealed class CipherDecoderPanel : UserControl
{
    private readonly CipherDecoderViewModel _vm = new();

    public CipherDecoderPanel()
    {
        // --- Mode selector -------------------------------------------------
        var modeLabel = new TextBlock { Text = "Mode:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
        var modeCombo = new ComboBox { Width = 130, Margin = new Thickness(0, 0, 8, 0) };
        modeCombo.Items.Add("XOR — single key");
        modeCombo.Items.Add("XOR — rolling key");
        modeCombo.Items.Add("ROT — alpha");
        modeCombo.Items.Add("ROT-47");
        modeCombo.Items.Add("Auto-detect XOR");
        modeCombo.SetBinding(Selector.SelectedIndexProperty, new Binding(nameof(_vm.ModeIndex)) { Source = _vm });

        // --- XOR key input -------------------------------------------------
        var keyLabel = new TextBlock { Text = "Key (hex):", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
        keyLabel.SetBinding(VisibilityProperty, new Binding(nameof(_vm.IsXorMode)) { Source = _vm, Converter = new CipherBoolVisConverter() });
        var keyBox = new TextBox { Width = 100, Margin = new Thickness(0, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center };
        keyBox.SetBinding(TextBox.TextProperty, new Binding(nameof(_vm.XorKeyHex)) { Source = _vm, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        keyBox.SetBinding(VisibilityProperty, new Binding(nameof(_vm.IsXorMode)) { Source = _vm, Converter = new CipherBoolVisConverter() });

        // --- ROT shift input -----------------------------------------------
        var rotLabel = new TextBlock { Text = "Shift:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
        rotLabel.SetBinding(VisibilityProperty, new Binding(nameof(_vm.IsRotMode)) { Source = _vm, Converter = new CipherBoolVisConverter() });
        var rotBox = new TextBox { Width = 50, Margin = new Thickness(0, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center };
        rotBox.SetBinding(TextBox.TextProperty, new Binding(nameof(_vm.RotShift)) { Source = _vm, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        rotBox.SetBinding(VisibilityProperty, new Binding(nameof(_vm.IsRotMode)) { Source = _vm, Converter = new CipherBoolVisConverter() });

        // --- Range / selection toggle ---------------------------------------
        var selChk = new CheckBox { Content = "Use selection", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        selChk.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, new Binding(nameof(_vm.UseSelection)) { Source = _vm });

        var lenLabel = new TextBlock { Text = "Bytes:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
        var lenBox = new TextBox { Width = 60, Margin = new Thickness(0, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center };
        lenBox.SetBinding(TextBox.TextProperty, new Binding(nameof(_vm.RangeLength)) { Source = _vm, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

        // --- Action buttons ------------------------------------------------
        var decodeBtn = new Button { Content = "Decode", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 4, 0) };
        decodeBtn.SetBinding(ButtonBase.CommandProperty, new Binding(nameof(_vm.DecodeCommand)) { Source = _vm });
        var cancelBtn = new Button { Content = "Cancel", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 8, 0) };
        cancelBtn.SetBinding(ButtonBase.CommandProperty, new Binding(nameof(_vm.CancelCommand)) { Source = _vm });
        var copyBtn = new Button { Content = "Copy", Padding = new Thickness(10, 2, 10, 2) };
        copyBtn.SetBinding(ButtonBase.CommandProperty, new Binding(nameof(_vm.CopyCommand)) { Source = _vm });

        var statusTxt = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        statusTxt.SetBinding(TextBlock.TextProperty, new Binding(nameof(_vm.StatusText)) { Source = _vm });

        var toolbar = new WrapPanel { Margin = new Thickness(4), Orientation = Orientation.Horizontal };
        toolbar.Children.Add(modeLabel);
        toolbar.Children.Add(modeCombo);
        toolbar.Children.Add(keyLabel);
        toolbar.Children.Add(keyBox);
        toolbar.Children.Add(rotLabel);
        toolbar.Children.Add(rotBox);
        toolbar.Children.Add(selChk);
        toolbar.Children.Add(lenLabel);
        toolbar.Children.Add(lenBox);
        toolbar.Children.Add(decodeBtn);
        toolbar.Children.Add(cancelBtn);
        toolbar.Children.Add(copyBtn);
        toolbar.Children.Add(statusTxt);

        // --- Preview area --------------------------------------------------
        var previewBox = new TextBox
        {
            IsReadOnly          = true,
            AcceptsReturn       = true,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily          = new System.Windows.Media.FontFamily("Consolas"),
            FontSize            = 12,
            Margin              = new Thickness(4, 0, 4, 0)
        };
        previewBox.SetBinding(TextBox.TextProperty, new Binding(nameof(_vm.PreviewText)) { Source = _vm, Mode = BindingMode.OneWay });

        // --- Auto-detect results list (visible only in mode 4) -------------
        var autoList = new ListBox { Margin = new Thickness(4, 0, 4, 4), MaxHeight = 150 };
        autoList.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(_vm.AutoResults)) { Source = _vm });
        autoList.SetBinding(VisibilityProperty, new Binding(nameof(_vm.ModeIndex))
        {
            Source = _vm,
            Converter = new CipherIndexVisConverter(4)
        });

        // --- Layout --------------------------------------------------------
        var root = new DockPanel();
        DockPanel.SetDock(toolbar, Dock.Top);
        DockPanel.SetDock(autoList, Dock.Bottom);
        root.Children.Add(toolbar);
        root.Children.Add(autoList);
        root.Children.Add(previewBox);
        Content = root;
    }

    public void SetContext(IIDEHostContext context) => _vm.SetContext(context);

    public void OnFileOpened() => _vm.OnFileOpened();
}

// -- File-scoped converters --------------------------------------------------

file sealed class CipherBoolVisConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object v, Type t, object p, System.Globalization.CultureInfo c) =>
        v is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c) =>
        throw new NotSupportedException();
}

file sealed class CipherIndexVisConverter(int targetIndex) : System.Windows.Data.IValueConverter
{
    public object Convert(object v, Type t, object p, System.Globalization.CultureInfo c) =>
        v is int i && i == targetIndex ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c) =>
        throw new NotSupportedException();
}

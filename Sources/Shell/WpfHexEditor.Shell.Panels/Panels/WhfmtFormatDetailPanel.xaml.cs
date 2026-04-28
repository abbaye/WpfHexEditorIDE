// ==========================================================
// Project: WpfHexEditor.Shell.Panels
// File: Panels/WhfmtFormatDetailPanel.xaml.cs
// Description: Code-behind for the format detail card panel.
//              JSON tab uses the in-house CodeEditor for syntax highlighting.
// ==========================================================

using System.ComponentModel;
using System.Windows.Controls;
using WpfHexEditor.Shell.Panels.ViewModels;

namespace WpfHexEditor.Shell.Panels.Panels;

public partial class WhfmtFormatDetailPanel : UserControl
{
    public WhfmtFormatDetailPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private WhfmtFormatDetailVm? _vm;

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = e.NewValue as WhfmtFormatDetailVm;

        if (_vm is not null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            LoadJson(_vm.RawJson);
        }
        else
        {
            JsonCodeEditor.LoadText(string.Empty);
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WhfmtFormatDetailVm.RawJson))
            LoadJson(_vm?.RawJson);
    }

    private void LoadJson(string? json)
        => JsonCodeEditor.LoadText(json ?? string.Empty);
}

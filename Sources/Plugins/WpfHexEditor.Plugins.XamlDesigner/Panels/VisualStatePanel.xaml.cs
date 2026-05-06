// ==========================================================
// Project: WpfHexEditor.Plugins.XamlDesigner
// File: VisualStatePanel.xaml.cs
// Description:
//     Code-behind for the VisualStateManager dockable panel.
// Architecture Notes:
//     Lifecycle: do NOT null _vm in OnUnloaded.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Plugins.XamlDesigner.ViewModels;

namespace WpfHexEditor.Plugins.XamlDesigner.Panels;

/// <summary>
/// VisualStateManager editor panel — shows VSM groups, states, and setters.
/// </summary>
public partial class VisualStatePanel : UserControl
{
    private VisualStatePanelViewModel? _vm;

    public VisualStatePanel()
    {
        InitializeComponent();
    }

    public void SetViewModel(VisualStatePanelViewModel vm)
    {
        _vm         = vm;
        DataContext = vm;
    }
}

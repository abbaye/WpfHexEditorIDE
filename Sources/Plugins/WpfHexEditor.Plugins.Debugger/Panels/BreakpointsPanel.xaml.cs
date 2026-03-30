// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: Panels/BreakpointsPanel.xaml.cs
// Description: Code-behind for BreakpointsPanel — wires hover popup.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Plugins.Debugger.Controls;
using WpfHexEditor.Plugins.Debugger.ViewModels;

namespace WpfHexEditor.Plugins.Debugger.Panels;

public partial class BreakpointsPanel : UserControl
{
    private BreakpointHoverPopup? _popup;
    private BreakpointRowEx?      _lastHoveredRow;

    public BreakpointsPanel() => InitializeComponent();

    // ── Hover tracking ────────────────────────────────────────────────────────

    private void OnListViewMouseMove(object sender, MouseEventArgs e)
    {
        var row = HitTestRow(e.GetPosition(BpListView));
        if (row is null || ReferenceEquals(row, _lastHoveredRow)) return;

        _lastHoveredRow = row;

        if (DataContext is not BreakpointsPanelViewModel vm) return;

        _popup ??= new BreakpointHoverPopup { PlacementTarget = BpListView };
        _popup.Show(row, vm.Debugger);
    }

    private void OnListViewMouseLeave(object sender, MouseEventArgs e)
    {
        _lastHoveredRow = null;
        _popup?.OnHostMouseLeft();
    }

    // ── Hit-test helper ───────────────────────────────────────────────────────

    private BreakpointRowEx? HitTestRow(Point position)
    {
        var hit = VisualTreeHelper.HitTest(BpListView, position);
        if (hit is null) return null;

        DependencyObject? current = hit.VisualHit;
        while (current is not null)
        {
            if (current is ListViewItem { DataContext: BreakpointRowEx row })
                return row;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}

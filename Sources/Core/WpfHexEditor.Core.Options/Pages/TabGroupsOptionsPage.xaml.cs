// GNU Affero General Public License v3.0 - 2026
// Contributors: Claude Sonnet 4.6

using System;
using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Core.Options.Pages;

public sealed partial class TabGroupsOptionsPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;
    private bool _loading;

    public TabGroupsOptionsPage() => InitializeComponent();

    // -- IOptionsPage ----------------------------------------------------------

    public void Load(AppSettings s)
    {
        _loading = true;
        try
        {
            ChkEnforceEqualSize.IsChecked = s.TabGroups.EnforceEqualSize;
            ChkShowGroupBadge.IsChecked   = s.TabGroups.ShowGroupNumberBadge;
            ChkPersistLayout.IsChecked    = s.TabGroups.PersistTabGroupLayout;
            TxtMinWidth.Text              = s.TabGroups.MinGroupWidthPx.ToString();
            TxtMinHeight.Text             = s.TabGroups.MinGroupHeightPx.ToString();
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings s)
    {
        s.TabGroups.EnforceEqualSize      = ChkEnforceEqualSize.IsChecked == true;
        s.TabGroups.ShowGroupNumberBadge  = ChkShowGroupBadge.IsChecked   == true;
        s.TabGroups.PersistTabGroupLayout = ChkPersistLayout.IsChecked    == true;

        if (int.TryParse(TxtMinWidth.Text,  out int w) && w > 0) s.TabGroups.MinGroupWidthPx  = w;
        if (int.TryParse(TxtMinHeight.Text, out int h) && h > 0) s.TabGroups.MinGroupHeightPx = h;
    }

    // -- Handlers --------------------------------------------------------------

    private void OnChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }
}

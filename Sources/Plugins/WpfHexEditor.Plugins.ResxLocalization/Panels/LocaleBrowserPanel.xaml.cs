// ==========================================================
// Project: WpfHexEditor.Plugins.ResxLocalization
// File: Panels/LocaleBrowserPanel.xaml.cs
// Description:
//     Code-behind for the Locale Browser dockable panel.
//     Receives locale data via Refresh() called from
//     ResxLocalizationPlugin when ResxLocaleDiscoveredEvent fires.
//     Raises OpenLocaleRequested when the user selects a locale row.
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using WpfHexEditor.Plugins.ResxLocalization.ViewModels;

namespace WpfHexEditor.Plugins.ResxLocalization.Panels;

/// <summary>Converts bool (IsBase) to FontWeight for base locale emphasis.</summary>
public sealed class BoolToFontWeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? FontWeights.SemiBold : FontWeights.Normal;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Dockable panel listing all locale variants for the active .resx file.
/// </summary>
public partial class LocaleBrowserPanel : UserControl
{
    private readonly LocaleBrowserViewModel _vm = new();

    /// <summary>
    /// Raised when the user clicks a locale row.
    /// Payload is the file path to open.
    /// </summary>
    public event Action<string>? OpenLocaleRequested;

    public LocaleBrowserPanel()
    {
        InitializeComponent();
        DataContext = _vm;
    }

    // -----------------------------------------------------------------------
    // Public API — called by plugin
    // -----------------------------------------------------------------------

    public void Refresh(string basePath, string[] variantPaths)
        => _vm.Refresh(basePath, variantPaths);

    public void SetActiveFile(string filePath)
        => _vm.SetActiveFile(filePath);

    // -----------------------------------------------------------------------
    // Event handlers
    // -----------------------------------------------------------------------

    private void LocaleList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LocaleList.SelectedItem is LocaleRowViewModel row && !string.IsNullOrEmpty(row.FilePath))
            OpenLocaleRequested?.Invoke(row.FilePath);
    }
}

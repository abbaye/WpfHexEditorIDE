//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Windows;

namespace WpfHexEditor.ProjectSystem.Dialogs;

/// <summary>
/// Dialog for creating a new solution (.whsln).
/// After ShowDialog() returns true, read <see cref="SolutionName"/> and <see cref="SolutionDirectory"/>.
/// </summary>
public partial class NewSolutionDialog : Window
{
    public string SolutionName      { get; private set; } = "";
    public string SolutionDirectory { get; private set; } = "";

    public NewSolutionDialog()
    {
        InitializeComponent();
        LocationBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        NameBox.Focus();
    }

    private void OnNameOrLocationChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        => Refresh();

    private void Refresh()
    {
        var name = NameBox.Text.Trim();
        var loc  = LocationBox.Text.Trim();
        var valid = name.Length > 0 && loc.Length > 0
                    && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

        OkButton.IsEnabled = valid;
        PreviewText.Text   = valid
            ? Path.Combine(loc, name, name + ".whsln")
            : string.Empty;
    }

    private void OnBrowseLocation(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title            = "Select parent folder for the new solution",
            InitialDirectory = LocationBox.Text,
        };
        if (dlg.ShowDialog() == true)
            LocationBox.Text = dlg.FolderName;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        SolutionName      = NameBox.Text.Trim();
        SolutionDirectory = LocationBox.Text.Trim();
        DialogResult      = true;
    }
}

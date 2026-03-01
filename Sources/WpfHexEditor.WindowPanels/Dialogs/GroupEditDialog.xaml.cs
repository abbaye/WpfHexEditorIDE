//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;

namespace WpfHexEditor.WindowPanels.Dialogs;

/// <summary>
/// XAML-based dialog for editing bookmark group properties (Name + Description).
/// </summary>
internal partial class GroupEditDialog : Window
{
    public string GroupName        { get; private set; } = "";
    public string GroupDescription { get; private set; } = "";

    public GroupEditDialog(string initialName = "", string initialDescription = "")
    {
        InitializeComponent();
        NameTextBox.Text        = initialName;
        DescriptionTextBox.Text = initialDescription;
        NameTextBox.Focus();
        NameTextBox.SelectAll();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Group name cannot be empty.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            NameTextBox.Focus();
            return;
        }

        GroupName        = name;
        GroupDescription = DescriptionTextBox.Text.Trim();
        DialogResult     = true;
    }
}

//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using Microsoft.Win32;

namespace RiderSimpleExample;

/// <summary>
/// Simple WpfHexEditor example for JetBrains Rider users
///
/// This demonstrates:
/// - Opening files with HexEditor
/// - Handling events (SelectionChanged, DataCopied)
/// - Using properties (ReadOnlyMode, BytePerLine)
/// - Saving changes
/// - Loading TBL (Character Table) files for custom encoding
///
/// 💡 Rider Tips:
/// - Press Ctrl+Space to see IntelliSense for HexEditor properties
/// - Use XAML Preview (View -> Tool Windows -> XAML Preview) to see design
/// - Import Live Templates from docs/IDE/WpfHexEditor.DotSettings for quick snippets
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Open a binary file in the hex editor
    /// </summary>
    private void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select a file to open in hex editor",
            Filter = "All Files (*.*)|*.*|Binary Files (*.bin;*.dat)|*.bin;*.dat|Executable Files (*.exe;*.dll)|*.exe;*.dll",
            CheckFileExists = true,
            Multiselect = false
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                // Set the FileName property to open the file
                HexEditor.FileName = openFileDialog.FileName;

                StatusText.Text = $"📂 Opened: {System.IO.Path.GetFileName(openFileDialog.FileName)} ({HexEditor.Length} bytes)";
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error opening file: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                StatusText.Text = "❌ Error opening file";
            }
        }
    }

    /// <summary>
    /// Save changes to the file
    /// </summary>
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(HexEditor.FileName))
        {
            MessageBox.Show("No file is currently open.",
                "No File",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            // Submit changes to save them
            HexEditor.SubmitChanges();

            StatusText.Text = $"💾 Saved changes to: {System.IO.Path.GetFileName(HexEditor.FileName)}";

            MessageBox.Show("File saved successfully!",
                "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"Error saving file: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            StatusText.Text = "❌ Error saving file";
        }
    }

    /// <summary>
    /// Handle selection changes in the hex editor
    /// </summary>
    private void HexEditor_OnSelectionChanged(object sender, System.EventArgs e)
    {
        if (HexEditor.SelectionLength > 0)
        {
            SelectionText.Text = $"📍 Selection: {HexEditor.SelectionStart:N0} → {HexEditor.SelectionStop:N0} ({HexEditor.SelectionLength} bytes)";
        }
        else
        {
            SelectionText.Text = "No selection";
        }
    }

    /// <summary>
    /// Handle data copied event
    /// </summary>
    private void HexEditor_OnDataCopied(object sender, System.EventArgs e)
    {
        // Display current selection length as indication of copied data
        var length = HexEditor.SelectionLength > 0 ? HexEditor.SelectionLength : 0;
        StatusText.Text = length > 0
            ? $"📋 Copied {length} bytes to clipboard"
            : "📋 Data copied to clipboard";
    }

    /// <summary>
    /// Load a TBL (Character Table) file for custom encoding
    /// </summary>
    private void LoadTblButton_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select a TBL file",
            Filter = "TBL Files (*.tbl)|*.tbl|All Files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                // Load the TBL file
                HexEditor.LoadTBLFile(openFileDialog.FileName);

                // Enable the Close TBL button
                CloseTblButton.IsEnabled = true;

                StatusText.Text = $"📋 TBL loaded: {System.IO.Path.GetFileName(openFileDialog.FileName)}";

                MessageBox.Show(
                    $"TBL file loaded successfully!\n\n" +
                    $"File: {System.IO.Path.GetFileName(openFileDialog.FileName)}\n" +
                    $"Entries: {HexEditor.TBL?.Length ?? 0}\n\n" +
                    $"The hex editor will now display characters using the custom character table.",
                    "TBL Loaded",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error loading TBL file: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                StatusText.Text = "❌ Error loading TBL file";
            }
        }
    }

    /// <summary>
    /// Close the current TBL file and return to ASCII encoding
    /// </summary>
    private void CloseTblButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Close the TBL
            HexEditor.CloseTBL();

            // Disable the Close TBL button
            CloseTblButton.IsEnabled = false;

            StatusText.Text = "📋 TBL closed, using ASCII encoding";

            MessageBox.Show(
                "TBL file closed successfully.\n\n" +
                "The hex editor has returned to standard ASCII encoding.",
                "TBL Closed",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"Error closing TBL: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            StatusText.Text = "❌ Error closing TBL";
        }
    }
}

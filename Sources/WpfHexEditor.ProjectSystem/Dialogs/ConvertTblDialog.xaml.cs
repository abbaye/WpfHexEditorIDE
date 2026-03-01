//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace WpfHexEditor.ProjectSystem.Dialogs;

/// <summary>
/// Dialog that lets the user configure a .tbl → .tblx conversion, including
/// optional game metadata (title, platform, region, author, release year).
/// </summary>
public partial class ConvertTblDialog : Window
{
    private readonly string _sourcePath;
    private string _targetFolder;

    public ConvertTblDialog(string sourceTblPath)
    {
        InitializeComponent();

        _sourcePath   = sourceTblPath;
        _targetFolder = Path.GetDirectoryName(sourceTblPath) ?? string.Empty;

        SourceFileText.Text = Path.GetFileName(sourceTblPath);
        RefreshTargetText();
    }

    // ── Output properties consumed by the host ────────────────────────────────

    /// <summary>
    /// Full path of the source .tbl file.
    /// </summary>
    public string SourcePath => _sourcePath;

    /// <summary>
    /// Full path where the .tblx file will be written.
    /// </summary>
    public string TargetPath => Path.Combine(_targetFolder,
        Path.GetFileNameWithoutExtension(_sourcePath) + ".tblx");

    /// <summary>
    /// Game title (may be empty).
    /// </summary>
    public string GameTitle => GameTitleBox.Text.Trim();

    /// <summary>
    /// Platform string (may be empty).
    /// </summary>
    public string Platform  => GetComboText(PlatformCombo);

    /// <summary>
    /// Region string (may be empty).
    /// </summary>
    public string Region    => GetComboText(RegionCombo);

    /// <summary>
    /// Author (may be empty).
    /// </summary>
    public string Author    => AuthorBox.Text.Trim();

    /// <summary>
    /// Release year, or <see langword="null"/> if not entered / invalid.
    /// </summary>
    public int? ReleaseYear
    {
        get
        {
            var txt = YearBox.Text.Trim();
            return int.TryParse(txt, out int y) && y > 0 ? y : null;
        }
    }

    /// <summary>
    /// Whether the converted .tblx should be added to the project.
    /// </summary>
    public bool AddToProject       => AddToProjectCheck.IsChecked == true;

    /// <summary>
    /// Whether the converted .tblx should be opened in the TBL Editor.
    /// </summary>
    public bool OpenAfterConversion => OpenAfterCheck.IsChecked == true;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GetComboText(System.Windows.Controls.ComboBox combo)
    {
        if (combo.IsEditable && !string.IsNullOrWhiteSpace(combo.Text))
            return combo.Text.Trim();
        if (combo.SelectedItem is System.Windows.Controls.ComboBoxItem item)
            return item.Content?.ToString()?.Trim() ?? string.Empty;
        return string.Empty;
    }

    private void RefreshTargetText()
        => TargetFileText.Text = Path.GetFileNameWithoutExtension(_sourcePath) + ".tblx";

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnOutputLocationChanged(object sender, RoutedEventArgs e)
    {
        if (CustomFolderRow is null) return; // called during InitializeComponent
        var useCustom = CustomFolderRadio?.IsChecked == true;
        CustomFolderRow.Visibility = useCustom ? Visibility.Visible : Visibility.Collapsed;
        if (!useCustom)
        {
            _targetFolder = Path.GetDirectoryName(_sourcePath) ?? string.Empty;
            RefreshTargetText();
        }
    }

    private void OnCustomFolderChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _targetFolder = CustomFolderBox.Text.Trim();
        RefreshTargetText();
    }

    private void OnBrowseCustomFolder(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title             = "Select output folder for the converted TBLX file",
            InitialDirectory  = _targetFolder,
        };
        if (dlg.ShowDialog() == true)
        {
            CustomFolderBox.Text = dlg.FolderName;
            _targetFolder = dlg.FolderName;
            RefreshTargetText();
        }
    }

    private void OnYearPreviewTextInput(object sender, TextCompositionEventArgs e)
        => e.Handled = !Regex.IsMatch(e.Text, @"^\d$");

    private void OnConvert(object sender, RoutedEventArgs e)
    {
        // Validate output folder exists (or create it)
        if (!Directory.Exists(_targetFolder))
        {
            try { Directory.CreateDirectory(_targetFolder); }
            catch
            {
                System.Windows.MessageBox.Show(
                    $"Cannot create output folder:\n{_targetFolder}",
                    "Convert TBL", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        // Guard against overwriting source with same path/name edge case
        var target = TargetPath;
        if (string.Equals(target, _sourcePath, StringComparison.OrdinalIgnoreCase))
        {
            System.Windows.MessageBox.Show(
                "The output path is identical to the source. Choose a different folder.",
                "Convert TBL", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }
}

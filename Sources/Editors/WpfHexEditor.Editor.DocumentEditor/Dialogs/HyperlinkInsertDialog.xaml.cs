// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Dialogs/HyperlinkInsertDialog.xaml.cs
// Description: Modal dialog for inserting a hyperlink block.
//     Validates URL format and enables an optional open-in-browser preview.
// ==========================================================

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Editor.Core.Views;

namespace WpfHexEditor.Editor.DocumentEditor.Dialogs;

public partial class HyperlinkInsertDialog : ThemedDialog
{
    public string DisplayText { get; private set; } = string.Empty;
    public string Url         { get; private set; } = string.Empty;

    public HyperlinkInsertDialog(string initialText = "")
    {
        InitializeComponent();
        PART_DisplayText.Text = initialText;
        Loaded += (_, _) =>
        {
            if (string.IsNullOrEmpty(initialText))
                PART_DisplayText.Focus();
            else
                PART_Url.Focus();
        };
        ValidateUrl();
    }

    private void OnDisplayTextChanged(object sender, TextChangedEventArgs e)
    {
        // If display text is set and URL is empty, suggest https:// prefix
    }

    private void OnUrlChanged(object sender, TextChangedEventArgs e) => ValidateUrl();

    private void ValidateUrl()
    {
        var raw = PART_Url?.Text?.Trim() ?? string.Empty;
        bool empty = string.IsNullOrEmpty(raw);

        bool valid = !empty &&
                     Uri.TryCreate(raw, UriKind.Absolute, out var uri) &&
                     (uri.Scheme == Uri.UriSchemeHttps ||
                      uri.Scheme == Uri.UriSchemeHttp  ||
                      uri.Scheme == "mailto"            ||
                      uri.Scheme == "ftp");

        if (PART_OkBtn   is not null) PART_OkBtn.IsEnabled   = valid || empty;
        if (PART_TestBtn is not null) PART_TestBtn.IsEnabled  = valid;

        if (PART_UrlHint is not null)
        {
            if (!empty && !valid)
            {
                PART_UrlHint.Text       = TryFindResource("HyperlinkDlg_InvalidUrl") as string
                                          ?? "Please enter a valid URL (https://, http://, mailto:, ftp://)";
                PART_UrlHint.Visibility = Visibility.Visible;
            }
            else
            {
                PART_UrlHint.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void OnTestUrlClicked(object sender, RoutedEventArgs e)
    {
        var raw = PART_Url.Text.Trim();
        if (!string.IsNullOrEmpty(raw))
        {
            try { Process.Start(new ProcessStartInfo(raw) { UseShellExecute = true }); }
            catch { /* ignore */ }
        }
    }

    private void OnOkClicked(object sender, RoutedEventArgs e)
    {
        DisplayText  = PART_DisplayText.Text.Trim();
        Url          = PART_Url.Text.Trim();
        DialogResult = true;
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e) => DialogResult = false;
}

// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Dialogs/RoundTripPreviewDialog.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-10
// Description:
//     Confirmation dialog shown before a round-trip patch is written to
//     disk (ADR-022 Phase 1B). Displays the file path, the requested
//     edit, and a side-by-side before/after text comparison. The user
//     can Apply, Cancel, or opt out of seeing the dialog for the rest
//     of the session.
//
// Architecture Notes:
//     Lives in the Editor assembly so other modules can show the same
//     dialog if they ever produce a RoundTripResult. Owner window is
//     provided by the caller so the dialog inherits the IDE theme and
//     is modal over the right window.
// ==========================================================

using System.Windows;
using WpfHexEditor.Editor.ClassDiagram.Core.RoundTrip.Abstractions;

namespace WpfHexEditor.Editor.ClassDiagram.Dialogs;

/// <summary>
/// Modal preview dialog shown before a <see cref="RoundTripResult"/> is written
/// to disk. The session-wide "don't ask again" state is the caller's
/// responsibility — this dialog only reports the checkbox value.
/// </summary>
public partial class RoundTripPreviewDialog : Window
{
    /// <summary>True after the user clicks Apply.</summary>
    public bool Confirmed { get; private set; }

    /// <summary>Reflects the "Don't ask again" checkbox at close time.</summary>
    public bool DontAskAgain => DontAskAgainBox.IsChecked == true;

    public RoundTripPreviewDialog(RoundTripResult result, string editDescription)
    {
        InitializeComponent();

        HeaderFileText.Text = result.FilePath;
        HeaderEditText.Text = editDescription;
        BeforeBox.Text      = result.ContentBefore;
        AfterBox.Text       = result.ContentAfter;
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        Confirmed   = true;
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Confirmed   = false;
        DialogResult = false;
        Close();
    }

    /// <summary>
    /// Convenience helper — shows the dialog modally over <paramref name="owner"/>
    /// and returns true if the user clicked Apply.
    /// </summary>
    public static bool Confirm(Window? owner, RoundTripResult result, string editDescription, out bool dontAskAgain)
    {
        var dlg = new RoundTripPreviewDialog(result, editDescription) { Owner = owner };
        bool ok = dlg.ShowDialog() == true && dlg.Confirmed;
        dontAskAgain = dlg.DontAskAgain;
        return ok;
    }
}

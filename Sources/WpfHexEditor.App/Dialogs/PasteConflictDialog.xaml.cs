// ==========================================================
// Project: WpfHexEditor.App
// File: Dialogs/PasteConflictDialog.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Themed dialog shown when a clipboard paste would overwrite an existing
//     file. The user can rename the incoming file, skip it, or cancel all
//     remaining paste operations.
//
// Architecture Notes:
//     Inherits ThemedDialog for VS2026-consistent appearance.
//     Input:  OriginalName (display) + NewName (pre-filled suggestion).
//     Output: NewName (read when DialogResult=true), CancelAll flag (Skip vs Cancel).
// ==========================================================

using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;

namespace WpfHexEditor.App.Dialogs;

/// <summary>
/// Displayed when a clipboard paste operation encounters a file with the same name
/// in the destination folder.
/// <para>
/// When <see cref="Window.DialogResult"/> is <see langword="true"/>, the caller
/// uses <see cref="NewName"/> as the destination file name.
/// When <see cref="Window.DialogResult"/> is <see langword="false"/> and
/// <see cref="CancelAll"/> is <see langword="false"/>, the caller skips the file.
/// When <see cref="CancelAll"/> is <see langword="true"/>, the caller aborts the
/// entire paste operation.
/// </para>
/// </summary>
public partial class PasteConflictDialog : WpfHexEditor.Editor.Core.Views.ThemedDialog
{
    // ── Input ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Name of the file that already exists in the destination folder.
    /// Shown in the conflict message; not editable.
    /// </summary>
    public required string OriginalName { get; init; }

    // ── Output ───────────────────────────────────────────────────────────────

    /// <summary>
    /// The file name chosen by the user. Valid only when
    /// <see cref="Window.DialogResult"/> is <see langword="true"/>.
    /// </summary>
    public string NewName { get; set; } = string.Empty;

    /// <summary>
    /// <see langword="true"/> when the user clicked "Cancel All", meaning the
    /// host should abort all remaining paste operations.
    /// </summary>
    public bool CancelAll { get; private set; }

    // ── Constructor ──────────────────────────────────────────────────────────

    public PasteConflictDialog()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            // Populate the inline Run with the conflicting file name
            OriginalNameRun.Text = OriginalName;

            // Pre-fill and select the suggested name so the user can type immediately
            NewNameBox.Text = NewName;
            NewNameBox.Focus();
            NewNameBox.SelectAll();
        };
    }

    // ── Event handlers ───────────────────────────────────────────────────────

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) TryAccept();
    }

    private void OnRename(object sender, RoutedEventArgs e) => TryAccept();

    private void OnSkip(object sender, RoutedEventArgs e)
    {
        CancelAll    = false;
        DialogResult = false;
        Close();
    }

    private void OnCancelAll(object sender, RoutedEventArgs e)
    {
        CancelAll    = true;
        DialogResult = false;
        Close();
    }

    // ── Validation ───────────────────────────────────────────────────────────

    private void TryAccept()
    {
        var name = NewNameBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError("Please enter a file name.");
            return;
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            ShowError("The name contains invalid characters.");
            return;
        }

        NewName      = name;
        DialogResult = true;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorText.Text       = message;
        ErrorText.Visibility = Visibility.Visible;
        NewNameBox.Focus();
        NewNameBox.SelectAll();
    }
}

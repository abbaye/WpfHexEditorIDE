// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: Dialogs/IDialogService.cs
// Description:
//     SDK-level contract for showing themed modal dialogs.
//     Plugins obtain this via IIDEHostContext.Dialogs.
//
// Architecture Notes:
//     Mirrors System.Windows.MessageBox API surface so call-sites
//     can migrate with minimal changes.  The async overload is
//     preferred from ViewModels to keep UI thread unblocked until
//     the dialog needs to appear.
// ==========================================================

using System.Windows;

namespace WpfHexEditor.Editor.Core.Dialogs;

/// <summary>
/// IDE-wide service for showing themed, host-integrated modal dialogs.
/// Accessible to plugins via <c>IIDEHostContext.Dialogs</c>.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows a themed modal message dialog and returns the user's choice.
    /// Must be called on the UI thread; use <see cref="ShowAsync"/> from background tasks.
    /// </summary>
    /// <param name="message">Body text displayed in the dialog.</param>
    /// <param name="title">Window caption.</param>
    /// <param name="button">Which button(s) to display.</param>
    /// <param name="icon">Icon severity shown left of the message.</param>
    /// <param name="owner">
    /// Optional WPF owner window.  When <c>null</c> the IDE main window is used.
    /// </param>
    MessageBoxResult Show(
        string           message,
        string           title  = "",
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage  icon   = MessageBoxImage.None,
        Window?          owner  = null);

    /// <summary>
    /// Async version — marshals to the UI thread automatically.
    /// Safe to call from background tasks or async ViewModels.
    /// </summary>
    Task<MessageBoxResult> ShowAsync(
        string           message,
        string           title  = "",
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage  icon   = MessageBoxImage.None,
        Window?          owner  = null);
}

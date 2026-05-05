// ==========================================================
// Project: WpfHexEditor.App
// File: Services/DialogServiceImpl.cs
// Description:
//     Concrete implementation of IDialogService.
//     Delegates to IdeMessageBox (themed WPF dialog).
//
// Architecture Notes:
//     Registered as a singleton in AppServiceCollection and
//     exposed to plugins via IDEHostContextImpl.Dialogs.
// ==========================================================

using System.Windows;
using WpfHexEditor.Editor.Core.Dialogs;

namespace WpfHexEditor.App.Services;

internal sealed class DialogServiceImpl : IDialogService
{
    public MessageBoxResult Show(
        string           message,
        string           title  = "",
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage  icon   = MessageBoxImage.None,
        Window?          owner  = null)
        => IdeMessageBox.Show(message, title, button, icon, owner);

    public Task<MessageBoxResult> ShowAsync(
        string           message,
        string           title  = "",
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage  icon   = MessageBoxImage.None,
        Window?          owner  = null)
        => IdeMessageBox.ShowAsync(message, title, button, icon, owner);
}

//////////////////////////////////////////////
// Project      : WpfHexEditor.App
// File         : MainWindow.CommandPalette.cs
// Description  : Command Palette handler (Ctrl+Shift+P).
//                Builds the command catalog from the central CommandRegistry
//                (all built-in IDE commands) + plugin-contributed menu items,
//                then opens the CommandPaletteWindow overlay.
// Architecture : Partial class of MainWindow. Depends on _commandRegistry and
//                _keyBindingService (initialised by InitCommands in MainWindow.Commands.cs).
//////////////////////////////////////////////

using WpfHexEditor.App.Dialogs;
using WpfHexEditor.App.Models;
using WpfHexEditor.App.Services;

namespace WpfHexEditor.App;

public partial class MainWindow
{
    // -----------------------------------------------------------------------
    // Handler wired in MainWindow.xaml CommandBindings
    // -----------------------------------------------------------------------

    private void OnShowCommandPalette(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
        // 1. All built-in IDE commands from the central registry — gesture from KeyBindingService.
        var entries = _commandRegistry
            .GetAll()
            .Select(cmd => new CommandPaletteEntry(
                Name:        cmd.Name,
                Category:    cmd.Category,
                GestureText: _keyBindingService.ResolveGesture(cmd.Id),
                IconGlyph:   cmd.IconGlyph,
                Command:     cmd.Command))
            .ToList();

        // 2. Plugin-contributed menu items (registered via IMenuAdapter — not yet in CommandRegistry).
        if (_menuAdapter is not null)
        {
            foreach (var (uiId, descriptor) in _menuAdapter.GetAllMenuItems())
            {
                if (descriptor.Command is null) continue;

                var category = string.IsNullOrWhiteSpace(descriptor.ParentPath)
                    ? "Plugins"
                    : descriptor.ParentPath.TrimStart('_');

                entries.Add(new CommandPaletteEntry(
                    Name:             descriptor.Header?.ToString() ?? uiId,
                    Category:         category,
                    GestureText:      descriptor.GestureText,
                    IconGlyph:        descriptor.IconGlyph,
                    Command:          descriptor.Command,
                    CommandParameter: descriptor.CommandParameter));
            }
        }

        var service = new CommandPaletteService(entries);

        // Anchor palette flush below the title bar launcher button (if available)
        System.Windows.Point? anchor = null;
        if (TitleBarSearchButton is { IsLoaded: true })
            anchor = TitleBarSearchButton.PointToScreen(
                new System.Windows.Point(
                    TitleBarSearchButton.ActualWidth  / 2,
                    TitleBarSearchButton.ActualHeight));

        new CommandPaletteWindow(service, this, anchor).Show();
    }
}

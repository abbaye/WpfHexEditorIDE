// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Services/EditorCommandAdapter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Registers WPF command bindings on a CodeEditor host element so that
//     standard editor commands (Undo, Redo, Save, Find, SelectAll …)
//     publish IDE-level events on IIDEEventBus when executed.
//     Also provides a lightweight programmatic dispatch path used by
//     plugins and IDE panels to trigger editor actions by name.
//
// Architecture Notes:
//     Pattern: Adapter / Command Bridge
//     - Binds WPF ApplicationCommands and custom RoutedCommands to handler
//       delegates supplied by the caller (CodeEditor control).
//     - Decouples command routing from the CodeEditor control so the bus
//       can be changed without touching CodeEditor rendering code.
//     - No DI container; caller passes all dependencies explicitly.
// ==========================================================

using System.Windows;
using System.Windows.Input;
using WpfHexEditor.Events;
using WpfHexEditor.Events.IDEEvents;

namespace WpfHexEditor.Editor.CodeEditor.Services;

/// <summary>
/// Bridges WPF <see cref="RoutedCommand"/> bindings to CodeEditor actions
/// and publishes command execution events on <see cref="IIDEEventBus"/>.
/// </summary>
public sealed class EditorCommandAdapter : IDisposable
{
    // -----------------------------------------------------------------------
    // Custom RoutedCommands
    // -----------------------------------------------------------------------

    /// <summary>Opens the Find/Replace bar within the code editor pane.</summary>
    public static readonly RoutedCommand FindCommand    = new("Find",    typeof(EditorCommandAdapter),
        new InputGestureCollection { new KeyGesture(Key.F, ModifierKeys.Control) });

    /// <summary>Opens the Find/Replace bar in Replace mode.</summary>
    public static readonly RoutedCommand ReplaceCommand = new("Replace", typeof(EditorCommandAdapter),
        new InputGestureCollection { new KeyGesture(Key.H, ModifierKeys.Control) });

    /// <summary>Folds / collapses all regions.</summary>
    public static readonly RoutedCommand CollapseAllCommand = new("CollapseAll", typeof(EditorCommandAdapter));

    /// <summary>Unfolds / expands all regions.</summary>
    public static readonly RoutedCommand ExpandAllCommand   = new("ExpandAll",   typeof(EditorCommandAdapter));

    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------

    private readonly UIElement         _host;
    private readonly IIDEEventBus      _bus;
    private readonly string            _filePath;
    private readonly List<CommandBinding> _bindings = [];
    private          bool              _disposed;

    // -----------------------------------------------------------------------
    // Construction
    // -----------------------------------------------------------------------

    /// <summary>
    /// Registers command bindings on <paramref name="host"/> and connects them
    /// to <paramref name="actions"/>. Call <see cref="Dispose"/> when the tab closes.
    /// </summary>
    /// <param name="host">
    ///   The WPF element that should own the command bindings (typically the
    ///   <c>CodeEditor</c> control or its containing <c>DockItem</c> content pane).
    /// </param>
    /// <param name="eventBus">Bus to publish execution events on.</param>
    /// <param name="filePath">Absolute file path — included in event payloads.</param>
    /// <param name="actions">Editor action delegates keyed by command name.</param>
    public EditorCommandAdapter(
        UIElement                       host,
        IIDEEventBus                    eventBus,
        string                          filePath,
        IReadOnlyDictionary<string, Action> actions)
    {
        _host     = host     ?? throw new ArgumentNullException(nameof(host));
        _bus      = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _filePath = filePath ?? string.Empty;

        RegisterBinding(ApplicationCommands.Undo,       actions, "undo");
        RegisterBinding(ApplicationCommands.Redo,       actions, "redo");
        RegisterBinding(ApplicationCommands.Copy,       actions, "copy");
        RegisterBinding(ApplicationCommands.Cut,        actions, "cut");
        RegisterBinding(ApplicationCommands.Paste,      actions, "paste");
        RegisterBinding(ApplicationCommands.SelectAll,  actions, "selectAll");
        RegisterBinding(ApplicationCommands.Save,       actions, "save");
        RegisterBinding(FindCommand,                    actions, "find");
        RegisterBinding(ReplaceCommand,                 actions, "replace");
        RegisterBinding(CollapseAllCommand,             actions, "collapseAll");
        RegisterBinding(ExpandAllCommand,               actions, "expandAll");
    }

    // -----------------------------------------------------------------------
    // Programmatic dispatch
    // -----------------------------------------------------------------------

    /// <summary>
    /// Executes the named command programmatically (e.g. from a plugin or IDE panel).
    /// Returns <c>false</c> when no binding for <paramref name="commandName"/> exists.
    /// </summary>
    public bool Execute(string commandName)
    {
        var binding = _bindings.FirstOrDefault(
            b => b.Command is RoutedCommand rc &&
                 rc.Name.Equals(commandName, StringComparison.OrdinalIgnoreCase));

        if (binding is null) return false;

        if (binding.Command is ICommand cmd && cmd.CanExecute(null))
        {
            cmd.Execute(null);
            return true;
        }
        return false;
    }

    // -----------------------------------------------------------------------
    // IDisposable
    // -----------------------------------------------------------------------

    /// <summary>Removes all registered command bindings from the host element.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var binding in _bindings)
            _host.CommandBindings.Remove(binding);

        _bindings.Clear();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private void RegisterBinding(
        ICommand                        command,
        IReadOnlyDictionary<string, Action> actions,
        string                          actionKey)
    {
        var commandName = command is RoutedCommand rc ? rc.Name : actionKey;
        Action? action  = actions.TryGetValue(actionKey, out var a) ? a : null;

        var binding = new CommandBinding(
            command,
            executed: (_, _) =>
            {
                action?.Invoke();
                _bus.Publish(new CodeEditorCommandExecutedEvent
                {
                    FilePath    = _filePath,
                    CommandName = commandName,
                });
            },
            canExecute: (_, e) => { e.CanExecute = true; e.Handled = true; });

        _bindings.Add(binding);
        _host.CommandBindings.Add(binding);
    }
}

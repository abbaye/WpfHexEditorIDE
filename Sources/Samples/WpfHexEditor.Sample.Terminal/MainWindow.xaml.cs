// ==========================================================
// Project: WpfHexEditor.Sample.Terminal
// File: MainWindow.xaml.cs
// Author: Auto
// Created: 2026-03-08
// Description:
//     Code-behind for the main window. Wires the TerminalPanelViewModel
//     to the view, handles theme switching, shell-mode menu sync, and
//     delegates terminal commands (clear, save, mode) to the VM.
//
// Architecture Notes:
//     Pattern: MVVM code-behind adapter — all business logic in VM.
//     StandaloneIDEHostContext provides no-op SDK services so the VM
//     constructor does not throw; HxTerminal built-in commands degrade
//     gracefully for IDE-specific operations (e.g. "no active document").
//     Theme switching: App.SwitchTheme() swaps the merged ResourceDictionary;
//     all DynamicResource bindings update automatically.
// ==========================================================

using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using WpfHexEditor.Core.Terminal.ShellSession;
using WpfHexEditor.Terminal;

namespace WpfHexEditor.Sample.Terminal;

public partial class MainWindow : Window
{
    // -- Theme URIs ---------------------------------------------------------------

    private static readonly Uri DarkThemeUri  = new("pack://application:,,,/WpfHexEditor.Shell;component/Themes/DarkTheme.xaml");
    private static readonly Uri LightThemeUri = new("pack://application:,,,/WpfHexEditor.Shell;component/Themes/OfficeTheme.xaml");

    // -- ViewModel ----------------------------------------------------------------

    private readonly TerminalPanelViewModel _viewModel = new(new StandaloneIDEHostContext());

    // -- Constructor --------------------------------------------------------------

    public MainWindow()
    {
        // DataContext must be set BEFORE InitializeComponent() so that
        // TerminalPanel's DataContextChanged fires with the VM already populated
        // (Sessions already contains the initial HxTerminal tab created in the
        // VM constructor). Setting it after InitializeComponent or in
        // ContentRendered causes the panel to render once without a DataContext,
        // leaving the tab strip empty on first paint.
        DataContext = _viewModel;

        InitializeComponent();

        // Subscribe to VM property changes to keep status bar and menu in sync.
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Sync initial state.
        SyncShellModeMenu();
    }

    // -- Lifecycle ----------------------------------------------------------------

    /// <summary>Disposes the VM (kills any external shell processes) on close.</summary>
    private void OnClosed(object sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.Dispose();
    }

    // -- VM property change -------------------------------------------------------

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TerminalPanelViewModel.CurrentModeLabel))
            SyncShellModeMenu();
    }

    /// <summary>
    /// Keeps the Terminal menu IsChecked states and the status bar shell label
    /// in sync with the active session's shell type.
    /// </summary>
    private void SyncShellModeMenu()
    {
        var shellType = _viewModel.ActiveSession?.Session.ShellType
                        ?? TerminalShellType.HxTerminal;

        MenuHxTerminal.IsChecked = shellType == TerminalShellType.HxTerminal;
        MenuPowerShell.IsChecked = shellType == TerminalShellType.PowerShell;
        MenuBash.IsChecked       = shellType == TerminalShellType.Bash;

        StatusShellLabel.Text = $"\u25cf {_viewModel.CurrentModeLabel}";
    }

    // -- File menu ----------------------------------------------------------------

    private void OnExit(object sender, RoutedEventArgs e) => Close();

    // -- View > Theme -------------------------------------------------------------

    private void OnThemeDark(object sender, RoutedEventArgs e)  => SetTheme(dark: true);
    private void OnThemeLight(object sender, RoutedEventArgs e) => SetTheme(dark: false);

    private void SetTheme(bool dark)
    {
        App.SwitchTheme(dark ? DarkThemeUri : LightThemeUri);
        MenuDarkTheme.IsChecked  =  dark;
        MenuLightTheme.IsChecked = !dark;
        StatusThemeLabel.Text    =  dark ? "Theme: Dark" : "Theme: Light";
    }

    // -- Terminal menu ------------------------------------------------------------

    /// <summary>Clears the active session's output.</summary>
    private void OnClearOutput(object sender, RoutedEventArgs e)
        => _viewModel.ClearOutputCommand.Execute(null);

    /// <summary>Opens a new HxTerminal session tab and makes it active.</summary>
    private void OnModeHxTerminal(object sender, RoutedEventArgs e)
        => _viewModel.AddHxTerminalCommand.Execute(null);

    /// <summary>Opens a new PowerShell session tab and makes it active.</summary>
    private void OnModePowerShell(object sender, RoutedEventArgs e)
        => _viewModel.AddPowerShellCommand.Execute(null);

    /// <summary>Opens a new Bash session tab and makes it active.</summary>
    private void OnModeBash(object sender, RoutedEventArgs e)
        => _viewModel.AddBashCommand.Execute(null);

    /// <summary>Saves the active session's output via the VM's built-in save dialog.</summary>
    private void OnSaveOutput(object sender, RoutedEventArgs e)
        => _viewModel.SaveOutputCommand.Execute(null);

    // -- Help menu ----------------------------------------------------------------

    private void OnAbout(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "WpfHexEditor — Terminal Sample\n\n" +
            "Standalone showcase of the WpfHexEditor terminal panel.\n\n" +
            "Features:\n" +
            "  • 40 built-in HxTerminal commands (type 'help')\n" +
            "  • External shells: PowerShell, Bash\n" +
            "  • Macro recording and .hxscript replay\n" +
            "  • Multi-tab sessions\n" +
            "  • Dark / Light runtime theme switching\n\n" +
            "Apache 2.0 — WpfHexEditorControl",
            "About",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    // -- Keyboard shortcuts -------------------------------------------------------

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        // Ctrl+L → clear output (mirrors VS Code terminal shortcut).
        if (e.Key == Key.L && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            _viewModel.ClearOutputCommand.Execute(null);
            e.Handled = true;
        }

        base.OnPreviewKeyDown(e);
    }
}

// ==========================================================
// Project: WpfHexEditor.Sample.CodeEditor
// File: MainWindow.xaml.cs
// Author: Auto
// Created: 2026-03-18
// Description:
//     Code-behind for the main window. Wires the CodeEditorSplitHost
//     to the MainViewModel, handles file operations (New/Open/Save/SaveAs),
//     theme switching, language selection, toolbar actions, and
//     editor event forwarding to the status bar.
//
// Architecture Notes:
//     Pattern: MVVM code-behind adapter — thin bridge between the editor
//     control events and the ViewModel. No business logic here.
//     Theme switching: App.SwitchTheme(name) swaps the merged ResourceDictionary;
//     all DynamicResource bindings update automatically.
//     Language loading: LoadEmbeddedLanguageDefinitions() must run before
//     any file is opened to populate LanguageRegistry with built-in definitions.
// ==========================================================

using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Core.Definitions;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Core.ProjectSystem.Languages;
using WpfHexEditor.Sample.CodeEditor.ViewModels;

namespace WpfHexEditor.Sample.CodeEditor;

public partial class MainWindow : Window
{
    // -- ViewModel & state --------------------------------------------------------

    private readonly MainViewModel _vm = new();
    private bool _languageComboSuppressEvent;

    // -- Routed commands (bound to Window InputBindings in XAML) ------------------

    public static readonly RoutedCommand NewCommand      = new();
    public static readonly RoutedCommand OpenCommand     = new();
    public static readonly RoutedCommand SaveCommand     = new();
    public static readonly RoutedCommand SaveAsCommand   = new();
    public static readonly RoutedCommand DarkThemeCommand  = new();
    public static readonly RoutedCommand LightThemeCommand = new();
    public static readonly RoutedCommand ZoomInCommand   = new();
    public static readonly RoutedCommand ZoomOutCommand  = new();
    public static readonly RoutedCommand ZoomResetCommand = new();

    // -- Constructor --------------------------------------------------------------

    public MainWindow()
    {
        DataContext = _vm;
        InitializeComponent();

        // Wire routed commands so KeyBindings in XAML work.
        CommandBindings.Add(new CommandBinding(NewCommand,      (_, _) => DoNew()));
        CommandBindings.Add(new CommandBinding(OpenCommand,     (_, _) => DoOpen()));
        CommandBindings.Add(new CommandBinding(SaveCommand,     (_, _) => DoSave()));
        CommandBindings.Add(new CommandBinding(SaveAsCommand,   (_, _) => DoSaveAs()));
        CommandBindings.Add(new CommandBinding(DarkThemeCommand,  (_, _) => ApplyTheme("DarkTheme")));
        CommandBindings.Add(new CommandBinding(LightThemeCommand, (_, _) => ApplyTheme("OfficeTheme")));
        CommandBindings.Add(new CommandBinding(ZoomInCommand,   (_, _) => ZoomBy(+0.1)));
        CommandBindings.Add(new CommandBinding(ZoomOutCommand,  (_, _) => ZoomBy(-0.1)));
        CommandBindings.Add(new CommandBinding(ZoomResetCommand,(_, _) => SetZoom(1.0)));
    }

    // -- Lifecycle ----------------------------------------------------------------

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 1. Register all embedded .whlang definitions so language auto-detection works.
        LoadEmbeddedLanguageDefinitions();

        // 2. Populate the Language ComboBox and menu from LanguageRegistry.
        PopulateLanguagePicker();

        // 3. Wire editor events to status bar.
        Editor.ModifiedChanged  += OnEditorModifiedChanged;
        Editor.TitleChanged     += OnEditorTitleChanged;
        Editor.PrimaryEditor.CaretMoved += OnEditorCaretMoved;

        // ZoomLevel: subscribe via DependencyPropertyDescriptor for live updates.
        var zpd = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
            WpfHexEditor.Editor.CodeEditor.Controls.CodeEditor.ZoomLevelProperty,
            typeof(WpfHexEditor.Editor.CodeEditor.Controls.CodeEditor));
        zpd.AddValueChanged(Editor.PrimaryEditor, OnEditorZoomChanged);

        // Initial status bar sync.
        RefreshPositionStatus();
        RefreshZoomStatus();
    }

    private void OnClosed(object sender, EventArgs e)
    {
        Editor.ModifiedChanged  -= OnEditorModifiedChanged;
        Editor.TitleChanged     -= OnEditorTitleChanged;
        Editor.PrimaryEditor.CaretMoved -= OnEditorCaretMoved;
    }

    // -- Language definitions bootstrap -------------------------------------------

    /// <summary>
    /// Loads all embedded language definitions (from .whfmt syntaxDefinition blocks) into LanguageRegistry.
    /// Must be called once before any file is opened.
    /// </summary>
    private static void LoadEmbeddedLanguageDefinitions()
    {
        var formatCatalog = EmbeddedFormatCatalog.Instance;
        var registry      = LanguageRegistry.Instance;

        foreach (var entry in formatCatalog.GetAll())
        {
            if (!entry.HasSyntaxDefinition) continue;
            try
            {
                var syntaxJson = formatCatalog.GetSyntaxDefinitionJson(entry.ResourceKey);
                if (syntaxJson is null) continue;

                var def = LanguageDefinitionSerializer.ParseSyntaxDefinitionBlock(
                    syntaxJson,
                    entry.Name,
                    entry.Extensions,
                    entry.PreferredEditor);
                registry.RegisterBuiltin(def);
            }
            catch
            {
                // Skip malformed or incomplete syntaxDefinition blocks.
            }
        }

        registry.ResolveIncludes();
    }

    // -- Language picker ----------------------------------------------------------

    private void PopulateLanguagePicker()
    {
        var languages = LanguageRegistry.Instance
            .AllLanguages()
            .OrderBy(l => l.Name)
            .ToList();

        _languageComboSuppressEvent = true;

        // Plain Text entry (no syntax highlighting)
        LanguageCombo.Items.Clear();
        LanguageCombo.Items.Add(new ComboBoxItem { Content = "Plain Text", Tag = null });

        // Registered languages
        foreach (var lang in languages)
            LanguageCombo.Items.Add(new ComboBoxItem { Content = lang.Name, Tag = lang });

        LanguageCombo.SelectedIndex = 0;
        _languageComboSuppressEvent = false;

        // Also populate the Language menu
        MenuLanguage.Items.Clear();
        var plainTextItem = new MenuItem
        {
            Header      = "_Plain Text",
            IsCheckable = true,
            IsChecked   = true,
            Tag         = (LanguageDefinition?)null
        };
        plainTextItem.Click += OnMenuLanguageItem;
        MenuLanguage.Items.Add(plainTextItem);
        MenuLanguage.Items.Add(new Separator());

        foreach (var lang in languages)
        {
            var item = new MenuItem
            {
                Header      = lang.Name,
                IsCheckable = true,
                IsChecked   = false,
                Tag         = lang
            };
            item.Click += OnMenuLanguageItem;
            MenuLanguage.Items.Add(item);
        }
    }

    private void OnMenuLanguageItem(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item) return;
        var lang = item.Tag as LanguageDefinition;
        ApplyLanguage(lang);

        // Sync checkmarks — uncheck all, check selected
        foreach (var mi in MenuLanguage.Items.OfType<MenuItem>())
            mi.IsChecked = mi == item;
    }

    private void OnLanguageComboChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_languageComboSuppressEvent) return;
        if (LanguageCombo.SelectedItem is not ComboBoxItem item) return;
        ApplyLanguage(item.Tag as LanguageDefinition);
    }

    private void ApplyLanguage(LanguageDefinition? lang)
    {
        Editor.SetLanguage(lang);
        _vm.LanguageName = lang?.Name ?? "Plain Text";
        StatusLanguage.Text = _vm.LanguageName;

        // Sync combo selection
        _languageComboSuppressEvent = true;
        foreach (ComboBoxItem ci in LanguageCombo.Items)
        {
            if (ci.Tag == lang)
            {
                LanguageCombo.SelectedItem = ci;
                break;
            }
        }
        _languageComboSuppressEvent = false;
    }

    // -- File operations ----------------------------------------------------------

    private void DoNew()
    {
        if (!ConfirmDiscardChanges()) return;
        Editor.PrimaryEditor.LoadText(string.Empty);
        _vm.NotifyNewFile();
        StatusLanguage.Text = "Plain Text";
        _languageComboSuppressEvent = true;
        if (LanguageCombo.Items.Count > 0) LanguageCombo.SelectedIndex = 0;
        _languageComboSuppressEvent = false;
    }

    private async void DoOpen()
    {
        if (!ConfirmDiscardChanges()) return;

        var dlg = new OpenFileDialog
        {
            Title  = "Open File",
            Filter = "All files (*.*)|*.*|" +
                     "C# files (*.cs)|*.cs|" +
                     "JSON files (*.json)|*.json|" +
                     "XML files (*.xml)|*.xml|" +
                     "Text files (*.txt)|*.txt"
        };

        if (dlg.ShowDialog(this) != true) return;

        await ((IOpenableDocument)Editor).OpenAsync(dlg.FileName);
        _vm.NotifyFileOpened(dlg.FileName);

        // Sync language display from auto-detected language
        var lang = LanguageRegistry.Instance.GetLanguageForFile(dlg.FileName);
        _vm.LanguageName    = lang?.Name ?? "Plain Text";
        StatusLanguage.Text = _vm.LanguageName;

        _languageComboSuppressEvent = true;
        foreach (ComboBoxItem ci in LanguageCombo.Items)
        {
            if (ci.Tag == lang) { LanguageCombo.SelectedItem = ci; break; }
        }
        if (lang is null) LanguageCombo.SelectedIndex = 0;
        _languageComboSuppressEvent = false;
    }

    private async void DoSave()
    {
        if (_vm.CurrentFilePath is null)
        {
            DoSaveAs();
            return;
        }
        await Editor.SaveAsync();
    }

    private async void DoSaveAs()
    {
        var dlg = new SaveFileDialog
        {
            Title      = "Save File As",
            FileName   = _vm.CurrentFilePath is not null
                         ? Path.GetFileName(_vm.CurrentFilePath)
                         : "Untitled.txt",
            Filter     = "All files (*.*)|*.*|Text files (*.txt)|*.txt"
        };

        if (dlg.ShowDialog(this) != true) return;
        await Editor.SaveAsAsync(dlg.FileName);
        _vm.NotifyFileOpened(dlg.FileName);
    }

    /// <summary>
    /// Prompts the user to save unsaved changes. Returns true if the operation
    /// should proceed (saved or discarded), false if cancelled.
    /// </summary>
    private bool ConfirmDiscardChanges()
    {
        if (!Editor.IsDirty) return true;

        var result = MessageBox.Show(
            "The current file has unsaved changes. Do you want to save them?",
            "Unsaved Changes",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Cancel) return false;
        if (result == MessageBoxResult.Yes) { DoSave(); }
        return true;
    }

    // -- Theme --------------------------------------------------------------------

    private void ApplyTheme(string themeName)
    {
        App.SwitchTheme(themeName);
        _vm.CurrentThemeName       = themeName;
        StatusThemeLabel.Text      = $"Theme: {_vm.ThemeLabel}";

        // Sync menu checkmarks
        SyncThemeMenuChecks(themeName);
    }

    private void SyncThemeMenuChecks(string active)
    {
        MenuThemeDark.IsChecked      = active == "DarkTheme";
        MenuThemeLight.IsChecked     = active == "OfficeTheme";
        MenuThemeVS2022Dark.IsChecked= active == "VS2022DarkTheme";
        MenuThemeMinimal.IsChecked   = active == "MinimalTheme";
        MenuThemeCyberpunk.IsChecked = active == "CyberpunkTheme";
    }

    // -- Zoom ---------------------------------------------------------------------

    private void ZoomBy(double delta)
        => SetZoom(Math.Clamp(Editor.PrimaryEditor.ZoomLevel + delta, 0.5, 2.0));

    private void SetZoom(double level)
    {
        Editor.PrimaryEditor.ZoomLevel = level;
        // Secondary editor shares the document but has its own zoom
        Editor.SecondaryEditor.ZoomLevel = level;
        RefreshZoomStatus();
    }

    private void RefreshZoomStatus()
    {
        int pct = (int)(Editor.PrimaryEditor.ZoomLevel * 100);
        _vm.ZoomLevel        = Editor.PrimaryEditor.ZoomLevel;
        StatusZoom.Text      = $"Zoom: {pct}%";
    }

    // -- Editor event handlers ----------------------------------------------------

    private void OnEditorModifiedChanged(object? sender, EventArgs e)
    {
        _vm.IsDirty       = Editor.IsDirty;
        StatusDirty.Visibility = Editor.IsDirty ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnEditorTitleChanged(object? sender, string title)
        => _vm.Title = title;

    private void OnEditorCaretMoved(object? sender, EventArgs e)
        => RefreshPositionStatus();

    private void OnEditorZoomChanged(object? sender, EventArgs e)
        => RefreshZoomStatus();

    private void RefreshPositionStatus()
    {
        var pos = Editor.PrimaryEditor.CursorPosition;
        _vm.CaretLine   = pos.Line + 1;    // TextPosition is 0-based
        _vm.CaretColumn = pos.Column + 1;
        StatusPosition.Text = $"Ln {_vm.CaretLine}, Col {_vm.CaretColumn}";
    }

    // -- Menu handlers — File -----------------------------------------------------

    private void OnMenuNew(object sender, RoutedEventArgs e)    => DoNew();
    private void OnMenuOpen(object sender, RoutedEventArgs e)   => DoOpen();
    private void OnMenuSave(object sender, RoutedEventArgs e)   => DoSave();
    private void OnMenuSaveAs(object sender, RoutedEventArgs e) => DoSaveAs();
    private void OnMenuExit(object sender, RoutedEventArgs e)   => Close();

    // -- Menu handlers — Edit -----------------------------------------------------

    private void OnMenuUndo(object sender, RoutedEventArgs e)      => Editor.Undo();
    private void OnMenuRedo(object sender, RoutedEventArgs e)      => Editor.Redo();
    private void OnMenuCut(object sender, RoutedEventArgs e)       => Editor.Cut();
    private void OnMenuCopy(object sender, RoutedEventArgs e)      => Editor.Copy();
    private void OnMenuPaste(object sender, RoutedEventArgs e)     => Editor.Paste();
    private void OnMenuSelectAll(object sender, RoutedEventArgs e) => Editor.SelectAll();

    // -- Menu handlers — View (toggles) -------------------------------------------

    private void OnMenuToggleLineNumbers(object sender, RoutedEventArgs e)
    {
        bool v = MenuShowLineNumbers.IsChecked;
        Editor.PrimaryEditor.ShowLineNumbers   = v;
        Editor.SecondaryEditor.ShowLineNumbers = v;
    }

    private void OnMenuToggleCodeFolding(object sender, RoutedEventArgs e)
    {
        bool v = MenuCodeFolding.IsChecked;
        Editor.PrimaryEditor.IsFoldingEnabled   = v;
        Editor.SecondaryEditor.IsFoldingEnabled = v;
    }

    private void OnMenuToggleInlineHints(object sender, RoutedEventArgs e)
    {
        bool v = MenuInlineHints.IsChecked;
        Editor.PrimaryEditor.ShowInlineHints   = v;
        Editor.SecondaryEditor.ShowInlineHints = v;
    }

    private void OnMenuToggleSmartComplete(object sender, RoutedEventArgs e)
    {
        bool v = MenuSmartComplete.IsChecked;
        Editor.PrimaryEditor.EnableSmartComplete   = v;
        Editor.SecondaryEditor.EnableSmartComplete = v;
    }

    private void OnMenuToggleWordHighlight(object sender, RoutedEventArgs e)
    {
        bool v = MenuWordHighlight.IsChecked;
        Editor.PrimaryEditor.EnableWordHighlight   = v;
        Editor.SecondaryEditor.EnableWordHighlight = v;
    }

    // -- Menu handlers — View (zoom) ----------------------------------------------

    private void OnMenuZoomIn(object sender, RoutedEventArgs e)    => ZoomBy(+0.1);
    private void OnMenuZoomOut(object sender, RoutedEventArgs e)   => ZoomBy(-0.1);
    private void OnMenuZoomReset(object sender, RoutedEventArgs e) => SetZoom(1.0);

    // -- Menu handlers — View (theme) ---------------------------------------------

    private void OnMenuTheme(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || item.Tag is not string themeName) return;
        ApplyTheme(themeName);
    }

    // -- Menu handlers — Find -----------------------------------------------------

    private void OnMenuFind(object sender, RoutedEventArgs e)
        => Editor.ShowSearch();

    private void OnMenuFindReplace(object sender, RoutedEventArgs e)
    {
        // Show the inline search bar then expand its replace section.
        Editor.ShowSearchAndReplace();
    }

    // -- Menu handlers — Help -----------------------------------------------------

    private void OnMenuAbout(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "WpfHexEditor — CodeEditor Sample\n\n" +
            "Standalone showcase of the WpfHexEditor code editor.\n\n" +
            "Features:\n" +
            "  • Syntax highlighting (30+ languages)\n" +
            "  • Code folding, InlineHints, SmartComplete\n" +
            "  • Find & Replace (Ctrl+F / Ctrl+H)\n" +
            "  • Split view (toggle in navigation bar)\n" +
            "  • Dark / Light runtime theme switching (F1 / F2)\n" +
            "  • Zoom (Ctrl+± / Ctrl+0)\n\n" +
            "Apache 2.0 — WpfHexEditorControl",
            "About",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    // -- Toolbar handlers — Theme toggle buttons ----------------------------------

    private void OnTbtnDark(object sender, RoutedEventArgs e)
        => ApplyTheme("DarkTheme");

    private void OnTbtnLight(object sender, RoutedEventArgs e)
        => ApplyTheme("OfficeTheme");
}

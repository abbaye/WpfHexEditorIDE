// Project      : WpfHexEditor.App
// File         : Dialogs/GenerateParserDialog.xaml.cs
// Description  : Dialog that generates a strongly-typed parser class from a .whfmt
//                definition. Supports C# and C# Span at IDE runtime; F#/Rust/VB
//                show a "use whfmt-codegen CLI" notice.
// Architecture : Calls WhfmtAppCodeGenerator (linked Generator source files) — no
//                dependency on whfmt.CodeGen as a project reference.

using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WpfHexEditor.App.Codegen;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.Core.Dialogs;
using WhfmtCodeGen.Generator;

namespace WpfHexEditor.App.Dialogs;

public partial class GenerateParserDialog : Window
{
    // ── Input ────────────────────────────────────────────────────────────────
    private readonly string _whfmtJson;
    private readonly string _whfmtFilePath;
    private readonly IProject? _project;
    private readonly ISolutionManager? _solutionManager;

    // ── State ────────────────────────────────────────────────────────────────
    private string? _generatedSource;

    private static readonly (string Display, OutputLanguage Lang, string Extension)[] _languages =
    [
        ("C#",       OutputLanguage.CSharp,       ".cs"),
        ("C# Span",  OutputLanguage.CSharpSpan,   ".cs"),
        ("F#",       OutputLanguage.FSharp,        ".fs"),
        ("Rust",     OutputLanguage.Rust,          ".rs"),
        ("VB.NET",   OutputLanguage.VisualBasic,   ".vb"),
    ];

    public GenerateParserDialog(
        string whfmtJson,
        string whfmtFilePath,
        ISolutionManager? solutionManager = null,
        IProject?         project         = null)
    {
        InitializeComponent();

        _whfmtJson       = whfmtJson;
        _whfmtFilePath   = whfmtFilePath;
        _solutionManager = solutionManager;
        _project         = project;

        FormatNameText.Text = Path.GetFileName(whfmtFilePath);

        foreach (var (display, _, _) in _languages)
            LanguageCombo.Items.Add(display);

        LanguageCombo.SelectedIndex = 0;

        var formatName = Path.GetFileNameWithoutExtension(whfmtFilePath);
        NamespaceBox.Text  = "MyApp.Parsers";
        ClassNameBox.Text  = char.ToUpperInvariant(formatName[0]) + formatName[1..] + "Parser";

        AddToProjectBtn.IsEnabled = solutionManager is not null && project is not null;

        RegeneratePreview();
    }

    // ── Event handlers ───────────────────────────────────────────────────────

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e) => RegeneratePreview();
    private void OnOptionChanged(object sender, TextChangedEventArgs e)        => RegeneratePreview();

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        if (_generatedSource is not null)
            Clipboard.SetText(_generatedSource);
    }

    private void OnSaveToFile(object sender, RoutedEventArgs e)
    {
        if (_generatedSource is null) return;

        var ext = CurrentExtension();
        var dlg = new SaveFileDialog
        {
            Title           = "Save generated parser",
            Filter          = $"Source files (*{ext})|*{ext}",
            FileName        = ClassNameBox.Text.Trim() + ext,
            OverwritePrompt = true
        };
        if (dlg.ShowDialog(this) == true)
            File.WriteAllText(dlg.FileName, _generatedSource);
    }

    private async void OnAddToProject(object sender, RoutedEventArgs e)
    {
        if (_generatedSource is null || _solutionManager is null || _project is null) return;

        var ext      = CurrentExtension();
        var dir      = Path.GetDirectoryName(_whfmtFilePath) ?? string.Empty;
        var destPath = Path.Combine(dir, ClassNameBox.Text.Trim() + ".g" + ext);

        if (File.Exists(destPath))
        {
            var result = IdeMessageBox.Show(
                $"'{Path.GetFileName(destPath)}' already exists. Overwrite?",
                "Generate Parser",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;
        }

        await File.WriteAllTextAsync(destPath, _generatedSource);
        await _solutionManager.AddItemAsync(_project, destPath);
        Close();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    // ── Generation ───────────────────────────────────────────────────────────

    private void RegeneratePreview()
    {
        if (LanguageCombo.SelectedIndex < 0) return;

        var (_, lang, _) = _languages[LanguageCombo.SelectedIndex];
        var isSupported   = WhfmtAppCodeGenerator.IsSupported(lang);

        UnsupportedNotice.Visibility = isSupported ? Visibility.Collapsed : Visibility.Visible;
        CopyBtn.IsEnabled            = isSupported;
        SaveBtn.IsEnabled            = isSupported;
        AddToProjectBtn.IsEnabled    = isSupported && _solutionManager is not null && _project is not null;

        if (!isSupported)
        {
            UnsupportedText.Text = $"{lang} generation is not available in the IDE. " +
                                   $"Use the whfmt-codegen CLI:  " +
                                   $"whfmt-codegen generate --lang {lang.ToString().ToLower()} \"{_whfmtFilePath}\"";
            PreviewEditor.LoadText(string.Empty);
            _generatedSource = null;
            return;
        }

        var ns  = NamespaceBox.Text.Trim();
        var cls = ClassNameBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(ns) || string.IsNullOrWhiteSpace(cls))
        {
            PreviewEditor.LoadText("// Enter a namespace and class name to preview the generated code.");
            _generatedSource = null;
            return;
        }

        try
        {
            _generatedSource = WhfmtAppCodeGenerator.Generate(_whfmtJson, ns, cls, lang);
            PreviewEditor.LoadText(_generatedSource);
        }
        catch (Exception ex)
        {
            _generatedSource = null;
            PreviewEditor.LoadText($"// Generation failed: {ex.Message}");
        }
    }

    private string CurrentExtension()
        => LanguageCombo.SelectedIndex >= 0
            ? _languages[LanguageCombo.SelectedIndex].Extension
            : ".cs";
}

// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Options/CodeEditorOptionsPage.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Code-behind for the Code Editor IDE options page.
//     Binds to a CodeEditorOptions instance and persists changes
//     to AppSettings on Save.
//
// Architecture Notes:
//     Pattern: Options Page
//     Registered via IOptionsPageRegistry under "Code Editor" category.
//     Theme: DockMenuBackgroundBrush / DockMenuForegroundBrush / DockBorderBrush
// ==========================================================

using System.Windows.Controls;

namespace WpfHexEditor.Editor.CodeEditor.Options;

/// <summary>
/// IDE options page for Code Editor settings.
/// Register via:
/// <c>registry.Register("Code Editor", "General", typeof(CodeEditorOptionsPage));</c>
/// </summary>
public partial class CodeEditorOptionsPage : UserControl
{
    private readonly CodeEditorOptions _options;

    public CodeEditorOptionsPage() : this(new CodeEditorOptions()) { }

    public CodeEditorOptionsPage(CodeEditorOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        InitializeComponent();
        DataContext = _options;
    }

    /// <summary>Options title shown in IDE options tree.</summary>
    public string PageTitle => "Code Editor";

    /// <summary>No-op — options are two-way bound; CodeEditor observes PropertyChanged.</summary>
    public void Apply() { }

    /// <summary>Restore defaults.</summary>
    public void Reset() { }
}

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
using WpfHexEditor.Options;

namespace WpfHexEditor.Editor.CodeEditor.Options;

/// <summary>
/// IDE options page for Code Editor settings.
/// Register via:
/// <c>registry.Register("Code Editor", "General", typeof(CodeEditorOptionsPage));</c>
/// </summary>
public partial class CodeEditorOptionsPage : UserControl, IOptionsPage
{
    private readonly CodeEditorOptions _options;

    public CodeEditorOptionsPage() : this(new CodeEditorOptions()) { }

    public CodeEditorOptionsPage(CodeEditorOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        InitializeComponent();
        DataContext = _options;
    }

    // -----------------------------------------------------------------------
    // IOptionsPage
    // -----------------------------------------------------------------------

    public string PageTitle => "Code Editor";

    /// <inheritdoc />
    public void Apply()
    {
        // Options are two-way bound — nothing extra needed.
        // Consumers (CodeEditor) observe CodeEditorOptions.PropertyChanged.
    }

    /// <inheritdoc />
    public void Reset() { /* future: restore defaults */ }
}

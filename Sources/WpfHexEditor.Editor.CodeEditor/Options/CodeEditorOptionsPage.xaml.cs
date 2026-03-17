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
//     SyntaxColorRows: observable list of SyntaxColorRow VMs, one per editable token kind.
//     ColorPicker popup is opened inline as a child Popup anchored to the Pick button.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using WpfHexEditor.ProjectSystem.Languages;
using ColorPickerControl = WpfHexEditor.ColorPicker.Controls.ColorPicker;

namespace WpfHexEditor.Editor.CodeEditor.Options;

/// <summary>
/// IDE options page for Code Editor settings.
/// Register via:
/// <c>registry.Register("Code Editor", "General", typeof(CodeEditorOptionsPage));</c>
/// </summary>
public partial class CodeEditorOptionsPage : UserControl
{
    private readonly CodeEditorOptions _options;
    private Popup?             _activePopup;
    private ColorPickerControl? _activePicker;
    private SyntaxColorRow? _activeRow;

    // Token kinds surfaced in the UI (ordered by visual grouping)
    private static readonly (SyntaxTokenKind Kind, string Label, string ResourceKey)[] TokenRows =
    [
        (SyntaxTokenKind.Keyword,    "Keyword",    "CE_Keyword"),
        (SyntaxTokenKind.Comment,    "Comment",    "CE_Comment"),
        (SyntaxTokenKind.String,     "String",     "CE_String"),
        (SyntaxTokenKind.Number,     "Number",     "CE_Number"),
        (SyntaxTokenKind.Type,       "Type",       "CE_Type"),
        (SyntaxTokenKind.Identifier, "Identifier", "CE_Identifier"),
        (SyntaxTokenKind.Operator,   "Operator",   "CE_Operator"),
    ];

    public CodeEditorOptionsPage() : this(new CodeEditorOptions()) { }

    public CodeEditorOptionsPage(CodeEditorOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        InitializeComponent();
        DataContext = _options;
        BuildSyntaxColorRows();
    }

    /// <summary>Options title shown in IDE options tree.</summary>
    public string PageTitle => "Code Editor";

    /// <summary>No-op — options are two-way bound; CodeEditor observes PropertyChanged.</summary>
    public void Apply() { }

    /// <summary>Restore defaults.</summary>
    public void Reset() { }

    // -- Syntax color rows -------------------------------------------------------

    private void BuildSyntaxColorRows()
    {
        var rows = new ObservableCollection<SyntaxColorRow>();

        foreach (var (kind, label, resourceKey) in TokenRows)
        {
            var themeColor = ResolveThemeColor(resourceKey);
            var overrideColor = _options.GetOverride(kind);
            rows.Add(new SyntaxColorRow(kind, label, overrideColor ?? themeColor, overrideColor.HasValue));
        }

        SyntaxColorRowsList.ItemsSource = rows;
    }

    /// <summary>
    /// Resolves the current theme color for <paramref name="resourceKey"/>.
    /// Falls back to gray if the key is not in the resource tree.
    /// </summary>
    private Color ResolveThemeColor(string resourceKey)
    {
        if (TryFindResource(resourceKey) is SolidColorBrush brush)
            return brush.Color;
        return Colors.Gray;
    }

    // -- Event handlers ----------------------------------------------------------

    private void OnResetColorsClick(object sender, RoutedEventArgs e)
    {
        _options.ResetAllOverrides();
        BuildSyntaxColorRows();  // refresh all swatches to theme defaults
    }

    private void OnPickColorClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not SyntaxColorRow row) return;

        // Close any previously open picker
        CloseActivePopup();

        _activeRow = row;

        _activePicker = new ColorPickerControl
        {
            SelectedColor = row.Color,
            Width  = 280,
            Height = 320,
        };
        _activePicker.ColorChanged += OnPickerColorChanged;

        _activePopup = new Popup
        {
            Child             = _activePicker,
            PlacementTarget   = btn,
            Placement         = PlacementMode.Bottom,
            StaysOpen         = false,
            AllowsTransparency= true,
            IsOpen            = true,
        };
        _activePopup.Closed += (_, _) => CloseActivePopup();
    }

    private void OnPickerColorChanged(object? sender, Color newColor)
    {
        if (_activeRow is null) return;
        _activeRow.Color = newColor;
        _options.SetOverride(_activeRow.Kind, newColor);
    }

    private void CloseActivePopup()
    {
        if (_activePicker is not null)
            _activePicker.ColorChanged -= OnPickerColorChanged;

        if (_activePopup is not null)
            _activePopup.IsOpen = false;

        _activePicker = null;
        _activePopup  = null;
        _activeRow    = null;
    }
}

// -- SyntaxColorRow view-model -----------------------------------------------

/// <summary>
/// Lightweight VM for one syntax-token color row in the options page.
/// </summary>
public sealed class SyntaxColorRow : INotifyPropertyChanged
{
    private Color _color;
    private bool  _isOverridden;

    public SyntaxColorRow(SyntaxTokenKind kind, string label, Color color, bool isOverridden)
    {
        Kind        = kind;
        Label       = label;
        _color      = color;
        _isOverridden = isOverridden;
    }

    public SyntaxTokenKind Kind  { get; }
    public string          Label { get; }

    public Color Color
    {
        get => _color;
        set
        {
            _color      = value;
            _isOverridden = true;
            Notify();
            Notify(nameof(SwatchBrush));
            Notify(nameof(HexText));
        }
    }

    /// <summary>Brush used by the XAML swatch <see cref="System.Windows.Controls.Border"/>.</summary>
    public SolidColorBrush SwatchBrush => new(_color);

    /// <summary>Hex representation shown next to the swatch.</summary>
    public string HexText => $"#{_color.R:X2}{_color.G:X2}{_color.B:X2}";

    public bool IsOverridden => _isOverridden;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

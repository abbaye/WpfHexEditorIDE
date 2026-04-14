//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: Controls/ExpressionTextBox.cs
// Description: TextBox with inline prefix coloring (var:/calc:/offset:)
//              and lightweight syntax validation.
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace WpfHexEditor.Editor.StructureEditor.Controls;

/// <summary>
/// A <see cref="TextBox"/> extension that highlights recognized expression prefixes
/// (<c>var:</c>, <c>calc:</c>, <c>offset:</c>) and validates their syntax inline.
/// Implemented as a <see cref="Grid"/> containing a <see cref="TextBox"/> and an
/// adorner-free underline indicator using a bottom <see cref="Border"/>.
/// </summary>
internal sealed class ExpressionTextBox : Grid
{
    // ── Public API ────────────────────────────────────────────────────────────

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(ExpressionTextBox),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(ExpressionTextBox),
            new PropertyMetadata("", (d, _) => ((ExpressionTextBox)d).UpdatePlaceholder()));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public event EventHandler? TextChanged;

    // ── Controls ──────────────────────────────────────────────────────────────

    private readonly TextBox   _box;
    private readonly TextBlock _placeholder;
    private readonly Border    _errorBar;

    private bool _suppressCallback;

    // ── Constructor ───────────────────────────────────────────────────────────

    internal ExpressionTextBox()
    {
        _placeholder = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible  = false,
            Margin            = new Thickness(4, 0, 4, 0),
            FontSize          = 11,
        };
        _placeholder.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");

        _box = new TextBox
        {
            Background       = Brushes.Transparent,
            BorderThickness  = new Thickness(0),
            Padding          = new Thickness(4, 2, 4, 2),
            FontSize         = 11,
            FontFamily       = new FontFamily("Consolas, Courier New"),
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        _box.SetResourceReference(TextBox.ForegroundProperty, "DockMenuForegroundBrush");
        _box.TextChanged += OnBoxTextChanged;
        _box.GotFocus    += (_, _) => UpdatePlaceholder();
        _box.LostFocus   += (_, _) => UpdatePlaceholder();

        _errorBar = new Border
        {
            Height          = 2,
            VerticalAlignment = VerticalAlignment.Bottom,
            Background      = Brushes.Red,
            Visibility      = Visibility.Collapsed,
            Margin          = new Thickness(2, 0, 2, 0),
        };

        // Outer border for theme-consistent look
        var outer = new Border { BorderThickness = new Thickness(1) };
        outer.SetResourceReference(Border.BorderBrushProperty, "DockBorderBrush");
        outer.SetResourceReference(Border.BackgroundProperty,  "DockMenuBackgroundBrush");

        var inner = new Grid();
        inner.Children.Add(_placeholder);
        inner.Children.Add(_box);
        inner.Children.Add(_errorBar);

        outer.Child = inner;
        Children.Add(outer);
    }

    // ── Callbacks ─────────────────────────────────────────────────────────────

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (ExpressionTextBox)d;
        if (self._suppressCallback) return;
        self._suppressCallback = true;
        self._box.Text = (string)e.NewValue;
        self._suppressCallback = false;
        self.Validate();
        self.UpdatePlaceholder();
    }

    private void OnBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressCallback) return;
        _suppressCallback = true;
        Text = _box.Text;
        _suppressCallback = false;
        Validate();
        UpdatePlaceholder();
        TextChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private static readonly string[] KnownPrefixes = ["var:", "calc:", "offset:"];

    private void Validate()
    {
        var txt = _box.Text;
        if (string.IsNullOrEmpty(txt)) { _errorBar.Visibility = Visibility.Collapsed; return; }

        // Plain integer — always valid
        if (long.TryParse(txt, out _)) { _errorBar.Visibility = Visibility.Collapsed; return; }

        foreach (var prefix in KnownPrefixes)
        {
            if (!txt.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var body = txt[prefix.Length..].Trim();
            if (string.IsNullOrEmpty(body))
            {
                _errorBar.Visibility = Visibility.Visible;
                ToolTip = $"Expression body missing after '{prefix}'";
                return;
            }
            _errorBar.Visibility = Visibility.Collapsed;
            ToolTip = GetSyntaxHint(prefix);
            return;
        }

        // Not a recognized prefix — could be a raw expression; no error
        _errorBar.Visibility = Visibility.Collapsed;
        ToolTip = "Supported: integer, var:name, calc:expression, offset:N";
    }

    private static string GetSyntaxHint(string prefix) => prefix switch
    {
        "var:"    => "var:variableName — references a previously stored variable",
        "calc:"   => "calc:expression — math expression using variables (e.g. calc:width*4)",
        "offset:" => "offset:N — fixed byte offset (N = integer)",
        _         => "",
    };

    private void UpdatePlaceholder()
    {
        var hasFocus = _box.IsFocused;
        var hasText  = !string.IsNullOrEmpty(_box.Text);
        _placeholder.Visibility = (!hasFocus && !hasText) ? Visibility.Visible : Visibility.Collapsed;
        _placeholder.Text       = Placeholder;
    }
}

//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: Controls/ExpressionTextBox.cs
// Description: TextBox with inline prefix coloring (var:/calc:/offset:),
//              lightweight syntax validation, and autocomplete popup
//              (similar to VS debugger breakpoint condition editor).
// Architecture Notes:
//     Popup is triggered by Ctrl+Space (immediate) or by typing a known prefix
//     (debounced 200ms). The inner _box TextBox is the PlacementTarget.
//     VariableSource must be set by the host (BlocksTab, ConditionEditorRow, etc.)
//     to provide live variable name completions.
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.Editor.StructureEditor.Models;
using WpfHexEditor.Editor.StructureEditor.Services;

namespace WpfHexEditor.Editor.StructureEditor.Controls;

/// <summary>
/// A <see cref="TextBox"/> extension that highlights recognized expression prefixes
/// (<c>var:</c>, <c>calc:</c>, <c>offset:</c>), validates their syntax inline,
/// and shows an autocomplete popup for functions and variable names.
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

    public static readonly DependencyProperty VariableSourceProperty =
        DependencyProperty.Register(nameof(VariableSource), typeof(IVariableSource), typeof(ExpressionTextBox),
            new PropertyMetadata(null));

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

    public IVariableSource? VariableSource
    {
        get => (IVariableSource?)GetValue(VariableSourceProperty);
        set => SetValue(VariableSourceProperty, value);
    }

    public event EventHandler? TextChanged;

    // ── Controls ──────────────────────────────────────────────────────────────

    private readonly TextBox                    _box;
    private readonly TextBlock                  _placeholder;
    private readonly Border                     _errorBar;
    private readonly ExpressionCompletePopup    _popup;
    private readonly ExpressionCompletionProvider _provider;
    private readonly DispatcherTimer            _triggerTimer;

    private bool _suppressCallback;

    private static readonly string[] KnownPrefixes = ["var:", "calc:", "offset:"];

    // ── Variable source resolution ────────────────────────────────────────────

    private sealed class EmptyVariableSource : IVariableSource
    {
        internal static readonly EmptyVariableSource Instance = new();
        public IReadOnlyList<string> GetVariableNames() => [];
    }

    /// <summary>
    /// Resolves the active variable source: explicit VariableSource DP first,
    /// then walks the visual tree to find a StructureEditor registration,
    /// then falls back to an empty list.
    /// </summary>
    private IVariableSource ResolveVariableSource()
    {
        if (VariableSource is not null) return VariableSource;

        // Walk visual tree to find the nearest registered StructureEditor
        DependencyObject? current = this;
        while (current is not null)
        {
            if (ExpressionContextService.Get(current) is { } src)
                return src;
            current = VisualTreeHelper.GetParent(current);
        }

        return EmptyVariableSource.Instance;
    }

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
        _placeholder.SetResourceReference(TextBlock.ForegroundProperty, "SE_PlaceholderForeground");

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
        _box.TextChanged     += OnBoxTextChanged;
        _box.GotFocus        += (_, _) => UpdatePlaceholder();
        _box.LostFocus       += OnBoxLostFocus;
        _box.PreviewKeyDown  += OnBoxPreviewKeyDown;

        _errorBar = new Border
        {
            Height            = 2,
            VerticalAlignment = VerticalAlignment.Bottom,
            Visibility        = Visibility.Collapsed,
            Margin            = new Thickness(2, 0, 2, 0),
        };
        _errorBar.SetResourceReference(Border.BackgroundProperty, "SE_ValidationErrorBrush");

        var outer = new Border { BorderThickness = new Thickness(1) };
        outer.SetResourceReference(Border.BorderBrushProperty, "DockBorderBrush");
        outer.SetResourceReference(Border.BackgroundProperty,  "DockMenuBackgroundBrush");

        var inner = new Grid();
        inner.Children.Add(_placeholder);
        inner.Children.Add(_box);
        inner.Children.Add(_errorBar);

        outer.Child = inner;
        Children.Add(outer);

        // Autocomplete infrastructure
        _provider     = new ExpressionCompletionProvider();
        _popup        = new ExpressionCompletePopup();
        _popup.SuggestionCommitted += OnSuggestionCommitted;

        _triggerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _triggerTimer.Tick += (_, _) => { _triggerTimer.Stop(); TriggerImmediate(); };

        // Stop timer and close popup when control is unloaded (prevents leaked timers)
        Unloaded += (_, _) =>
        {
            _triggerTimer.Stop();
            _popup.CloseIfOpen();
        };
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

    private void OnBoxTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_suppressCallback) return;
        _suppressCallback = true;
        Text = _box.Text;
        _suppressCallback = false;
        Validate();
        UpdatePlaceholder();
        TextChanged?.Invoke(this, EventArgs.Empty);

        // Autocomplete trigger: start debounce timer when a prefix is active
        var ctx = BuildContext();
        if (ctx.ActivePrefix != null)
        {
            _triggerTimer.Stop();
            _triggerTimer.Start();
        }
        else if (_popup.IsOpen)
        {
            _popup.UpdateFilter(ctx.Token);
        }
    }

    private void OnBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl+Space → immediate trigger
        if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _triggerTimer.Stop();
            TriggerImmediate();
            e.Handled = true;
            return;
        }

        if (!_popup.IsOpen) return;

        switch (e.Key)
        {
            case Key.Up:
                if (_popup.NavigateUp())   e.Handled = true;
                break;
            case Key.Down:
                if (_popup.NavigateDown()) e.Handled = true;
                break;
            case Key.Return:
            case Key.Tab:
                if (_popup.CommitIfOpen()) e.Handled = true;
                break;
            case Key.Escape:
                if (_popup.CloseIfOpen())  e.Handled = true;
                break;
        }
    }

    private void OnBoxLostFocus(object sender, RoutedEventArgs e)
    {
        UpdatePlaceholder();
        // Delay close slightly so mouse clicks on the popup list are not swallowed
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => _popup.CloseIfOpen());
    }

    private void OnSuggestionCommitted(object? sender, ExpressionCompleteSuggestion suggestion)
    {
        var ctx       = BuildContext();
        var fullText  = _box.Text;
        int tokenStart = ctx.ActivePrefix is null
            ? 0
            : fullText.LastIndexOf(ctx.ActivePrefix, _box.CaretIndex, StringComparison.Ordinal)
              + ctx.ActivePrefix.Length;

        // Replace [tokenStart .. caretIndex] with InsertText
        var prefix = fullText[..tokenStart];
        var suffix = _box.CaretIndex < fullText.Length ? fullText[_box.CaretIndex..] : "";
        _box.Text = prefix + suggestion.InsertText + suffix;

        // Position caret
        int newCaret = tokenStart + suggestion.InsertText.Length;
        if (suggestion.CursorOffset.HasValue)
            newCaret -= suggestion.CursorOffset.Value;
        _box.CaretIndex = Math.Clamp(newCaret, 0, _box.Text.Length);
    }

    // ── Autocomplete ──────────────────────────────────────────────────────────

    private void TriggerImmediate()
    {
        var ctx          = BuildContext();
        var ctxWithVars  = ctx with { VariableSource = ResolveVariableSource() };
        var suggestions  = _provider.GetSuggestions(ctxWithVars);
        _popup.ShowFor(suggestions, ctx.Token, _box);
    }

    /// <summary>
    /// Parses the text left of the caret to detect the active prefix and current token.
    /// </summary>
    private ExpressionCompleteContext BuildContext()
    {
        var text  = _box.Text ?? "";
        int caret = Math.Clamp(_box.CaretIndex, 0, text.Length);
        var left  = text[..caret];
        var src   = EmptyVariableSource.Instance;  // placeholder; resolved at trigger time

        foreach (var prefix in KnownPrefixes)
        {
            int idx = left.LastIndexOf(prefix, StringComparison.Ordinal);
            if (idx < 0) continue;

            // Ensure no space between prefix and caret
            var token = left[(idx + prefix.Length)..];
            if (token.Contains(' ')) continue;

            return new ExpressionCompleteContext(text, caret, prefix, token, src);
        }

        // No prefix — token is the full left-of-caret word (no spaces)
        var word = left.Contains(' ')
            ? left[(left.LastIndexOf(' ') + 1)..]
            : left;

        return new ExpressionCompleteContext(text, caret, null, word, src);
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private void Validate()
    {
        var txt = _box.Text;
        if (string.IsNullOrEmpty(txt)) { _errorBar.Visibility = Visibility.Collapsed; return; }

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

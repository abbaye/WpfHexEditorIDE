//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Custom CodeEditor - SmartComplete Popup (Phase 4+)
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com), Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.Core.ProjectSystem.Languages;
using WpfHexEditor.Editor.CodeEditor.Helpers;
using WpfHexEditor.Editor.CodeEditor.Models;
using WpfHexEditor.Editor.CodeEditor.Services;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.Editor.CodeEditor.Controls
{
    /// <summary>
    /// SmartComplete popup control with suggestion list and documentation preview.
    /// Displays context-aware suggestions from CodeSmartCompleteProvider.
    /// Phase 4: Basic implementation with root level + blocks contexts.
    /// Phase 7+: Icon colors, fuzzy highlight, companion doc popup, commit chars.
    /// </summary>
    public class SmartCompletePopup : Popup
    {
        #region Fields

        private ListBox     _suggestionList;
        private Border      _popupBorder;
        private CodeEditor  _editor;
        private CodeSmartCompleteProvider _provider;
        private DispatcherTimer _delayTimer;
        private string      _filterText = string.Empty;
        private List<SmartCompleteSuggestion> _allSuggestions;

        // Companion documentation popup (Phase 3)
        private Popup?      _docPopup;
        private StackPanel? _docContentPanel;
        private TextBlock?  _docHeaderBlock;

        // LSP integration
        private ILspClient? _lspClient;

        /// <summary>Current file path, kept in sync by CodeEditor.SetLspClient wiring.</summary>
        internal string? CurrentFilePath { get; set; }

        /// <summary>
        /// Current language definition, updated by CodeEditor when Language changes.
        /// Passed to <see cref="EditorPluginIntegration.GetLocalCompletions"/> on each trigger.
        /// </summary>
        internal LanguageDefinition? CurrentLanguage { get; set; }

        /// <summary>
        /// Local completion provider registry, injected by CodeEditor via
        /// <see cref="Controls.CodeEditor.SetLocalCompletionRegistry"/>.
        /// When set, local completions are prepended to the suggestion list.
        /// </summary>
        internal EditorPluginIntegration? LocalProviderRegistry { get; set; }

        /// <summary>
        /// Character that triggered the current completion session.
        /// Set by <see cref="TriggerWithDelay"/> / <see cref="TriggerImmediate"/> and consumed in
        /// <see cref="ShowSuggestions"/> to populate <see cref="SmartCompleteContext.TriggerCharacter"/>.
        /// Automatically cleared after each <see cref="ShowSuggestions"/> invocation.
        /// </summary>
        internal char? PendingTriggerCharacter { get; set; }

        #endregion

        #region Constructor

        public SmartCompletePopup(CodeEditor editor)
        {
            _editor   = editor ?? throw new ArgumentNullException(nameof(editor));
            _provider = new CodeSmartCompleteProvider();

            InitializeUI();
            InitializeTimer();

            StaysOpen          = false;
            AllowsTransparency = true;

            // Close on Escape / commit chars
            PreviewKeyDown   += SmartCompletePopup_PreviewKeyDown;
            PreviewTextInput += OnPreviewTextInput;

            // Build companion doc popup
            _docPopup = BuildDocPopup(out _docHeaderBlock, out _docContentPanel);
            Closed   += (_, _) => { if (_docPopup is not null) _docPopup.IsOpen = false; };
        }

        #endregion

        #region UI Initialization

        private void InitializeUI()
        {
            // Create main container
            var mainPanel = new DockPanel
            {
                MinWidth  = 300,
                MaxWidth  = 500,
                MinHeight = 100,
                MaxHeight = 400
            };

            // Create suggestion list
            _suggestionList = new ListBox
            {
                BorderThickness = new Thickness(0),
                FontFamily      = new FontFamily("Consolas"),
                FontSize        = 12,
                Padding         = new Thickness(0)
            };
            _suggestionList.SetResourceReference(ListBox.BackgroundProperty, "SC_Background");
            _suggestionList.SetResourceReference(ListBox.ForegroundProperty, "TE_Foreground");

            // Custom item template for suggestions
            var itemTemplate     = new DataTemplate();
            var stackPanelFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackPanelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            stackPanelFactory.SetValue(StackPanel.MarginProperty, new Thickness(4, 2, 4, 2));

            // Icon (Segoe MDL2 Assets glyph, colored by kind)
            var iconFactory = new FrameworkElementFactory(typeof(TextBlock));
            iconFactory.SetBinding(TextBlock.TextProperty,       new System.Windows.Data.Binding("Icon"));
            iconFactory.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("IconBrush"));
            iconFactory.SetValue(TextBlock.FontFamilyProperty,   new FontFamily("Segoe MDL2 Assets"));
            iconFactory.SetValue(TextBlock.FontSizeProperty,     14.0);
            iconFactory.SetValue(TextBlock.MarginProperty,       new Thickness(0, 0, 6, 0));
            iconFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            stackPanelFactory.AppendChild(iconFactory);

            // Display text — ContentControl + HighlightedTextConverter for match-char bolding
            var textFactory = new FrameworkElementFactory(typeof(ContentControl));
            textFactory.SetValue(ContentControl.VerticalAlignmentProperty, VerticalAlignment.Center);
            var conv = new HighlightedTextConverter();
            textFactory.SetBinding(ContentControl.ContentProperty,
                new System.Windows.Data.Binding(".") { Converter = conv });
            stackPanelFactory.AppendChild(textFactory);

            // Type hint — muted foreground
            var typeFactory = new FrameworkElementFactory(typeof(TextBlock));
            typeFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("TypeHint"));
            typeFactory.SetResourceReference(TextBlock.ForegroundProperty, "SC_TypeHintForeground");
            typeFactory.SetValue(TextBlock.MarginProperty,             new Thickness(8, 0, 0, 0));
            typeFactory.SetValue(TextBlock.FontSizeProperty,           10.0);
            typeFactory.SetValue(TextBlock.VerticalAlignmentProperty,  VerticalAlignment.Center);
            stackPanelFactory.AppendChild(typeFactory);

            itemTemplate.VisualTree = stackPanelFactory;
            _suggestionList.ItemTemplate = itemTemplate;

            // Handle selection change
            _suggestionList.SelectionChanged  += SuggestionList_SelectionChanged;
            _suggestionList.MouseDoubleClick  += (s, e) => CommitSelection();

            mainPanel.Children.Add(_suggestionList);

            // Create border around popup
            _popupBorder = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(3),
                Child           = mainPanel,
                Effect          = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color       = Colors.Black,
                    Opacity     = 0.3,
                    BlurRadius  = 5,
                    ShadowDepth = 2
                }
            };
            _popupBorder.SetResourceReference(Border.BackgroundProperty,   "SC_Background");
            _popupBorder.SetResourceReference(Border.BorderBrushProperty,  "SC_BorderBrush");

            Child = _popupBorder;
        }

        private void InitializeTimer()
        {
            _delayTimer      = new DispatcherTimer();
            _delayTimer.Tick += DelayTimer_Tick;
        }

        #endregion

        #region Companion Doc Popup (Phase 3)

        private Popup BuildDocPopup(out TextBlock header, out StackPanel contentPanel)
        {
            var bg     = Application.Current?.TryFindResource("SC_DocBackground")       as Brush
                         ?? new SolidColorBrush(Color.FromRgb(45, 45, 48));
            var border = Application.Current?.TryFindResource("SC_DocBorder")            as Brush
                         ?? new SolidColorBrush(Color.FromRgb(69, 69, 69));
            var fg     = Application.Current?.TryFindResource("SC_DocHeaderForeground")  as Brush
                         ?? new SolidColorBrush(Color.FromRgb(212, 212, 212));

            header = new TextBlock
            {
                FontWeight   = FontWeights.SemiBold,
                FontSize     = 12,
                Foreground   = fg,
                Margin       = new Thickness(8, 6, 8, 4),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth     = 360,
            };

            var sep = new Border
            {
                Height     = 1,
                Background = border,
                Margin     = new Thickness(0, 4, 0, 4),
            };

            contentPanel = new StackPanel { Orientation = Orientation.Vertical };

            var scroll = new ScrollViewer
            {
                MaxHeight                     = 280,
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content                       = contentPanel,
                Margin                        = new Thickness(8, 0, 8, 8),
            };

            var root = new Border
            {
                Width           = 380,
                Background      = bg,
                BorderBrush     = border,
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(3),
                Effect          = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius  = 8,
                    ShadowDepth = 2,
                    Opacity     = 0.4,
                },
                Child = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Children    = { header, sep, scroll },
                },
            };

            return new Popup
            {
                AllowsTransparency = true,
                StaysOpen          = true,
                IsHitTestVisible   = false,
                Placement          = PlacementMode.Custom,
                Child              = root,
                CustomPopupPlacementCallback = (popupSize, targetSize, _) =>
                    new[] { new CustomPopupPlacement(
                        new Point(targetSize.Width + 2, 0),
                        PopupPrimaryAxis.Horizontal) },
            };
        }

        private void ShowDocPopup(SmartCompleteSuggestion suggestion)
        {
            if (_docPopup is null || _docHeaderBlock is null || _docContentPanel is null) return;

            var detail = string.IsNullOrWhiteSpace(suggestion.TypeHint) ? null : suggestion.TypeHint;
            var doc    = suggestion.Documentation;

            if (string.IsNullOrWhiteSpace(doc))
            {
                _docPopup.IsOpen = false;
                return;
            }

            // Header: use TypeHint if available, else first line of doc
            _docHeaderBlock.Text = detail ?? (doc.Split('\n')[0].Trim().TrimStart('#').Trim());

            // Build doc content
            _docContentPanel.Children.Clear();
            foreach (var el in MarkdownInlineRenderer.Render(doc))
                _docContentPanel.Children.Add(el);

            _docPopup.PlacementTarget = this;
            _docPopup.IsOpen          = true;
        }

        private void HideDocPopup()
        {
            if (_docPopup is not null)
                _docPopup.IsOpen = false;
        }

        #endregion

        #region Event Handlers

        private void SmartCompletePopup_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    IsOpen = false;
                    e.Handled = true;
                    break;

                case Key.Enter:
                case Key.Tab:
                    CommitSelection();
                    e.Handled = true;
                    break;

                case Key.Up:
                    if (_suggestionList.SelectedIndex > 0)
                    {
                        _suggestionList.SelectedIndex--;
                        _suggestionList.ScrollIntoView(_suggestionList.SelectedItem);
                    }
                    e.Handled = true;
                    break;

                case Key.Down:
                    if (_suggestionList.SelectedIndex < _suggestionList.Items.Count - 1)
                    {
                        _suggestionList.SelectedIndex++;
                        _suggestionList.ScrollIntoView(_suggestionList.SelectedItem);
                    }
                    e.Handled = true;
                    break;

                case Key.PageUp:
                    _suggestionList.SelectedIndex = Math.Max(0, _suggestionList.SelectedIndex - 10);
                    _suggestionList.ScrollIntoView(_suggestionList.SelectedItem);
                    e.Handled = true;
                    break;

                case Key.PageDown:
                    _suggestionList.SelectedIndex = Math.Min(_suggestionList.Items.Count - 1, _suggestionList.SelectedIndex + 10);
                    _suggestionList.ScrollIntoView(_suggestionList.SelectedItem);
                    e.Handled = true;
                    break;

                case Key.Home:
                    _suggestionList.SelectedIndex = 0;
                    _suggestionList.ScrollIntoView(_suggestionList.SelectedItem);
                    e.Handled = true;
                    break;

                case Key.End:
                    _suggestionList.SelectedIndex = _suggestionList.Items.Count - 1;
                    _suggestionList.ScrollIntoView(_suggestionList.SelectedItem);
                    e.Handled = true;
                    break;
            }
        }

        private void OnPreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (!IsOpen) return;
            if (_suggestionList.SelectedItem is not SmartCompleteSuggestion selected) return;
            if (selected.CommitCharacters is null) return;
            if (!selected.CommitCharacters.Contains(e.Text)) return;

            // Commit the selected suggestion first, then let the char through
            CommitSelection();
            // Don't mark handled — the char gets inserted normally by the editor's text input
        }

        private CancellationTokenSource? _resolveCtS;

        private async void SuggestionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suggestionList.SelectedItem is not SmartCompleteSuggestion suggestion) return;

            ShowDocPopup(suggestion);

            // Lazy-load documentation via completionItem/resolve when doc is missing
            // and the item came from LSP.
            if (string.IsNullOrEmpty(suggestion.Documentation)
                && suggestion.RawLspItem?.RawJson is not null
                && _lspClient is not null)
            {
                _resolveCtS?.Cancel();
                _resolveCtS = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try
                {
                    var resolved = await _lspClient
                        .ResolveCompletionItemAsync(suggestion.RawLspItem, _resolveCtS.Token)
                        .ConfigureAwait(true);

                    if (resolved?.Documentation is { Length: > 0 } doc
                        && ReferenceEquals(_suggestionList.SelectedItem, suggestion))
                    {
                        suggestion.Documentation = doc;
                        ShowDocPopup(suggestion);
                    }
                }
                catch (OperationCanceledException) { }
            }
        }

        private void DelayTimer_Tick(object sender, EventArgs e)
        {
            _delayTimer.Stop();
            ShowSuggestions();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Trigger SmartComplete with delay (for auto-trigger on typing)
        /// </summary>
        public void TriggerWithDelay(int delayMs = 500, char? triggerChar = null)
        {
            PendingTriggerCharacter = triggerChar;
            _delayTimer.Stop();
            _delayTimer.Interval = TimeSpan.FromMilliseconds(delayMs);
            _delayTimer.Start();
        }

        /// <summary>
        /// Show SmartComplete immediately (for Ctrl+Space)
        /// </summary>
        public void TriggerImmediate(char? triggerChar = null)
        {
            PendingTriggerCharacter = triggerChar;
            _delayTimer.Stop();
            ShowSuggestions();
        }

        /// <summary>
        /// Update filter and refresh suggestions
        /// </summary>
        public void UpdateFilter(string filterText)
        {
            _filterText = filterText ?? string.Empty;
            FilterSuggestions();
        }

        /// <summary>
        /// Injects (or clears) the LSP client used for server-side completions.
        /// Called by CodeEditor.SetLspClient so both hover and completion share the same client.
        /// </summary>
        internal void SetLspClient(ILspClient? client) => _lspClient = client;

        /// <summary>
        /// Sets (or clears) the local completion provider registry.
        /// Called by CodeEditor.SetLocalCompletionRegistry when the host injects
        /// a language-specific registry (e.g. script globals for .csx files).
        /// </summary>
        internal void SetLocalCompletionRegistry(EditorPluginIntegration? registry)
            => LocalProviderRegistry = registry;

        /// <summary>
        /// Hide SmartComplete popup
        /// </summary>
        public void Hide()
        {
            IsOpen = false;
            _delayTimer.Stop();
            HideDocPopup();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Show suggestions based on current editor context.
        /// Tries LSP server first (5 s timeout); falls back to local CodeSmartCompleteProvider.
        /// </summary>
        private async void ShowSuggestions()
        {
            if (_editor == null || _editor.Document == null)
                return;

            try
            {
                var cursorPos   = _editor.CursorPosition;
                var triggerChar = PendingTriggerCharacter;   // capture before any await

                // ── LSP path ─────────────────────────────────────────────────
                if (_lspClient?.IsInitialized == true && CurrentFilePath is not null)
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    try
                    {
                        var lspItems = await _lspClient.CompletionAsync(
                            CurrentFilePath, cursorPos.Line, cursorPos.Column, triggerChar, cts.Token)
                            .ConfigureAwait(true);   // resume on UI thread

                        if (lspItems is { Count: > 0 })
                        {
                            _allSuggestions = lspItems.Select(MapLspItem).ToList();
                            ShowWithCurrentSuggestions();
                            return;
                        }
                    }
                    catch (OperationCanceledException) { /* timeout — fall through */ }
                    catch { /* LSP error — fall through */ }
                }

                // ── Local fallback ────────────────────────────────────────────
                var context = new SmartCompleteContext
                {
                    DocumentText   = _editor.GetText(),
                    CursorPosition = cursorPos,
                    CurrentLine    = _editor.Document.Lines[cursorPos.Line].Text,
                    TriggerCharacter = triggerChar
                };
                PendingTriggerCharacter = null;   // reset for next session

                _allSuggestions = _provider.GetSuggestions(context, CurrentLanguage);

                // Merge local completions (script globals, plugin-registered providers)
                if (LocalProviderRegistry is not null)
                {
                    var langId = CurrentLanguage?.Id ?? string.Empty;
                    var local  = LocalProviderRegistry.GetLocalCompletions(langId, context, CurrentLanguage);
                    if (local.Count > 0)
                        _allSuggestions = [..local, .._allSuggestions];
                }

                ShowWithCurrentSuggestions();
            }
            catch (Exception)
            {
                // Silently fail if SmartComplete cannot be shown
                Hide();
            }
        }

        private void ShowWithCurrentSuggestions()
        {
            if (_allSuggestions == null || _allSuggestions.Count == 0)
            {
                Hide();
                return;
            }

            FilterSuggestions();
            PositionPopup();
            IsOpen = true;
            _suggestionList.Focus();
        }

        /// <summary>Maps an LSP completion item to a SmartCompleteSuggestion for the popup.</summary>
        private static SmartCompleteSuggestion MapLspItem(LspCompletionItem item) =>
            new SmartCompleteSuggestion
            {
                DisplayText      = item.Label,
                InsertText       = item.InsertText ?? item.Label,
                Icon             = LspKindToGlyph(item.Kind),
                IconBrush        = LspKindToColor(item.Kind),
                TypeHint         = item.Detail ?? string.Empty,
                Documentation    = item.Documentation ?? string.Empty,
                SortPriority     = 100,
                RawLspItem       = item,
                CommitCharacters = item.CommitCharacters,
            };

        /// <summary>Maps LSP CompletionItemKind strings to Segoe MDL2 Assets glyphs.</summary>
        private static string LspKindToGlyph(string? kind) => kind?.ToLowerInvariant() switch
        {
            "method" or "function" or "constructor" => "\uE8A5",
            "class"  or "struct"   or "object"      => "\uE8B1",
            "interface"                             => "\uE8D4",
            "enum"   or "enummember"                => "\uE8D7",
            "field"  or "variable" or "constant"    => "\uE8E3",
            "property"                              => "\uE90F",
            "keyword"                               => "\uE8C1",
            "snippet"                               => "\uECC5",
            "namespace" or "module"                 => "\uE8A1",
            _                                       => "\uE8A5",
        };

        /// <summary>Resolves a kind-specific icon color brush from theme tokens, with hardcoded fallbacks.</summary>
        private static Brush LspKindToColor(string? kind)
        {
            var key = kind?.ToLowerInvariant() switch
            {
                "method" or "function" or "constructor" => "SC_IconMethodBrush",
                "class"  or "struct"   or "object"      => "SC_IconClassBrush",
                "interface"                             => "SC_IconInterfaceBrush",
                "enum"   or "enummember"                => "SC_IconEnumBrush",
                "field"  or "variable" or "constant"
                    or "property"                       => "SC_IconFieldBrush",
                "keyword"                               => "SC_IconKeywordBrush",
                "snippet"                               => "SC_IconSnippetBrush",
                _                                       => null,
            };

            if (key is not null && Application.Current?.TryFindResource(key) is Brush b)
                return b;

            // Hardcoded fallbacks (token not yet in Colors.xaml)
            return new SolidColorBrush(kind?.ToLowerInvariant() switch
            {
                "method" or "function" or "constructor" => Color.FromRgb(220, 220, 170),
                "class"  or "struct"   or "object"      => Color.FromRgb(78,  201, 176),
                "interface"                             => Color.FromRgb(184, 215, 163),
                "enum"   or "enummember"                => Color.FromRgb(184, 184, 224),
                "field"  or "variable" or "constant"
                    or "property"                       => Color.FromRgb(156, 220, 254),
                "keyword"                               => Color.FromRgb(86,  156, 214),
                "snippet"                               => Color.FromRgb(197, 134, 192),
                _                                       => Color.FromRgb(204, 204, 204),
            });
        }

        /// <summary>
        /// Filter suggestions based on current filter text.
        /// Populates MatchedIndices on each passing suggestion for highlight rendering.
        /// </summary>
        private void FilterSuggestions()
        {
            if (_allSuggestions == null)
                return;

            List<SmartCompleteSuggestion> filtered;

            if (string.IsNullOrEmpty(_filterText))
            {
                // No query — sort by SortPriority only, clear indices
                foreach (var s in _allSuggestions)
                {
                    s.MatchScore      = 0;
                    s.MatchedIndices  = null;
                }
                filtered = [.. _allSuggestions.OrderBy(s => s.SortPriority)];
            }
            else
            {
                // Fuzzy scoring pass — score every suggestion, keep those with score ≥ 0
                var scored = new List<SmartCompleteSuggestion>(_allSuggestions.Count);
                foreach (var suggestion in _allSuggestions)
                {
                    int score = SmartCompleteFuzzyScorer.Score(
                        _filterText, suggestion.DisplayText, out var indices);
                    if (score >= 0)
                    {
                        suggestion.MatchScore     = score + (1000 - suggestion.SortPriority);
                        suggestion.MatchedIndices = indices;
                        scored.Add(suggestion);
                    }
                }
                // Sort best match first
                filtered = [.. scored.OrderByDescending(s => s.MatchScore)];
            }

            _suggestionList.ItemsSource = filtered;

            if (filtered.Count > 0)
            {
                _suggestionList.SelectedIndex = 0;
            }
            else
            {
                Hide();
            }
        }

        /// <summary>
        /// Positions the popup directly below the caret using the editor's local coordinate rect.
        /// </summary>
        private void PositionPopup()
        {
            PlacementTarget    = _editor;
            PlacementRectangle = _editor.GetCaretDisplayRect();
            Placement          = PlacementMode.Bottom;
            HorizontalOffset   = 0;
            VerticalOffset     = 0;
        }

        /// <summary>
        /// Commit selected suggestion to editor
        /// </summary>
        private void CommitSelection()
        {
            if (_suggestionList.SelectedItem is SmartCompleteSuggestion suggestion)
            {
                InsertSuggestion(suggestion);
                Hide();
            }
        }

        /// <summary>
        /// Insert suggestion into editor at cursor position
        /// </summary>
        private void InsertSuggestion(SmartCompleteSuggestion suggestion)
        {
            if (_editor == null || suggestion == null)
                return;

            try
            {
                var cursorPos = _editor.CursorPosition;

                if (!string.IsNullOrEmpty(_filterText))
                {
                    var startPos = new TextPosition(cursorPos.Line, cursorPos.Column - _filterText.Length);
                    _editor.Document.DeleteRange(startPos, cursorPos);
                }

                var insertText = suggestion.InsertText ?? suggestion.DisplayText;
                _editor.Document.InsertText(cursorPos, insertText);

                var newColumn = cursorPos.Column + insertText.Length - _filterText.Length;
                if (suggestion.CursorOffset > 0)
                    newColumn = cursorPos.Column + suggestion.CursorOffset - _filterText.Length;

                // Editor will be invalidated by document change event
            }
            catch (Exception)
            {
                // Silently ignore insertion errors
            }
        }

        #endregion
    }
}

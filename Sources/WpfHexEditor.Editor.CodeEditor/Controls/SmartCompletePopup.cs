//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Custom CodeEditor - SmartComplete Popup (Phase 4)
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
using WpfHexEditor.Editor.CodeEditor.Helpers;
using WpfHexEditor.Editor.CodeEditor.Models;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.Editor.CodeEditor.Controls
{
    /// <summary>
    /// SmartComplete popup control with suggestion list and documentation preview.
    /// Displays context-aware suggestions from CodeSmartCompleteProvider.
    /// Phase 4: Basic implementation with root level + blocks contexts.
    /// Phase 7 will add all contexts, snippets, and tooltips.
    /// </summary>
    public class SmartCompletePopup : Popup
    {
        #region Fields

        private ListBox _suggestionList;
        private TextBlock _documentationPreview;
        private Border _popupBorder;
        private CodeEditor _editor;
        private CodeSmartCompleteProvider _provider;
        private DispatcherTimer _delayTimer;
        private string _filterText = string.Empty;
        private List<SmartCompleteSuggestion> _allSuggestions;

        // LSP integration
        private ILspClient? _lspClient;

        /// <summary>
        /// Screen position of the caret. Must be set by <c>CodeEditor</c> before
        /// calling <see cref="TriggerImmediate"/> or <see cref="TriggerWithDelay"/>.
        /// </summary>
        public Point CaretScreenPoint { get; set; }

        /// <summary>Current file path, kept in sync by CodeEditor.SetLspClient wiring.</summary>
        internal string? CurrentFilePath { get; set; }

        #endregion

        #region Constructor

        public SmartCompletePopup(CodeEditor editor)
        {
            _editor = editor ?? throw new ArgumentNullException(nameof(editor));
            _provider = new CodeSmartCompleteProvider();

            InitializeUI();
            InitializeTimer();

            StaysOpen = false;
            AllowsTransparency = true;

            // Close on Escape
            PreviewKeyDown += SmartCompletePopup_PreviewKeyDown;
        }

        #endregion

        #region UI Initialization

        private void InitializeUI()
        {
            // Create main container
            var mainPanel = new DockPanel
            {
                MinWidth = 300,
                MaxWidth = 500,
                MinHeight = 100,
                MaxHeight = 400
            };
            mainPanel.SetResourceReference(DockPanel.BackgroundProperty, "TE_Background");

            // Create documentation preview (top section)
            _documentationPreview = new TextBlock
            {
                Padding = new Thickness(8),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                MaxHeight = 100
            };
            _documentationPreview.SetResourceReference(TextBlock.BackgroundProperty, "Panel_ToolbarBrush");
            _documentationPreview.SetResourceReference(TextBlock.ForegroundProperty, "TE_Foreground");
            _documentationPreview.SetValue(DockPanel.DockProperty, Dock.Top);

            // Create separator
            var separator = new Border
            {
                Height = 1
            };
            separator.SetResourceReference(Border.BackgroundProperty, "Panel_ToolbarBorderBrush");
            separator.SetValue(DockPanel.DockProperty, Dock.Top);

            // Create suggestion list
            _suggestionList = new ListBox
            {
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Padding = new Thickness(0)
            };
            _suggestionList.SetResourceReference(ListBox.BackgroundProperty, "TE_Background");
            _suggestionList.SetResourceReference(ListBox.ForegroundProperty, "TE_Foreground");

            // Custom item template for suggestions
            var itemTemplate = new DataTemplate();
            var stackPanelFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackPanelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            stackPanelFactory.SetValue(StackPanel.MarginProperty, new Thickness(4, 2, 4, 2));

            // Icon
            var iconFactory = new FrameworkElementFactory(typeof(TextBlock));
            iconFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Icon"));
            iconFactory.SetValue(TextBlock.FontSizeProperty, 14.0);
            iconFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 6, 0));
            iconFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            stackPanelFactory.AppendChild(iconFactory);

            // Display text
            var textFactory = new FrameworkElementFactory(typeof(TextBlock));
            textFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("DisplayText"));
            textFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            textFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            stackPanelFactory.AppendChild(textFactory);

            // Type hint — foreground resolved from theme at runtime (PFP_SubTextBrush)
            var typeFactory = new FrameworkElementFactory(typeof(TextBlock));
            typeFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("TypeHint"));
            typeFactory.SetValue(TextBlock.ForegroundProperty,
                Application.Current.TryFindResource("PFP_SubTextBrush") as Brush ?? Brushes.Gray);
            typeFactory.SetValue(TextBlock.MarginProperty, new Thickness(8, 0, 0, 0));
            typeFactory.SetValue(TextBlock.FontSizeProperty, 10.0);
            typeFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            stackPanelFactory.AppendChild(typeFactory);

            itemTemplate.VisualTree = stackPanelFactory;
            _suggestionList.ItemTemplate = itemTemplate;

            // Handle selection change
            _suggestionList.SelectionChanged += SuggestionList_SelectionChanged;

            // Handle double-click to commit
            _suggestionList.MouseDoubleClick += (s, e) => CommitSelection();

            // Add controls to main panel
            mainPanel.Children.Add(_documentationPreview);
            mainPanel.Children.Add(separator);
            mainPanel.Children.Add(_suggestionList);

            // Create border around popup
            _popupBorder = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Child = mainPanel,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    Opacity = 0.3,
                    BlurRadius = 5,
                    ShadowDepth = 2
                }
            };
            _popupBorder.SetResourceReference(Border.BackgroundProperty, "TE_Background");
            _popupBorder.SetResourceReference(Border.BorderBrushProperty, "Panel_ToolbarBorderBrush");

            Child = _popupBorder;
        }

        private void InitializeTimer()
        {
            _delayTimer = new DispatcherTimer();
            _delayTimer.Tick += DelayTimer_Tick;
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

        private void SuggestionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suggestionList.SelectedItem is SmartCompleteSuggestion suggestion)
            {
                UpdateDocumentation(suggestion);
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
        public void TriggerWithDelay(int delayMs = 500)
        {
            _delayTimer.Stop();
            _delayTimer.Interval = TimeSpan.FromMilliseconds(delayMs);
            _delayTimer.Start();
        }

        /// <summary>
        /// Show SmartComplete immediately (for Ctrl+Space)
        /// </summary>
        public void TriggerImmediate()
        {
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
        /// Hide SmartComplete popup
        /// </summary>
        public void Hide()
        {
            IsOpen = false;
            _delayTimer.Stop();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Show suggestions based on current editor context.
        /// Tries LSP server first (1 s timeout); falls back to local CodeSmartCompleteProvider.
        /// </summary>
        private async void ShowSuggestions()
        {
            if (_editor == null || _editor.Document == null)
                return;

            try
            {
                var cursorPos = _editor.CursorPosition;

                // ── LSP path ─────────────────────────────────────────────────
                if (_lspClient?.IsInitialized == true && CurrentFilePath is not null)
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                    try
                    {
                        var lspItems = await _lspClient.CompletionAsync(
                            CurrentFilePath, cursorPos.Line, cursorPos.Column, cts.Token)
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
                    DocumentText = _editor.GetText(),
                    CursorPosition = cursorPos,
                    CurrentLine = _editor.Document.Lines[cursorPos.Line].Text
                };

                _allSuggestions = _provider.GetSuggestions(context);
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
                DisplayText   = item.Label,
                InsertText    = item.InsertText ?? item.Label,
                Icon          = LspKindToGlyph(item.Kind),
                TypeHint      = item.Detail ?? string.Empty,
                Documentation = item.Documentation ?? string.Empty,
                SortPriority  = 100,
            };

        /// <summary>Maps LSP CompletionItemKind strings to Segoe MDL2 Assets glyphs.</summary>
        private static string LspKindToGlyph(string? kind) => kind?.ToLowerInvariant() switch
        {
            "method" or "function" or "constructor" => "\uE8A7",  // Calculator
            "class"  or "interface" or "struct"     => "\uE8D7",  // People
            "field"  or "variable"                  => "\uE734",  // Tag
            "property"                              => "\uE90F",  // Settings
            "keyword"                               => "\uE8C1",  // Bookmarks
            "snippet"                               => "\uE70F",  // Page
            "enum"   or "enummember"                => "\uE762",  // List
            "module" or "namespace"                 => "\uE8B7",  // Package
            _                                       => "\uE8A5",  // Code
        };

        /// <summary>
        /// Filter suggestions based on current filter text
        /// </summary>
        private void FilterSuggestions()
        {
            if (_allSuggestions == null)
                return;

            List<SmartCompleteSuggestion> filtered;

            if (string.IsNullOrEmpty(_filterText))
            {
                filtered = _allSuggestions;
            }
            else
            {
                // Case-insensitive prefix matching
                filtered = _allSuggestions
                    .Where(s => s.DisplayText.StartsWith(_filterText, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // If no prefix matches, try contains matching
                if (filtered.Count == 0)
                {
                    filtered = _allSuggestions
                        .Where(s => s.DisplayText.IndexOf(_filterText, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();
                }
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
        /// Positions the popup at the screen coordinates previously set via <see cref="CaretScreenPoint"/>.
        /// Uses PlacementMode.Absolute so the popup appears directly below the caret regardless of
        /// where the CodeEditor control is on screen or how far it has scrolled.
        /// </summary>
        private void PositionPopup()
        {
            PlacementTarget  = _editor;
            Placement        = PlacementMode.Absolute;
            HorizontalOffset = CaretScreenPoint.X;
            VerticalOffset   = CaretScreenPoint.Y;
        }

        /// <summary>
        /// Update documentation preview for selected suggestion
        /// </summary>
        private void UpdateDocumentation(SmartCompleteSuggestion suggestion)
        {
            if (suggestion == null)
            {
                _documentationPreview.Text = string.Empty;
                _documentationPreview.Visibility = Visibility.Collapsed;
                return;
            }

            if (string.IsNullOrEmpty(suggestion.Documentation))
            {
                _documentationPreview.Visibility = Visibility.Collapsed;
            }
            else
            {
                _documentationPreview.Text = suggestion.Documentation;
                _documentationPreview.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Commit selected suggestion to editor
        /// </summary>
        private void CommitSelection()
        {
            if (_suggestionList.SelectedItem is SmartCompleteSuggestion suggestion)
            {
                // Insert suggestion text at cursor
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
                // Get current cursor position
                var cursorPos = _editor.CursorPosition;

                // Delete filter text if any
                if (!string.IsNullOrEmpty(_filterText))
                {
                    var startPos = new TextPosition(cursorPos.Line, cursorPos.Column - _filterText.Length);
                    _editor.Document.DeleteRange(startPos, cursorPos);
                }

                // Insert suggestion text
                var insertText = suggestion.InsertText ?? suggestion.DisplayText;
                _editor.Document.InsertText(cursorPos, insertText);

                // Move cursor to end of inserted text (or to specified cursor offset)
                var newColumn = cursorPos.Column + insertText.Length - _filterText.Length;
                if (suggestion.CursorOffset > 0)
                {
                    newColumn = cursorPos.Column + suggestion.CursorOffset - _filterText.Length;
                }

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

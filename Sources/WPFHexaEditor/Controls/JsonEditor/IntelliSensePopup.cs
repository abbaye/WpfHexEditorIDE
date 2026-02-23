//////////////////////////////////////////////
// Apache 2.0  - 2026
// Custom JsonEditor - IntelliSense Popup (Phase 4)
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexaEditor.Helpers.JsonEditor;
using WpfHexaEditor.Models.JsonEditor;

namespace WpfHexaEditor.Controls.JsonEditor
{
    /// <summary>
    /// IntelliSense popup control with suggestion list and documentation preview.
    /// Displays context-aware suggestions from JsonIntelliSenseProvider.
    /// Phase 4: Basic implementation with root level + blocks contexts.
    /// Phase 7 will add all contexts, snippets, and tooltips.
    /// </summary>
    public class IntelliSensePopup : Popup
    {
        #region Fields

        private ListBox _suggestionList;
        private TextBlock _documentationPreview;
        private Border _popupBorder;
        private JsonEditor _editor;
        private JsonIntelliSenseProvider _provider;
        private DispatcherTimer _delayTimer;
        private string _filterText = string.Empty;
        private List<IntelliSenseSuggestion> _allSuggestions;

        #endregion

        #region Constructor

        public IntelliSensePopup(JsonEditor editor)
        {
            _editor = editor ?? throw new ArgumentNullException(nameof(editor));
            _provider = new JsonIntelliSenseProvider();

            InitializeUI();
            InitializeTimer();

            StaysOpen = false;
            AllowsTransparency = true;

            // Close on Escape
            PreviewKeyDown += IntelliSensePopup_PreviewKeyDown;
        }

        #endregion

        #region UI Initialization

        private void InitializeUI()
        {
            // Create main container
            var mainPanel = new DockPanel
            {
                Background = Brushes.White,
                MinWidth = 300,
                MaxWidth = 500,
                MinHeight = 100,
                MaxHeight = 400
            };

            // Create documentation preview (top section)
            _documentationPreview = new TextBlock
            {
                Padding = new Thickness(8),
                TextWrapping = TextWrapping.Wrap,
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                Foreground = new SolidColorBrush(Color.FromRgb(64, 64, 64)),
                FontSize = 11,
                MaxHeight = 100
            };
            _documentationPreview.SetValue(DockPanel.DockProperty, Dock.Top);

            // Create separator
            var separator = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(200, 200, 200))
            };
            separator.SetValue(DockPanel.DockProperty, Dock.Top);

            // Create suggestion list
            _suggestionList = new ListBox
            {
                Background = Brushes.White,
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Padding = new Thickness(0)
            };

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

            // Type hint
            var typeFactory = new FrameworkElementFactory(typeof(TextBlock));
            typeFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("TypeHint"));
            typeFactory.SetValue(TextBlock.ForegroundProperty, Brushes.Gray);
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
                BorderBrush = new SolidColorBrush(Color.FromRgb(172, 172, 172)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Background = Brushes.White,
                Child = mainPanel,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    Opacity = 0.3,
                    BlurRadius = 5,
                    ShadowDepth = 2
                }
            };

            Child = _popupBorder;
        }

        private void InitializeTimer()
        {
            _delayTimer = new DispatcherTimer();
            _delayTimer.Tick += DelayTimer_Tick;
        }

        #endregion

        #region Event Handlers

        private void IntelliSensePopup_PreviewKeyDown(object sender, KeyEventArgs e)
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
            if (_suggestionList.SelectedItem is IntelliSenseSuggestion suggestion)
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
        /// Trigger IntelliSense with delay (for auto-trigger on typing)
        /// </summary>
        public void TriggerWithDelay(int delayMs = 500)
        {
            _delayTimer.Stop();
            _delayTimer.Interval = TimeSpan.FromMilliseconds(delayMs);
            _delayTimer.Start();
        }

        /// <summary>
        /// Show IntelliSense immediately (for Ctrl+Space)
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
        /// Hide IntelliSense popup
        /// </summary>
        public void Hide()
        {
            IsOpen = false;
            _delayTimer.Stop();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Show suggestions based on current editor context
        /// </summary>
        private void ShowSuggestions()
        {
            if (_editor == null || _editor.Document == null)
                return;

            try
            {
                // Get suggestions from provider
                var context = new IntelliSenseContext
                {
                    DocumentText = _editor.GetText(),
                    CursorPosition = _editor.CursorPosition,
                    CurrentLine = _editor.Document.Lines[_editor.CursorPosition.Line].Text
                };

                _allSuggestions = _provider.GetSuggestions(context);

                if (_allSuggestions == null || _allSuggestions.Count == 0)
                {
                    Hide();
                    return;
                }

                // Apply filter
                FilterSuggestions();

                // Position popup near cursor
                PositionPopup();

                // Show popup
                IsOpen = true;

                // Focus list for keyboard navigation
                _suggestionList.Focus();
            }
            catch (Exception)
            {
                // Silently fail if IntelliSense cannot be shown
                Hide();
            }
        }

        /// <summary>
        /// Filter suggestions based on current filter text
        /// </summary>
        private void FilterSuggestions()
        {
            if (_allSuggestions == null)
                return;

            List<IntelliSenseSuggestion> filtered;

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
        /// Position popup near cursor in editor
        /// </summary>
        private void PositionPopup()
        {
            PlacementTarget = _editor;
            Placement = PlacementMode.Custom;
            CustomPopupPlacementCallback = CalculatePopupPlacement;
        }

        /// <summary>
        /// Calculate popup placement near cursor
        /// </summary>
        private CustomPopupPlacement[] CalculatePopupPlacement(Size popupSize, Size targetSize, Point offset)
        {
            // Position below cursor with small offset
            var placement = new CustomPopupPlacement(new Point(10, 20), PopupPrimaryAxis.Horizontal);
            return new[] { placement };
        }

        /// <summary>
        /// Update documentation preview for selected suggestion
        /// </summary>
        private void UpdateDocumentation(IntelliSenseSuggestion suggestion)
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
            if (_suggestionList.SelectedItem is IntelliSenseSuggestion suggestion)
            {
                // Insert suggestion text at cursor
                InsertSuggestion(suggestion);
                Hide();
            }
        }

        /// <summary>
        /// Insert suggestion into editor at cursor position
        /// </summary>
        private void InsertSuggestion(IntelliSenseSuggestion suggestion)
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

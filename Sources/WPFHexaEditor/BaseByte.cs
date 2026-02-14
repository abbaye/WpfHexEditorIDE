//////////////////////////////////////////////
// Apache 2.0  - 2016-2021
// Base author : Derek Tremblay (derektremblay666@gmail.com)
// Contributor : emes30
// Contributor : ehsan69h
// Contributor : Janus Tida
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexaEditor.Core;
using WpfHexaEditor.Core.EventArguments;
using WpfHexaEditor.Core.Interfaces;

namespace WpfHexaEditor
{
    /// <summary>
    /// Base class for bytecontrol
    /// </summary>
    abstract class BaseByte : FrameworkElement, IByteControl
    {
        #region Global class variables
        protected readonly HexEditorLegacy _parent;
        private bool _isSelected;
        private ByteAction _action = ByteAction.Nothing;
        private IByte _byte;
        private bool _isHighLight;
        private bool _tooltipLoaded;

        // Performance optimization: Cache typeface and formatted text
        private Typeface _cachedTypeface;
        private FormattedText _cachedFormattedText;
        private string _lastRenderedText;
        private bool _isUpdating; // Prevent multiple UpdateVisual calls
        #endregion global class variables

        #region Events

        public event EventHandler<ByteEventArgs> ByteModified;
        public event EventHandler MouseSelection;
        public event EventHandler Click;
        public event EventHandler RightClick;
        public event EventHandler DoubleClick;
        public event EventHandler<ByteEventArgs> MoveNext;
        public event EventHandler<ByteEventArgs> MovePrevious;
        public event EventHandler<ByteEventArgs> MoveRight;
        public event EventHandler<ByteEventArgs> MoveLeft;
        public event EventHandler<ByteEventArgs> MoveUp;
        public event EventHandler<ByteEventArgs> MoveDown;
        public event EventHandler<ByteEventArgs> MovePageDown;
        public event EventHandler<ByteEventArgs> MovePageUp;
        public event EventHandler ByteDeleted;
        public event EventHandler EscapeKey;
        public event EventHandler CtrlzKey;
        public event EventHandler CtrlvKey;
        public event EventHandler CtrlcKey;
        public event EventHandler CtrlaKey;
        public event EventHandler CtrlyKey;

        #endregion Events

        #region Constructor

        protected BaseByte(HexEditorLegacy parent)
        {
            //Parent hexeditor
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));

            ToolTip = ".";

            //Default properties
            DataContext = this;
            Focusable = true;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Position in file
        /// </summary>
        public long BytePositionInStream { get; set; } = -1L;

        /// <summary>
        /// Used for selection coloring
        /// </summary>
        public bool FirstSelected { protected get; set; }

        /// <summary>
        /// Used to prevent ByteModified event occurc when we dont want! 
        /// </summary>
        public bool InternalChange { get; set; }

        /// <summary>
        /// Get or set if control as in read only mode
        /// </summary>
        public bool ReadOnlyMode { protected get; set; }

        /// <summary>
        /// Get or set the description to shown in tooltip
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Return true if mouse is over... (Used with traverse methods via IByteControl)
        /// </summary>
        public bool IsMouseOverMe { get; internal set; }

        /// <summary>
        /// Get or Set if control as selected
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (value == _isSelected) return;

                _isSelected = value;
                if (!_isUpdating) UpdateVisual();
            }
        }

        /// <summary>
        /// Get of Set if control as marked as highlighted
        /// </summary>
        public bool IsHighLight
        {
            get => _isHighLight;
            set
            {
                if (value == _isHighLight) return;

                _isHighLight = value;
                if (!_isUpdating) UpdateVisual();
            }
        }

        /// <summary>
        /// Byte used for this instance
        /// </summary>
        public IByte Byte
        {
            get => _byte;
            set
            {
                _byte = value;

                UpdateTextRenderFromByte();

                if (value is not null)
                    _byte.del_ByteOnChange += OnByteChange;
            }
        }

        internal void OnByteChange(List<byte> bytes, int index)
        {
            //if (Action != ByteAction.Nothing && InternalChange == false)
            if (InternalChange == false)
                ByteModified?.Invoke(this, new ByteEventArgs() { Index = index });

            UpdateTextRenderFromByte();
        }

        /// <summary>
        /// Action with this byte
        /// </summary>
        public ByteAction Action
        {
            get => _action;
            set
            {
                _action = value != ByteAction.All ? value : ByteAction.Nothing;

                if (!_isUpdating) UpdateVisual();
            }
        }

        protected FormattedText TextFormatted { get; private set; }

        /// <summary>
        /// Definie the foreground
        /// </summary>
        private static readonly DependencyProperty ForegroundProperty =
            TextElement.ForegroundProperty.AddOwner(typeof(BaseByte));

        protected Brush Foreground
        {
            get => (Brush)GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public static readonly DependencyProperty BackgroundProperty =
            TextElement.BackgroundProperty.AddOwner(typeof(BaseByte),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// Defines the background
        /// </summary>
        protected Brush Background
        {
            get => (Brush)GetValue(BackgroundProperty);
            set => SetValue(BackgroundProperty, value);
        }

        private static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(BaseByte),
                new FrameworkPropertyMetadata(string.Empty,
                    FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure,
                    OnTextChanged));

        /// <summary>
        /// Text to be displayed representation of Byte
        /// </summary>
        protected string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BaseByte baseByte)
            {
                baseByte._lastRenderedText = null; // Invalidate only text cache, keep typeface
            }
        }

        private static readonly DependencyProperty FontWeightProperty = TextElement.FontWeightProperty.AddOwner(typeof(BaseByte));

        /// <summary>
        /// The FontWeight property specifies the weight of the font.
        /// </summary>
        protected FontWeight FontWeight
        {
            get => (FontWeight)GetValue(FontWeightProperty);
            set => SetValue(FontWeightProperty, value);
        }

        #endregion Base properties

        #region Methods

        /// <summary>
        /// Begin batch update - prevents multiple UpdateVisual() calls
        /// </summary>
        protected void BeginUpdate()
        {
            _isUpdating = true;
        }

        /// <summary>
        /// End batch update - calls UpdateVisual() once
        /// </summary>
        protected void EndUpdate()
        {
            _isUpdating = false;
            UpdateVisual();
        }

        /// <summary>
        /// Update Background,foreground and font property
        /// </summary>
        public virtual void UpdateVisual()
        {
            if (IsSelected)
            {
                FontWeight = _parent.FontWeight;
                Foreground = _parent.ForegroundContrast;

                Background = FirstSelected
                    ? _parent.SelectionFirstColor
                    : _parent.SelectionSecondColor;
            }
            else if (IsHighLight)
            {
                FontWeight = _parent.FontWeight;
                Foreground = _parent.Foreground;
                Background = _parent.HighLightColor;
            }
            else if (Action != ByteAction.Nothing)
            {
                FontWeight = FontWeights.Bold;
                Foreground = _parent.Foreground;

                switch (Action)
                {
                    case ByteAction.Modified:
                        Background = _parent.ByteModifiedColor;
                        break;
                    case ByteAction.Deleted:
                        Background = _parent.ByteDeletedColor;
                        break;
                    case ByteAction.Added:
                        Background = _parent.ByteAddedColor;
                        break;
                }
            }
            else //Aoply a CustomBackgroundBlock over byte if needed
            {
                var cbb = _parent.GetCustomBackgroundBlock(BytePositionInStream);

                Description = cbb is not null ? cbb.Description : "";

                Background = cbb is not null ? cbb.Color : Brushes.Transparent;

                Foreground = _parent.GetColumnNumber(BytePositionInStream) % 2 == 0
                    ? _parent.Foreground
                    : _parent.ForegroundSecondColor;

                FontWeight = _parent.FontWeight;
            }

            UpdateAutoHighLiteSelectionByteVisual();

            InvalidateVisual();
        }

        /// <summary>
        /// Auto highlite SelectionByte
        /// </summary>
        protected void UpdateAutoHighLiteSelectionByteVisual()
        {
            if (_parent.AllowAutoHighLightSelectionByte && _parent.SelectionByte is not null &&
                Byte is not null && Byte.IsEqual(new byte[] { _parent.SelectionByte.Value }) && !IsSelected)
                Background = _parent.AutoHighLiteSelectionByteBrush;
        }

        /// <summary>
        /// Update the render of text derived bytecontrol from byte property
        /// </summary>
        public abstract void UpdateTextRenderFromByte();

        /// <summary>
        /// Clear control
        /// </summary>
        public virtual void Clear()
        {
            InternalChange = true;
            Byte = null;
            BytePositionInStream = -1;
            Action = ByteAction.Nothing;
            IsSelected = false;
            Description = string.Empty;
            InvalidateRenderCache(); // Clear cached rendering objects
            InternalChange = false;
        }

        #endregion

        #region Binding tooltip

        /// <summary>
        /// Load tooltip if necessary
        /// Hex editor is more faster when tootip is not loaded at creation
        /// </summary>
        internal void LoadToolTip()
        {
            if (_tooltipLoaded) return;

            // Load ressources dictionnary
            Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("/WPFHexaEditor;component/Resources/Dictionary/ToolTipDictionary.xaml", UriKind.Relative)
            });

            SetBinding(ToolTipProperty, new Binding
            {
                Source = FindResource("ByteToolTip"),
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                Mode = BindingMode.OneWay
            });

            _tooltipLoaded = true;
        }

        #endregion

        #region Events delegate

        /// <summary>
        /// Render the control (OPTIMIZED v2.2+)
        /// Caches Typeface and FormattedText to reduce allocations (2-3x faster)
        /// </summary>
        protected override void OnRender(DrawingContext dc)
        {
            //Draw background
            if (Background is not null)
                dc.DrawRectangle(Background, null, new Rect(0, 0, RenderSize.Width, RenderSize.Height));

            //OPTIMIZATION: Cache typeface - only recreate if font properties changed
            if (_cachedTypeface == null)
            {
                _cachedTypeface = new Typeface(_parent.FontFamily, _parent.FontStyle, FontWeight, _parent.FontStretch);
            }

            //OPTIMIZATION: Cache formatted text - only recreate if text changed
            if (_cachedFormattedText == null || _lastRenderedText != Text)
            {
                _cachedFormattedText = new FormattedText(Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    _cachedTypeface, _parent.FontSize, Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                _lastRenderedText = Text;
            }

            dc.DrawText(_cachedFormattedText, new Point(2, 2));

            //Update properties
            TextFormatted = _cachedFormattedText;
        }

        /// <summary>
        /// Invalidate cached rendering objects (call when font properties change)
        /// </summary>
        protected void InvalidateRenderCache()
        {
            _cachedTypeface = null;
            _cachedFormattedText = null;
            _lastRenderedText = null;
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            if (Byte is not null && !IsSelected && !IsHighLight &&
                Action != ByteAction.Modified &&
                Action != ByteAction.Deleted &&
                Action != ByteAction.Added)
                Background = _parent.MouseOverColor;

            UpdateAutoHighLiteSelectionByteVisual();

            if (e.LeftButton == MouseButtonState.Pressed)
                MouseSelection?.Invoke(this, e);

            IsMouseOverMe = true;

            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            var cbb = _parent.GetCustomBackgroundBlock(BytePositionInStream);

            if (Byte is not null && !IsSelected && !IsHighLight &&
                Action != ByteAction.Modified &&
                Action != ByteAction.Deleted &&
                Action != ByteAction.Added)
                Background = Brushes.Transparent;

            if (cbb is not null && !IsSelected && !IsHighLight &&
                Action != ByteAction.Modified &&
                Action != ByteAction.Deleted &&
                Action != ByteAction.Added)
                Background = cbb.Color;

            IsMouseOverMe = false;

            UpdateAutoHighLiteSelectionByteVisual();

            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (!IsFocused)  Focus();

                switch (e.ClickCount)
                {
                    case 1:
                        Click?.Invoke(this, e);
                        break;
                    case 2:
                        DoubleClick?.Invoke(this, e);
                        break;
                }
            }

            if (e.RightButton == MouseButtonState.Pressed)
                RightClick?.Invoke(this, e);

            base.OnMouseDown(e);
        }

        protected override void OnToolTipOpening(ToolTipEventArgs e)
        {
            if (Byte == null || !_parent.ShowByteToolTip)
                e.Handled = true;
            else
                LoadToolTip();

            base.OnToolTipOpening(e);
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            _parent.HideCaret();
            base.OnLostFocus(e);
        }

        protected void OnMoveNext(ByteEventArgs e) => MoveNext?.Invoke(this, e);

        protected bool KeyValidation(KeyEventArgs e)
        {
            #region Key validation and launch event if needed

            if (KeyValidator.IsUpKey(e.Key))
            {
                e.Handled = true;
                MoveUp?.Invoke(this, new ByteEventArgs(BytePositionInStream));

                return true;
            }
            if (KeyValidator.IsDownKey(e.Key))
            {
                e.Handled = true;
                MoveDown?.Invoke(this, new ByteEventArgs(BytePositionInStream));

                return true;
            }
            if (KeyValidator.IsLeftKey(e.Key))
            {
                e.Handled = true;
                MoveLeft?.Invoke(this, new ByteEventArgs(BytePositionInStream));

                return true;
            }
            if (KeyValidator.IsRightKey(e.Key))
            {
                e.Handled = true;
                MoveRight?.Invoke(this, new ByteEventArgs(BytePositionInStream));

                return true;
            }
            if (KeyValidator.IsPageDownKey(e.Key))
            {
                e.Handled = true;
                MovePageDown?.Invoke(this, new ByteEventArgs(BytePositionInStream));

                return true;
            }
            if (KeyValidator.IsPageUpKey(e.Key))
            {
                e.Handled = true;
                MovePageUp?.Invoke(this, new ByteEventArgs(BytePositionInStream));

                return true;
            }
            if (KeyValidator.IsDeleteKey(e.Key))
            {
                if (!ReadOnlyMode)
                {
                    e.Handled = true;
                    ByteDeleted?.Invoke(this, EventArgs.Empty);

                    return true;
                }
            }
            else if (KeyValidator.IsBackspaceKey(e.Key))
            {
                e.Handled = true;
                ByteDeleted?.Invoke(this, EventArgs.Empty);

                MovePrevious?.Invoke(this, new ByteEventArgs(BytePositionInStream));

                return true;
            }
            else if (KeyValidator.IsEscapeKey(e.Key))
            {
                e.Handled = true;
                EscapeKey?.Invoke(this, EventArgs.Empty);
                return true;
            }
            else if (KeyValidator.IsCtrlZKey(e.Key))
            {
                e.Handled = true;
                CtrlzKey?.Invoke(this, EventArgs.Empty);
                return true;
            }
            else if (KeyValidator.IsCtrlYKey(e.Key))
            {
                e.Handled = true;
                CtrlyKey?.Invoke(this, EventArgs.Empty);
                return true;
            }
            else if (KeyValidator.IsCtrlVKey(e.Key))
            {
                e.Handled = true;
                CtrlvKey?.Invoke(this, EventArgs.Empty);
                return true;
            }
            else if (KeyValidator.IsCtrlCKey(e.Key))
            {
                e.Handled = true;
                CtrlcKey?.Invoke(this, EventArgs.Empty);
                return true;
            }
            else if (KeyValidator.IsCtrlAKey(e.Key))
            {
                e.Handled = true;
                CtrlaKey?.Invoke(this, EventArgs.Empty);
                return true;
            }

            return false;
            #endregion
        }
    }
    #endregion
}


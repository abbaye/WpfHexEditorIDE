//////////////////////////////////////////////
// Apache 2.0  - 2026
// Base Editor Settings - Unified Architecture Helper for All Settings Panels
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)
// Pattern: Composition-based helper (WPF-compatible with partial classes)
//////////////////////////////////////////////

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core.Settings;

namespace WpfHexEditor.HexEditor.Controls
{
    /// <summary>
    /// Helper class for all editor settings panels.
    /// Provides unified architecture with auto-generated UI via DynamicSettingsGenerator.
    ///
    /// <para><b>Design Pattern: Composition (WPF-Compatible)</b></para>
    /// <para>
    /// - Uses composition instead of inheritance to support WPF partial classes
    /// - Auto-generates UI from [Category] attributes on Dependency Properties
    /// - Provides JSON-based persistence (GetSettingsJson/LoadSettingsJson)
    /// - Implements common button handlers (Save/Load/Reset)
    /// </para>
    ///
    /// <para><b>Usage:</b></para>
    /// <para>
    /// 1. Create UserControl with XAML (with ScrollViewer named "SettingsScrollViewer")
    /// 2. In constructor, create BaseEditorSettings&lt;T&gt; helper instance
    /// 3. Delegate method calls to the helper
    /// </para>
    ///
    /// <para><b>Example:</b></para>
    /// <code>
    /// public partial class MyEditorSettings : UserControl
    /// {
    ///     private MyEditor _editorControl;
    ///     private BaseEditorSettings&lt;MyEditor&gt; _baseHelper;
    ///
    ///     public MyEditorSettings()
    ///     {
    ///         InitializeComponent();
    ///         _baseHelper = new BaseEditorSettings&lt;MyEditor&gt;(
    ///             this, typeof(MyEditor),
    ///             () => _editorControl,
    ///             () => SettingsScrollViewer);
    ///     }
    ///
    ///     public string GetSettingsJson() => _baseHelper.GetSettingsJson();
    /// }
    /// </code>
    /// </summary>
    /// <typeparam name="TEditor">Type of editor control (e.g., HexEditor, JsonEditor)</typeparam>
    public class BaseEditorSettings<TEditor> where TEditor : FrameworkElement
    {
        #region Fields

        private readonly UserControl _owner;
        private readonly Type _editorType;
        private readonly Func<TEditor> _getEditorControl;
        private readonly Func<ScrollViewer> _getScrollViewer;

        protected SettingsStateService _stateService;
        protected DynamicSettingsGenerator _generator;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new BaseEditorSettings helper instance
        /// </summary>
        /// <param name="owner">The UserControl that owns this helper</param>
        /// <param name="editorType">The type of the editor control</param>
        /// <param name="getEditorControl">Function to get the editor control instance</param>
        /// <param name="getScrollViewer">Function to get the ScrollViewer for UI</param>
        public BaseEditorSettings(
            UserControl owner,
            Type editorType,
            Func<TEditor> getEditorControl,
            Func<ScrollViewer> getScrollViewer)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _editorType = editorType ?? throw new ArgumentNullException(nameof(editorType));
            _getEditorControl = getEditorControl ?? throw new ArgumentNullException(nameof(getEditorControl));
            _getScrollViewer = getScrollViewer ?? throw new ArgumentNullException(nameof(getScrollViewer));

            // Initialize services
            _stateService = new SettingsStateService(_editorType);
            _generator = new DynamicSettingsGenerator(_editorType);

            // Generate UI when control is loaded
            _owner.Loaded += OnOwnerLoaded;
        }

        #endregion

        #region Event Handlers

        private void OnOwnerLoaded(object sender, RoutedEventArgs e)
        {
            var editor = _getEditorControl();
            if (editor != null)
            {
                RegenerateUI(editor);
            }
        }

        #endregion

        #region UI Generation

        /// <summary>
        /// Regenerates the entire settings panel UI using reflection-based discovery.
        /// </summary>
        public void RegenerateUI(TEditor editor)
        {
            if (editor == null)
            {
                return;
            }

            try
            {
                // Generate the complete panel
                var panel = _generator.GenerateSettingsPanel();
                panel.DataContext = editor;

                // Set as content of ScrollViewer
                var scrollViewer = _getScrollViewer();
                if (scrollViewer != null)
                {
                    scrollViewer.Content = panel;
                    scrollViewer.UpdateLayout();

                    // Connect button handlers
                    WireUpButtonHandlers(panel);
                }
            }
            catch (Exception)
            {
                // Silently ignore errors
            }
        }

        /// <summary>
        /// Wires up button click handlers for Save/Load/Reset buttons.
        /// </summary>
        private void WireUpButtonHandlers(Panel rootPanel)
        {
            var saveButton = FindElementByName<Button>(rootPanel, "SaveStateButton");
            var loadButton = FindElementByName<Button>(rootPanel, "LoadStateButton");
            var resetButton = FindElementByName<Button>(rootPanel, "ResetButton");

            if (saveButton != null)
            {
                saveButton.Click += SaveStateButton_Click;
            }

            if (loadButton != null)
            {
                loadButton.Click += LoadStateButton_Click;
            }

            if (resetButton != null)
            {
                resetButton.Click += ResetButton_Click;
            }
        }

        /// <summary>
        /// Finds a control by name in the visual tree.
        /// </summary>
        protected T FindElementByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);

                if (child is T element && element.Name == name)
                    return element;

                var result = FindElementByName<T>(child, name);
                if (result != null)
                    return result;
            }

            return null;
        }

        #endregion

        #region Public API for Settings Persistence

        /// <summary>
        /// Gets the current editor settings as JSON string.
        /// The consumer is responsible for persisting this (file, database, registry, etc.)
        /// </summary>
        /// <returns>JSON string containing all settings</returns>
        /// <exception cref="InvalidOperationException">If EditorControl is not set</exception>
        public string GetSettingsJson()
        {
            System.Diagnostics.Debug.WriteLine("[BaseEditorSettings.GetSettingsJson] Called");

            var editor = _getEditorControl();
            System.Diagnostics.Debug.WriteLine($"[BaseEditorSettings.GetSettingsJson] Editor: {editor?.GetType().Name ?? "null"}");

            if (editor == null)
                throw new InvalidOperationException("EditorControl is not set");

            System.Diagnostics.Debug.WriteLine($"[BaseEditorSettings.GetSettingsJson] Calling SaveState on {_stateService?.GetType().Name ?? "null"}");
            var result = _stateService.SaveState(editor);
            System.Diagnostics.Debug.WriteLine($"[BaseEditorSettings.GetSettingsJson] SaveState returned {result?.Length ?? 0} chars");

            return result;
        }

        /// <summary>
        /// Loads editor settings from JSON string.
        /// </summary>
        /// <param name="json">JSON string containing settings (obtained from GetSettingsJson)</param>
        /// <exception cref="InvalidOperationException">If EditorControl is not set</exception>
        /// <exception cref="ArgumentException">If JSON is null or empty</exception>
        public void LoadSettingsJson(string json)
        {
            System.Diagnostics.Debug.WriteLine($"[BaseEditorSettings.LoadSettingsJson] Called with JSON length: {json?.Length ?? 0}");

            var editor = _getEditorControl();
            System.Diagnostics.Debug.WriteLine($"[BaseEditorSettings.LoadSettingsJson] Editor: {editor?.GetType().Name ?? "null"}");

            if (editor == null)
                throw new InvalidOperationException("EditorControl is not set");

            if (string.IsNullOrEmpty(json))
                throw new ArgumentException("JSON string cannot be null or empty", nameof(json));

            System.Diagnostics.Debug.WriteLine($"[BaseEditorSettings.LoadSettingsJson] Calling LoadState on {_stateService?.GetType().Name ?? "null"}");
            _stateService.LoadState(editor, json);
            System.Diagnostics.Debug.WriteLine($"[BaseEditorSettings.LoadSettingsJson] LoadState completed");
        }

        #endregion

        #region Button Event Handlers

        private void SaveStateButton_Click(object sender, RoutedEventArgs e)
        {
            var editor = _getEditorControl();
            if (editor == null) return;

            try
            {
                var json = GetSettingsJson();

                MessageBox.Show(
                    $"Settings JSON ready ({json.Length} chars).\n\n" +
                    "Use GetSettingsJson() to retrieve and persist this configuration.",
                    "Settings Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to generate settings JSON:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void LoadStateButton_Click(object sender, RoutedEventArgs e)
        {
            var editor = _getEditorControl();
            if (editor == null) return;

            try
            {
                MessageBox.Show(
                    "In your application, load the JSON and call LoadSettingsJson(json).",
                    "Load Settings",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load settings:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var editor = _getEditorControl();
            if (editor == null) return;

            var result = MessageBox.Show(
                "Reset all settings to defaults?\n\nThis will reset all properties to their DependencyProperty default values.",
                "Confirm Reset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var discoveryService = new PropertyDiscoveryService(_editorType);
                    var properties = discoveryService.DiscoverProperties();

                    foreach (var metadata in properties)
                    {
                        try
                        {
                            var propInfo = editor.GetType().GetProperty(metadata.PropertyName);
                            if (propInfo != null && propInfo.CanWrite && metadata.DefaultValue != null)
                            {
                                propInfo.SetValue(editor, metadata.DefaultValue);
                            }
                        }
                        catch (Exception)
                        {
                            // Silently ignore property reset errors
                        }
                    }

                    MessageBox.Show(
                        "Settings reset to defaults",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to reset settings:\n{ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        #endregion
    }
}

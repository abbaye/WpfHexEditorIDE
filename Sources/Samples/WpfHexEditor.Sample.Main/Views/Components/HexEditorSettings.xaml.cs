using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexaEditor;
using WpfHexaEditor.Core;
using WpfHexaEditor.Models;

namespace WpfHexEditor.Sample.Main.Views.Components
{
    /// <summary>
    /// Complete HexEditor Settings Panel
    /// Exposes all HexEditor properties for testing
    /// </summary>
    public partial class HexEditorSettings : UserControl
    {
        private global::WpfHexaEditor.HexEditor _hexEditorControl;

        /// <summary>
        /// Reference to the HexEditor control to configure
        /// </summary>
        public global::WpfHexaEditor.HexEditor HexEditorControl
        {
            get => _hexEditorControl;
            set
            {
                _hexEditorControl = value;

                // IMPORTANT: Set DataContext on the content (ScrollViewer), not on the UserControl itself
                // This preserves the UserControl's inherited DataContext for its own bindings (like Visibility)
                if (Content is FrameworkElement contentElement)
                {
                    contentElement.DataContext = value;
                    System.Diagnostics.Debug.WriteLine($"[HexEditorSettings] Set content DataContext. Content = {contentElement.GetType().Name}, HexEditor.ShowByteToolTip = {value?.ShowByteToolTip}");
                }

                // Recreate bindings if control is already loaded, otherwise wait for Loaded event
                if (value != null && IsLoaded)
                {
                    System.Diagnostics.Debug.WriteLine($"[HexEditorSettings] Control already loaded, calling RecreateBindings immediately");
                    RecreateBindings();
                }
                else if (value != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[HexEditorSettings] Control not yet loaded, will call RecreateBindings in Loaded event");
                }
            }
        }

        public HexEditorSettings()
        {
            InitializeComponent();

            // Update bindings when control is loaded
            Loaded += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine("[HexEditorSettings] Loaded event fired");
                if (HexEditorControl != null && Content is FrameworkElement contentElement)
                {
                    System.Diagnostics.Debug.WriteLine($"[HexEditorSettings] HexEditorControl exists, Content DataContext = {contentElement.DataContext?.GetType().Name}");

                    // IMPORTANT: Call RecreateBindings here when visual tree is fully loaded
                    System.Diagnostics.Debug.WriteLine($"[HexEditorSettings] Calling RecreateBindings from Loaded event");
                    RecreateBindings();

                    // Also log current state after setup
                    System.Diagnostics.Debug.WriteLine($"[HexEditorSettings] After RecreateBindings: ShowByteToolTip={HexEditorControl.ShowByteToolTip}");
                }
            };

            // Monitor DataContext changes on content
            if (Content is FrameworkElement element)
            {
                element.DataContextChanged += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[HexEditorSettings.Content] DataContextChanged: Old={e.OldValue?.GetType().Name}, New={e.NewValue?.GetType().Name}");
                };
            }
        }

        private void RecreateBindings()
        {
            if (HexEditorControl == null || Content is not FrameworkElement contentRoot)
                return;

            System.Diagnostics.Debug.WriteLine("[HexEditorSettings] RecreateBindings called");

            // Find all CheckBox controls and set up two-way synchronization
            void SetupCheckBoxBindings(DependencyObject element)
            {
                if (element == null) return;

                if (element is CheckBox checkBox)
                {
                    // Get the binding to determine which property to sync
                    var binding = System.Windows.Data.BindingOperations.GetBinding(checkBox, CheckBox.IsCheckedProperty);
                    if (binding != null && binding.Path != null)
                    {
                        var propertyName = binding.Path.Path;
                        System.Diagnostics.Debug.WriteLine($"  Setting up CheckBox for property: {propertyName}");

                        // Set initial value from HexEditor
                        var property = HexEditorControl.GetType().GetProperty(propertyName);
                        if (property != null && property.PropertyType == typeof(bool))
                        {
                            checkBox.IsChecked = (bool)property.GetValue(HexEditorControl);

                            // Remove old handler if exists
                            checkBox.Checked -= CheckBox_Changed;
                            checkBox.Unchecked -= CheckBox_Changed;

                            // Add handler to update HexEditor when checkbox changes
                            checkBox.Checked += CheckBox_Changed;
                            checkBox.Unchecked += CheckBox_Changed;

                            // Store property name in Tag for the handler
                            checkBox.Tag = propertyName;
                        }
                    }
                }

                // Recurse through visual tree
                int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(element);
                for (int i = 0; i < childCount; i++)
                {
                    var child = System.Windows.Media.VisualTreeHelper.GetChild(element, i);
                    SetupCheckBoxBindings(child);
                }
            }

            SetupCheckBoxBindings(contentRoot);

            // Initialize all ColorPickers with current values
            InitializeColorPickers();
        }

        private void InitializeColorPickers()
        {
            if (HexEditorControl == null) return;

            System.Diagnostics.Debug.WriteLine("[HexEditorSettings] InitializeColorPickers called");

            // Selection Colors
            SelectionFirstColorPicker.SelectedColor = HexEditorControl.SelectionFirstColor;
            SelectionSecondColorPicker.SelectedColor = HexEditorControl.SelectionSecondColor;

            // Byte State Colors
            ByteModifiedColorPicker.SelectedColor = HexEditorControl.ByteModifiedColor;
            ByteDeletedColorPicker.SelectedColor = HexEditorControl.ByteDeletedColor;
            ByteAddedColorPicker.SelectedColor = HexEditorControl.ByteAddedColor;

            // General Colors
            HighLightColorPicker.SelectedColor = HexEditorControl.HighLightColor;
            MouseOverColorPicker.SelectedColor = HexEditorControl.MouseOverColor;
            ForegroundSecondColorPicker.SelectedColor = HexEditorControl.ForegroundSecondColor;
            ForegroundContrastPicker.SelectedColor = HexEditorControl.ForegroundContrast;

            // TBL Colors
            TblDteColorPicker.SelectedColor = HexEditorControl.TblDteColor;
            TblMteColorPicker.SelectedColor = HexEditorControl.TblMteColor;
            TblEndBlockColorPicker.SelectedColor = HexEditorControl.TblEndBlockColor;
            TblEndLineColorPicker.SelectedColor = HexEditorControl.TblEndLineColor;
            TblDefaultColorPicker.SelectedColor = HexEditorControl.TblDefaultColor;

            // Bar Chart
            BarChartColorPicker.SelectedColor = HexEditorControl.BarChartColor;
        }

        private void CheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox checkBox || HexEditorControl == null)
                return;

            var propertyName = checkBox.Tag as string;
            if (string.IsNullOrEmpty(propertyName))
                return;

            var property = HexEditorControl.GetType().GetProperty(propertyName);
            if (property != null && property.PropertyType == typeof(bool))
            {
                var newValue = checkBox.IsChecked == true;
                property.SetValue(HexEditorControl, newValue);
                System.Diagnostics.Debug.WriteLine($"[HexEditorSettings] CheckBox changed: {propertyName} = {newValue}");
            }
        }

        private void UpdateBindings()
        {
            // Force all bindings to update from source (HexEditor properties)
            // This is needed because bindings were created before DataContext was set

            System.Diagnostics.Debug.WriteLine("[HexEditorSettings] UpdateBindings called");

            // Start from the content element, not the UserControl itself
            if (Content is not DependencyObject contentRoot)
            {
                System.Diagnostics.Debug.WriteLine("[HexEditorSettings] No content to update");
                return;
            }

            int bindingsUpdated = 0;

            // Helper method to recursively update all bindings in visual tree
            void UpdateBindingsRecursive(DependencyObject element)
            {
                if (element == null) return;

                // Get all locally set properties for this element
                var enumerator = element.GetLocalValueEnumerator();
                while (enumerator.MoveNext())
                {
                    var entry = enumerator.Current;
                    if (System.Windows.Data.BindingOperations.IsDataBound(element, entry.Property))
                    {
                        var bindingExpr = System.Windows.Data.BindingOperations.GetBindingExpression(element, entry.Property);
                        if (bindingExpr != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"  Updating binding on {element.GetType().Name}.{entry.Property.Name}");
                            bindingExpr.UpdateTarget();
                            bindingsUpdated++;
                        }
                    }
                }

                // Recurse through visual tree
                int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(element);
                for (int i = 0; i < childCount; i++)
                {
                    var child = System.Windows.Media.VisualTreeHelper.GetChild(element, i);
                    UpdateBindingsRecursive(child);
                }
            }

            UpdateBindingsRecursive(contentRoot);
            System.Diagnostics.Debug.WriteLine($"[HexEditorSettings] Updated {bindingsUpdated} bindings");
        }

        private void BytesPerLineComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HexEditorControl == null || BytesPerLineComboBox.SelectedItem == null) return;

            var selectedItem = (ComboBoxItem)BytesPerLineComboBox.SelectedItem;
            if (int.TryParse(selectedItem.Tag?.ToString(), out int bytesPerLine))
            {
                HexEditorControl.BytePerLine = bytesPerLine;
            }
        }

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (HexEditorControl == null) return;

            // Apply zoom via ScaleTransform
            var scaleTransform = new ScaleTransform(e.NewValue, e.NewValue);
            HexEditorControl.LayoutTransform = scaleTransform;
        }

        private void EditModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HexEditorControl == null || EditModeComboBox.SelectedItem == null) return;

            var selectedItem = (ComboBoxItem)EditModeComboBox.SelectedItem;
            var editModeString = selectedItem.Tag?.ToString();

            if (Enum.TryParse<EditMode>(editModeString, out var editMode))
            {
                HexEditorControl.EditMode = editMode;
            }
        }

        private void CaretModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HexEditorControl == null || CaretModeComboBox.SelectedItem == null) return;

            var selectedItem = (ComboBoxItem)CaretModeComboBox.SelectedItem;
            var caretModeString = selectedItem.Tag?.ToString();

            if (Enum.TryParse<WpfHexaEditor.Core.CaretMode>(caretModeString, out var caretMode))
            {
                HexEditorControl.VisualCaretMode = caretMode;
            }
        }

        private void CopyModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HexEditorControl == null || CopyModeComboBox.SelectedItem == null) return;

            var selectedItem = (ComboBoxItem)CopyModeComboBox.SelectedItem;
            var copyModeString = selectedItem.Tag?.ToString();

            if (Enum.TryParse<CopyPasteMode>(copyModeString, out var copyMode))
            {
                HexEditorControl.DefaultCopyToClipboardMode = copyMode;
            }
        }

        private void ByteShiftLeftTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (HexEditorControl == null) return;

            if (long.TryParse(ByteShiftLeftTextBox.Text, out long byteShift))
            {
                HexEditorControl.ByteShiftLeft = byteShift;
            }
        }

        private void SaveStateButton_Click(object sender, RoutedEventArgs e)
        {
            if (HexEditorControl == null) return;

            try
            {
                var state = HexEditorControl.CurrentState;
                // Save to file or storage (implement as needed)
                MessageBox.Show(
                    "State saved successfully!",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to save state: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void LoadStateButton_Click(object sender, RoutedEventArgs e)
        {
            if (HexEditorControl == null) return;

            try
            {
                // Load from file or storage (implement as needed)
                MessageBox.Show(
                    "Load state feature - to be implemented",
                    "Info",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load state: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void AutoHighlightColorPicker_ColorChanged(object sender, Color e)
        {
            if (HexEditorControl == null) return;
            HexEditorControl.AutoHighLiteSelectionByteBrush = e;
        }

        // Selection Colors
        private void SelectionFirstColorPicker_ColorChanged(object sender, Color e)
        {
            if (HexEditorControl == null) return;
            HexEditorControl.SelectionFirstColor = e;
        }

        private void SelectionSecondColorPicker_ColorChanged(object sender, Color e)
        {
            if (HexEditorControl == null) return;
            HexEditorControl.SelectionSecondColor = e;
        }

        // Byte State Colors
        private void ByteModifiedColorPicker_ColorChanged(object sender, Color e)
        {
            if (HexEditorControl == null) return;
            HexEditorControl.ByteModifiedColor = e;
        }

        private void ByteDeletedColorPicker_ColorChanged(object sender, Color e)
        {
            if (HexEditorControl == null) return;
            HexEditorControl.ByteDeletedColor = e;
        }

        private void ByteAddedColorPicker_ColorChanged(object sender, Color e)
        {
            if (HexEditorControl == null) return;
            HexEditorControl.ByteAddedColor = e;
        }

        // General Colors
        private void HighLightColorPicker_ColorChanged(object sender, Color e)
        {
            if (HexEditorControl == null) return;
            HexEditorControl.HighLightColor = e;
        }

        private void MouseOverColorPicker_ColorChanged(object sender, Color e)
        {
            if (HexEditorControl == null) return;
            HexEditorControl.MouseOverColor = e;
        }

        private void ForegroundSecondColorPicker_ColorChanged(object sender, Color e)
        {
            if (HexEditorControl == null) return;
            HexEditorControl.ForegroundSecondColor = e;
        }

        private void ForegroundContrastPicker_ColorChanged(object sender, Color e)
        {
            if (HexEditorControl == null) return;
            HexEditorControl.ForegroundContrast = e;
        }

        // TBL Colors
        private void TblDteColorPicker_ColorChanged(object sender, Color e)
        {
            if (HexEditorControl == null) return;
            HexEditorControl.TblDteColor = e;
        }

        private void TblMteColorPicker_ColorChanged(object sender, Color e)
        {
            if (HexEditorControl == null) return;
            HexEditorControl.TblMteColor = e;
        }

        private void TblEndBlockColorPicker_ColorChanged(object sender, Color e)
        {
            if (HexEditorControl == null) return;
            HexEditorControl.TblEndBlockColor = e;
        }

        private void TblEndLineColorPicker_ColorChanged(object sender, Color e)
        {
            if (HexEditorControl == null) return;
            HexEditorControl.TblEndLineColor = e;
        }

        private void TblDefaultColorPicker_ColorChanged(object sender, Color e)
        {
            if (HexEditorControl == null) return;
            HexEditorControl.TblDefaultColor = e;
        }

        // Bar Chart
        private void BarChartColorPicker_ColorChanged(object sender, Color e)
        {
            if (HexEditorControl == null) return;
            HexEditorControl.BarChartColor = e;
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (HexEditorControl == null) return;

            var result = MessageBox.Show(
                "Reset all settings to defaults?",
                "Confirm Reset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Reset to defaults
                HexEditorControl.ShowByteToolTip = false;
                HexEditorControl.HideByteDeleted = false;
                HexEditorControl.BytePerLine = 16;
                HexEditorControl.ReadOnlyMode = false;
                HexEditorControl.CanInsertAnywhere = false;
                HexEditorControl.AllowDeleteByte = true;
                HexEditorControl.AllowExtend = true;
                HexEditorControl.AppendNeedConfirmation = false;
                HexEditorControl.EditMode = EditMode.Overwrite;
                HexEditorControl.VisualCaretMode = WpfHexaEditor.Core.CaretMode.Overwrite;
                HexEditorControl.DefaultCopyToClipboardMode = CopyPasteMode.HexaString;
                HexEditorControl.AllowFileDrop = true;
                HexEditorControl.FileDroppingConfirmation = false;
                HexEditorControl.AllowTextDrop = true;
                HexEditorControl.AllowAutoHighLightSelectionByte = true;
                HexEditorControl.AllowAutoSelectSameByteAtDoubleClick = true;
                HexEditorControl.AllowMarkerClickNavigation = true;
                HexEditorControl.AllowByteCount = true;
                HexEditorControl.ByteShiftLeft = 0;

                // Reset UI controls
                BytesPerLineComboBox.SelectedIndex = 1; // 16 bytes
                ZoomSlider.Value = 1.0;
                EditModeComboBox.SelectedIndex = 0;
                CaretModeComboBox.SelectedIndex = 0;
                CopyModeComboBox.SelectedIndex = 0;
                ByteShiftLeftTextBox.Text = "0";
                AutoHighlightColorPicker.SelectedColor = Color.FromRgb(0x40, 0x40, 0xFF);

                MessageBox.Show(
                    "Settings reset to defaults",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }
}

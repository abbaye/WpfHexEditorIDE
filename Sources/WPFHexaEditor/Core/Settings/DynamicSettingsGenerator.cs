// Apache 2.0 - 2026
// Property Discovery and Auto-Generation System
// Author: Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using WpfHexaEditor.Core.Settings.Controls;

namespace WpfHexaEditor.Core.Settings
{
    /// <summary>
    /// Dynamically generates the complete settings panel UI via reflection.
    /// Creates Expanders by category, each containing auto-generated controls for properties.
    /// </summary>
    public class DynamicSettingsGenerator
    {
        private readonly PropertyDiscoveryService _discoveryService;
        private readonly Type _targetType;

        public DynamicSettingsGenerator(Type targetType)
        {
            _targetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
            _discoveryService = new PropertyDiscoveryService(targetType);
        }

        /// <summary>
        /// Generates the complete settings panel with Expanders grouped by category.
        /// Returns a StackPanel containing all UI elements.
        /// </summary>
        public StackPanel GenerateSettingsPanel()
        {
            var rootPanel = new StackPanel { Margin = new Thickness(16) };

            // 1. Header
            rootPanel.Children.Add(CreateHeader());

            // 2. Group properties by category
            var propertiesByCategory = _discoveryService.GroupByCategory();

            // 3. Create an Expander for each category
            foreach (var category in propertiesByCategory)
            {
                var expander = CreateCategoryExpander(category.Key, category.Value);
                rootPanel.Children.Add(expander);
            }

            // 4. Buttons (Save/Load/Reset) at the bottom
            rootPanel.Children.Add(CreateActionButtons());

            return rootPanel;
        }

        /// <summary>
        /// Creates the header TextBlock at the top of the panel.
        /// </summary>
        private TextBlock CreateHeader()
        {
            var header = new TextBlock
            {
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 16)
            };

            // Use DynamicResource for Text so it updates when language changes
            try
            {
                header.SetResourceReference(TextBlock.TextProperty, "HexSettings_Title");
            }
            catch
            {
                header.Text = "Hex Editor Settings";
            }

            return header;
        }

        /// <summary>
        /// Creates an Expander for a single category with all its properties.
        /// </summary>
        private Expander CreateCategoryExpander(string category, List<PropertyMetadata> properties)
        {
            var expander = new Expander
            {
                IsExpanded = (category == "Display"), // First expander (Display) open by default
                Margin = new Thickness(0, 0, 0, 8),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200))
            };

            // Use DynamicResource for Header so it updates when language changes
            var resourceKey = $"HexSettings_{category}_Title";
            try
            {
                expander.SetResourceReference(Expander.HeaderProperty, resourceKey);
                System.Diagnostics.Debug.WriteLine($"[Localization] Expander '{category}': Using DynamicResource '{resourceKey}'");
            }
            catch
            {
                // Fallback if resource doesn't exist
                expander.Header = GetCategoryHeader(category);
                System.Diagnostics.Debug.WriteLine($"[Localization] Expander '{category}': Using fallback (resource not found)");
            }

            var contentPanel = new StackPanel
            {
                Margin = new Thickness(16, 8, 0, 0)
            };

            // Generate controls for each property in this category
            foreach (var property in properties)
            {
                // Skip read-only properties for now (can be added later if needed)
                if (property.IsReadOnly)
                    continue;

                var controlElement = GeneratePropertyControl(property);
                if (controlElement != null)
                    contentPanel.Children.Add(controlElement);
            }

            expander.Content = contentPanel;
            return expander;
        }

        /// <summary>
        /// Generates the appropriate control for a single property.
        /// Returns a FrameworkElement (may be wrapped in StackPanel with label).
        /// </summary>
        private FrameworkElement GeneratePropertyControl(PropertyMetadata metadata)
        {
            try
            {
                // 1. Create control via Factory
                var propertyControl = PropertyControlFactory.Create(metadata);
                System.Diagnostics.Debug.WriteLine($"[PropertyControl] {metadata.PropertyName} ({metadata.PropertyType.Name}): Using {propertyControl.GetType().Name}");

                // 2. Create the WPF control
                var control = propertyControl.CreateControl();
                System.Diagnostics.Debug.WriteLine($"  Created control: {control.GetType().Name}, Tag={control.Tag}");

                // 3. Create binding
                var binding = propertyControl.CreateBinding(metadata);

                // 4. Apply binding and configure control based on type
                return ConfigureControl(control, binding, metadata, propertyControl);
            }
            catch (Exception ex)
            {
                // Log error and return error placeholder
                System.Diagnostics.Debug.WriteLine($"Error generating control for {metadata.PropertyName}: {ex.Message}");
                return new TextBlock
                {
                    Text = $"⚠ Error: {metadata.PropertyName}",
                    Foreground = Brushes.Red,
                    Margin = new Thickness(0, 0, 0, 8)
                };
            }
        }

        /// <summary>
        /// Configures the control with binding and special handling based on control type.
        /// </summary>
        private FrameworkElement ConfigureControl(
            FrameworkElement control,
            Binding binding,
            PropertyMetadata metadata,
            IPropertyControl propertyControl)
        {
            // CheckBox (bool properties)
            if (control is CheckBox checkBox)
            {
                checkBox.SetBinding(CheckBox.IsCheckedProperty, binding);
                checkBox.Content = metadata.GetDisplayName();
                return checkBox;
            }

            // ComboBox (enum or int with AllowedValues)
            if (control is ComboBox comboBox)
            {
                // Set up converter if needed
                if (metadata.PropertyType.IsEnum)
                {
                    // Populate enum values
                    if (propertyControl is EnumPropertyControl enumControl)
                    {
                        enumControl.PopulateEnumValues(comboBox, metadata);
                    }

                    // Note: EnumToComboBoxConverter should be applied
                    // For now, use direct binding without converter (works if Tag matches enum string)
                    comboBox.SetBinding(ComboBox.SelectedValueProperty, binding);
                }
                else if (metadata.PropertyName == "BytePerLine")
                {
                    // Special case: BytePerLine with converter
                    if (propertyControl is IntComboBoxPropertyControl intControl)
                    {
                        intControl.PopulateAllowedValues(comboBox, metadata);
                    }

                    // Use SelectedIndex binding (converter will be added in HexEditorSettings if needed)
                    comboBox.SetBinding(ComboBox.SelectedIndexProperty, binding);
                }
                else
                {
                    // Generic int ComboBox
                    if (propertyControl is IntComboBoxPropertyControl intControl)
                    {
                        intControl.PopulateAllowedValues(comboBox, metadata);
                    }
                    comboBox.SetBinding(ComboBox.SelectedValueProperty, binding);
                }

                return CreateControlWithLabel(propertyControl.CreateLabel(metadata), comboBox);
            }

            // Grid with Slider (double properties)
            if (control is Grid gridWithSlider)
            {
                var slider = gridWithSlider.Children.OfType<Slider>().FirstOrDefault();
                if (slider != null)
                {
                    slider.SetBinding(Slider.ValueProperty, binding);

                    // Apply constraints
                    if (metadata.Constraints != null)
                    {
                        if (metadata.Constraints.MinValue.HasValue)
                            slider.Minimum = metadata.Constraints.MinValue.Value;
                        if (metadata.Constraints.MaxValue.HasValue)
                            slider.Maximum = metadata.Constraints.MaxValue.Value;
                        if (metadata.Constraints.StepValue.HasValue)
                            slider.TickFrequency = metadata.Constraints.StepValue.Value;
                    }

                    // Bind TextBlock to Slider value
                    var textBlock = gridWithSlider.Children.OfType<TextBlock>().FirstOrDefault();
                    if (textBlock != null)
                    {
                        textBlock.SetBinding(TextBlock.TextProperty, new Binding("Value")
                        {
                            Source = slider,
                            StringFormat = "{0:F2}"
                        });
                    }
                }

                return CreateControlWithLabel(propertyControl.CreateLabel(metadata), gridWithSlider);
            }

            // TextBox (int, long, string properties)
            if (control is TextBox textBox)
            {
                textBox.SetBinding(TextBox.TextProperty, binding);
                return CreateControlWithLabel(propertyControl.CreateLabel(metadata), textBox);
            }

            // Border placeholder for ColorPicker (will be replaced in HexEditorSettings.xaml.cs)
            if (control is Border border)
            {
                System.Diagnostics.Debug.WriteLine($"  Found Border! Tag={border.Tag}, checking if == 'ColorPicker'...");
                if (border.Tag?.ToString() == "ColorPicker")
                {
                    // Mark with property name for later replacement
                    border.Tag = $"ColorPicker:{metadata.PropertyName}";
                    System.Diagnostics.Debug.WriteLine($"  ✓ Set Border Tag to '{border.Tag}'");

                    var wrapper = CreateControlWithLabel(propertyControl.CreateLabel(metadata), border);

                    // VERIFY: Tag still exists after wrapping
                    System.Diagnostics.Debug.WriteLine($"  🔍 After CreateControlWithLabel: Border.Tag={border.Tag}");

                    return wrapper;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"  ✗ Border Tag didn't match! Tag='{border.Tag}' != 'ColorPicker'");
                }
            }

            // Default: just return the control
            System.Diagnostics.Debug.WriteLine($"  Returning control as-is (Default)");
            return control;
        }

        /// <summary>
        /// Wraps a control with its label in a StackPanel.
        /// </summary>
        private StackPanel CreateControlWithLabel(TextBlock label, FrameworkElement control)
        {
            var wrapper = new StackPanel();

            if (label != null)
                wrapper.Children.Add(label);

            wrapper.Children.Add(control);
            return wrapper;
        }

        /// <summary>
        /// Gets the localized category header, or falls back to raw category name.
        /// </summary>
        private string GetCategoryHeader(string category)
        {
            var resourceKey = $"HexSettings_{category}_Title";
            var localized = TryFindResource(resourceKey) as string;

            System.Diagnostics.Debug.WriteLine($"[Localization] Category '{category}': Key='{resourceKey}', Found={localized != null}, Value={localized ?? "null"}");

            return localized ?? AddEmojiToCategory(category);
        }

        /// <summary>
        /// Adds emoji to category headers for better visual distinction.
        /// </summary>
        private string AddEmojiToCategory(string category)
        {
            return category switch
            {
                "StatusBar" => "📊 Status Bar",
                "Display" => "🖥️ Display",
                "Colors" => "🌈 Colors",
                "Behavior" => "⚙️ Behavior",
                "Data" => "💾 Data",
                "Visual" => "👁️ Visual",
                "Keyboard" => "⌨️ Keyboard",
                "TBL" => "📝 Character Table",
                _ => category
            };
        }

        /// <summary>
        /// Creates action buttons (Save/Load/Reset) at the bottom of the panel.
        /// </summary>
        private StackPanel CreateActionButtons()
        {
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 16, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            // Save State button - Use DynamicResource for Content
            var saveButton = new Button
            {
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(12, 6, 12, 6),
                Name = "SaveStateButton"
            };
            try { saveButton.SetResourceReference(Button.ContentProperty, "HexSettings_SaveButton"); }
            catch { saveButton.Content = "Save State"; }

            // Load State button - Use DynamicResource for Content
            var loadButton = new Button
            {
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(12, 6, 12, 6),
                Name = "LoadStateButton"
            };
            try { loadButton.SetResourceReference(Button.ContentProperty, "HexSettings_LoadButton"); }
            catch { loadButton.Content = "Load State"; }

            // Reset button - Use DynamicResource for Content
            var resetButton = new Button
            {
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(12, 6, 12, 6),
                Name = "ResetButton"
            };
            try { resetButton.SetResourceReference(Button.ContentProperty, "HexSettings_ResetButton"); }
            catch { resetButton.Content = "Reset to Defaults"; }

            // Apply ModernButtonStyle if available
            var buttonStyle = TryFindResource("ModernButtonStyle") as Style;
            if (buttonStyle != null)
            {
                saveButton.Style = buttonStyle;
                loadButton.Style = buttonStyle;
                resetButton.Style = buttonStyle;
            }

            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(loadButton);
            buttonPanel.Children.Add(resetButton);

            return buttonPanel;
        }

        /// <summary>
        /// Tries to find a resource in Application.Current.Resources.
        /// Returns null if not found or Application.Current is null.
        /// </summary>
        private object TryFindResource(string key)
        {
            try
            {
                return Application.Current?.TryFindResource(key);
            }
            catch
            {
                return null;
            }
        }
    }
}

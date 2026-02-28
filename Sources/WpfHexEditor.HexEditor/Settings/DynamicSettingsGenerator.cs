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
using WpfHexEditor.HexEditor.Settings.Controls;
using WpfHexEditor.ColorPicker.Controls;
using WpfHexEditor.HexEditor.Converters;

namespace WpfHexEditor.Core.Settings
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
        /// Supports hierarchical categories (e.g., "Colors.Selection" creates nested expanders).
        /// Returns a StackPanel containing all UI elements.
        /// </summary>
        public StackPanel GenerateSettingsPanel()
        {
            var rootPanel = new StackPanel { Margin = new Thickness(16) };

            // 1. Header
            rootPanel.Children.Add(CreateHeader());

            // 2. Group properties by hierarchical categories
            var hierarchy = _discoveryService.GroupByHierarchy();

            // 3. Create an Expander for each parent category
            // Sort parent categories by custom order
            var sortedParents = hierarchy.Keys
                .OrderBy(cat => GetCategoryOrder(cat))
                .ThenBy(cat => cat);

            foreach (var parentCategory in sortedParents)
            {
                var subcategories = hierarchy[parentCategory];
                var expander = CreateHierarchicalCategoryExpander(parentCategory, subcategories);
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
            var resource = TryFindResource(resourceKey);
            if (resource != null)
            {
                expander.SetResourceReference(Expander.HeaderProperty, resourceKey);
            }
            else
            {
                // Fallback if resource doesn't exist
                expander.Header = GetCategoryHeader(category);
            }

            var contentPanel = new StackPanel
            {
                Margin = new Thickness(16, 8, 0, 0)
            };

            // Generate controls for each property in this category
            foreach (var property in properties)
            {
                // Include read-only properties but make them disabled
                var controlElement = GeneratePropertyControl(property);
                if (controlElement != null)
                    contentPanel.Children.Add(controlElement);
            }

            expander.Content = contentPanel;
            return expander;
        }

        /// <summary>
        /// Creates a hierarchical Expander for a parent category with optional subcategories.
        /// If subcategories exist, creates nested Expanders within the parent.
        /// </summary>
        private Expander CreateHierarchicalCategoryExpander(string parentCategory, Dictionary<string, List<PropertyMetadata>> subcategories)
        {
            var expander = new Expander
            {
                IsExpanded = (parentCategory == "Display"), // Display category open by default
                Margin = new Thickness(0, 0, 0, 8),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200))
            };

            // Set header for parent category
            var resourceKey = $"HexSettings_{parentCategory}_Title";
            var resource = TryFindResource(resourceKey);
            if (resource != null)
            {
                expander.SetResourceReference(Expander.HeaderProperty, resourceKey);
            }
            else
            {
                expander.Header = GetCategoryHeader(parentCategory);
            }

            var contentPanel = new StackPanel
            {
                Margin = new Thickness(16, 8, 0, 0)
            };

            // Check if there are direct properties (no subcategory) and subcategories
            var hasDirectProperties = subcategories.ContainsKey(string.Empty) && subcategories[string.Empty].Any();
            var hasSubcategories = subcategories.Keys.Any(k => !string.IsNullOrEmpty(k));

            // Add direct properties first (properties without subcategory)
            if (hasDirectProperties)
            {
                foreach (var property in subcategories[string.Empty])
                {
                    var controlElement = GeneratePropertyControl(property);
                    if (controlElement != null)
                        contentPanel.Children.Add(controlElement);
                }

                // Add separator if we also have subcategories
                if (hasSubcategories)
                {
                    contentPanel.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 8) });
                }
            }

            // Create nested Expanders for subcategories
            if (hasSubcategories)
            {
                // Sort subcategories alphabetically
                var sortedSubcategories = subcategories.Keys
                    .Where(k => !string.IsNullOrEmpty(k))
                    .OrderBy(k => GetSubcategoryOrder(k))
                    .ThenBy(k => k);

                foreach (var subcategoryName in sortedSubcategories)
                {
                    var subExpander = CreateSubcategoryExpander(parentCategory, subcategoryName, subcategories[subcategoryName]);
                    contentPanel.Children.Add(subExpander);
                }
            }

            expander.Content = contentPanel;
            return expander;
        }

        /// <summary>
        /// Creates a nested Expander for a subcategory within a parent category.
        /// </summary>
        private Expander CreateSubcategoryExpander(string parentCategory, string subcategoryName, List<PropertyMetadata> properties)
        {
            var expander = new Expander
            {
                IsExpanded = true, // Subcategories expanded by default
                Margin = new Thickness(0, 0, 0, 8),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                Background = new SolidColorBrush(Color.FromRgb(250, 250, 250))
            };

            // Try to use localized resource, fallback to formatted name
            var resourceKey = $"HexSettings_{parentCategory}_{subcategoryName}_Title";
            var resource = TryFindResource(resourceKey);
            if (resource != null)
            {
                expander.SetResourceReference(Expander.HeaderProperty, resourceKey);
            }
            else
            {
                expander.Header = GetSubcategoryHeader(subcategoryName);
            }

            var contentPanel = new StackPanel
            {
                Margin = new Thickness(16, 8, 0, 8)
            };

            // Generate controls for each property in this subcategory
            foreach (var property in properties)
            {
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

                // 2. Create the WPF control
                var control = propertyControl.CreateControl();

                // 3. Create binding
                var binding = propertyControl.CreateBinding(metadata);

                // 4. Apply binding and configure control based on type
                return ConfigureControl(control, binding, metadata, propertyControl);
            }
            catch (Exception ex)
            {
                // Return error placeholder
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
            // Adjust binding mode for read-only properties
            if (metadata.IsReadOnly)
            {
                binding.Mode = BindingMode.OneWay;
            }

            // Disable control if property is read-only
            if (metadata.IsReadOnly && control is Control ctrl)
            {
                ctrl.IsEnabled = false;
                ctrl.Opacity = 0.6; // Visual indication that it's read-only
            }

            // CheckBox (bool properties)
            if (control is CheckBox checkBox)
            {
                checkBox.SetBinding(CheckBox.IsCheckedProperty, binding);

                // Try to use localized resource for CheckBox content
                var resourceKey = $"HexSettings_{metadata.PropertyName}";
                try
                {
                    var resource = TryFindResource(resourceKey);
                    if (resource != null)
                    {
                        checkBox.SetResourceReference(System.Windows.Controls.ContentControl.ContentProperty, resourceKey);
                    }
                    else
                    {
                        checkBox.Content = metadata.GetDisplayName();
                    }
                }
                catch
                {
                    checkBox.Content = metadata.GetDisplayName();
                }

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

                    // CRITICAL FIX: Use SelectedValue instead of SelectedIndex
                    // Tag now contains the int value directly (8, 16, 24, 32), no converter needed
                    // Direct binding: HexEditor.BytePerLine (32) ↔ ComboBox.SelectedValue (32) ↔ Item.Tag (32)
                    comboBox.SetBinding(ComboBox.SelectedValueProperty, binding);
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
                        var valueBinding = new Binding("Value")
                        {
                            Source = slider
                        };

                        // Use ZoomToPercentConverter for ZoomScale property
                        if (metadata.PropertyName == "ZoomScale")
                        {
                            valueBinding.Converter = new ZoomToPercentConverter();
                        }
                        else
                        {
                            valueBinding.StringFormat = "{0:F2}";
                        }

                        textBlock.SetBinding(TextBlock.TextProperty, valueBinding);
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

            // ColorPicker (Color properties)
            if (control is WpfHexEditor.ColorPicker.Controls.ColorPicker colorPicker)
            {
                // CRITICAL FIX: ColorPicker sets its own DataContext internally, breaking inheritance.
                // RelativeSource with fixed level doesn't work due to variable nesting (subcategories).
                // Solution: Find DataContext dynamically when control is loaded.

                var propName = metadata.PropertyName;
                var converter = binding.Converter;

                colorPicker.Loaded += (s, e) =>
                {
                    if (s is WpfHexEditor.ColorPicker.Controls.ColorPicker cp)
                    {
                        // Walk up visual tree to find first element with DataContext
                        DependencyObject parent = System.Windows.Media.VisualTreeHelper.GetParent(cp);
                        while (parent != null)
                        {
                            if (parent is FrameworkElement fe && fe.DataContext != null)
                            {
                                // Found! Bind directly to this DataContext
                                var directBinding = new Binding(propName)
                                {
                                    Source = fe.DataContext,
                                    Mode = BindingMode.TwoWay,
                                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                                    Converter = converter
                                };
                                cp.SetBinding(WpfHexEditor.ColorPicker.Controls.ColorPicker.SelectedColorProperty, directBinding);
                                return;
                            }
                            parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
                        }
                    }
                };

                return CreateControlWithLabel(propertyControl.CreateLabel(metadata), colorPicker);
            }

            // Border placeholder for ColorPicker (will be replaced in HexEditorSettings.xaml.cs)
            if (control is Border border)
            {
                if (border.Tag?.ToString() == "ColorPicker")
                {
                    // Mark with property name for later replacement
                    border.Tag = $"ColorPicker:{metadata.PropertyName}";

                    var wrapper = CreateControlWithLabel(propertyControl.CreateLabel(metadata), border);

                    return wrapper;
                }
            }

            // Default: just return the control
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
                "ScrollMarkers" => "📍 Scroll Markers",
                "Behavior" => "⚙️ Behavior",
                "Data" => "💾 Data",
                "Visual" => "👁️ Visual",
                "Keyboard" => "⌨️ Keyboard & Mouse",
                "TBL" => "📝 Character Table",
                "BarChart" => "📈 Bar Chart",
                _ => category
            };
        }

        /// <summary>
        /// Gets the display name for a subcategory with emoji icons.
        /// </summary>
        private string GetSubcategoryHeader(string subcategory)
        {
            return subcategory switch
            {
                "Selection" => "🎯 Selection",
                "ByteStates" => "🔄 Byte States",
                "Foreground" => "✍️ Foreground",
                "CharacterTable" => "📝 Character Table (TBL)",
                "Charts" => "📊 Charts",
                "Legacy" => "⚠️ Legacy (Compatibility)",
                _ => subcategory
            };
        }

        /// <summary>
        /// Gets the sort order for subcategories within Colors category.
        /// </summary>
        private int GetSubcategoryOrder(string subcategory)
        {
            return subcategory switch
            {
                "Selection" => 1,
                "ByteStates" => 2,
                "Foreground" => 3,
                "CharacterTable" => 4,
                "Charts" => 5,
                "Legacy" => 99, // Legacy last
                _ => 50 // Unknown subcategories in middle
            };
        }

        /// <summary>
        /// Gets the sort order for parent categories.
        /// </summary>
        private int GetCategoryOrder(string category)
        {
            return category switch
            {
                "StatusBar" => 1,
                "Display" => 2,
                "Colors" => 3,
                "ScrollMarkers" => 4,
                "Behavior" => 5,
                "Data" => 6,
                "Visual" => 7,
                "Keyboard" => 8,
                "BarChart" => 9,
                _ => 99 // Unknown categories last
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

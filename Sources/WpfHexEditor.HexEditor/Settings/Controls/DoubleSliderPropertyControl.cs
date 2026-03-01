//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

// Apache 2.0 - 2026
// Property Discovery and Auto-Generation System
// Author: Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WpfHexEditor.Core.Settings;
using PropertyMetadata = WpfHexEditor.Core.Settings.PropertyMetadata;

namespace WpfHexEditor.HexEditor.Settings.Controls
{
    /// <summary>
    /// Control generator for double properties (Slider + TextBlock display).
    /// Example: ZoomScale, LongProcessProgress
    /// </summary>
    public class DoubleSliderPropertyControl : IPropertyControl
    {
        public FrameworkElement CreateControl()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Slider
            var slider = new Slider
            {
                Minimum = 0.0,
                Maximum = 1.0,
                TickFrequency = 0.1,
                IsSnapToTickEnabled = false,
                Margin = new Thickness(0, 0, 0, 4),
                Name = "PropertySlider"
            };
            Grid.SetRow(slider, 0);

            // TextBlock for value display
            var textBlock = new TextBlock
            {
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8),
                Name = "PropertyValueDisplay"
            };
            Grid.SetRow(textBlock, 1);

            grid.Children.Add(slider);
            grid.Children.Add(textBlock);

            return grid;
        }

        public Binding CreateBinding(PropertyMetadata metadata)
        {
            return new Binding(metadata.PropertyName)
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
        }

        public TextBlock CreateLabel(PropertyMetadata metadata)
        {
            return new TextBlock
            {
                Text = metadata.GetDisplayName(),
                FontSize = 11,
                Margin = new Thickness(0, 8, 0, 4)
            };
        }
    }
}

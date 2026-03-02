//////////////////////////////////////////////
// Apache 2.0 - 2021
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Reflection;
using System.Windows.Media;

namespace WpfHexEditor.Core
{
    public static class RandomBrushes
    {
        /// <summary>
        /// Pick a random bruch
        /// </summary>
        public static SolidColorBrush PickBrush()
        {
            var properties = typeof(Brushes).GetProperties();

            return (SolidColorBrush)properties
                [
                    new Random().Next(properties.Length)
                ].GetValue(null, null);
        }
    }
}

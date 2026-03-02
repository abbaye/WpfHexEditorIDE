//////////////////////////////////////////////
// Apache 2.0  - 2016-2020
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;

namespace WpfHexEditor.Core.Extensions
{
    public static class DoubleExtension
    {
        public static double Round(this double s, int digit = 2) => Math.Round(s, digit);
    }
}

//////////////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project: WpfHexEditor.App
// File: BinaryAnalysis/Services/EntropyColorMapper.cs
// Description: Maps a Shannon entropy score (0–8) to a WPF Color.
//              Three themes: BlueRed (forensics), Greyscale, TrafficLight.
//              Pure static, no WPF UI types beyond System.Windows.Media.Color.
//////////////////////////////////////////////////////

using System.Windows.Media;

namespace WpfHexEditor.App.BinaryAnalysis.Services;

/// <summary>Visual theme used by the entropy heatmap overlay.</summary>
public enum EntropyColorTheme
{
    BlueRed,
    Greyscale,
    TrafficLight,
}

/// <summary>Maps entropy scores to colors for the heatmap overlay.</summary>
public static class EntropyColorMapper
{
    // Entropy 0–8 → color.  t = entropy / 8.0  (0.0=low, 1.0=high)

    public static Color Map(double entropy, EntropyColorTheme theme)
    {
        double t = Math.Clamp(entropy / 8.0, 0.0, 1.0);
        return theme switch
        {
            EntropyColorTheme.Greyscale    => MapGreyscale(t),
            EntropyColorTheme.TrafficLight => MapTrafficLight(t),
            _                              => MapBlueRed(t),
        };
    }

    // Blue (cold, low entropy) → Red (hot, high entropy)
    private static Color MapBlueRed(double t)
    {
        // 0.0 → #2255CC (blue)  0.5 → #22AA44 (green)  1.0 → #CC2222 (red)
        byte r, g, b;
        if (t < 0.5)
        {
            double u = t * 2.0;          // 0→1 over first half
            r = Lerp(0x22, 0x22, u);
            g = Lerp(0x55, 0xAA, u);
            b = Lerp(0xCC, 0x44, u);
        }
        else
        {
            double u = (t - 0.5) * 2.0; // 0→1 over second half
            r = Lerp(0x22, 0xCC, u);
            g = Lerp(0xAA, 0x22, u);
            b = Lerp(0x44, 0x22, u);
        }
        return Color.FromRgb(r, g, b);
    }

    // Dark grey (low) → White (high)
    private static Color MapGreyscale(double t)
    {
        byte v = Lerp(0x20, 0xFF, t);
        return Color.FromRgb(v, v, v);
    }

    // Green (low) → Yellow (medium) → Red (high)
    private static Color MapTrafficLight(double t)
    {
        byte r, g;
        if (t < 0.5)
        {
            double u = t * 2.0;
            r = Lerp(0x22, 0xDD, u);
            g = Lerp(0xAA, 0xDD, u);
        }
        else
        {
            double u = (t - 0.5) * 2.0;
            r = Lerp(0xDD, 0xCC, u);
            g = Lerp(0xDD, 0x22, u);
        }
        return Color.FromRgb(r, g, 0x22);
    }

    private static byte Lerp(byte from, byte to, double t)
        => (byte)(from + (to - from) * t);
}

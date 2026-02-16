//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Text;
using WpfHexaEditor.Core;
using WpfHexaEditor.Core.Bytes;
using WpfHexaEditor.Core.Platform.Input;
using WpfHexaEditor.Core.Platform.Media;

namespace WpfHexaEditor.Core.Example;

/// <summary>
/// Console example demonstrating WpfHexaEditor.Core platform-agnostic functionality.
/// This example shows that the Core library has zero UI dependencies and can run anywhere.
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine("  WpfHexaEditor.Core - Platform-Agnostic Example");
        Console.WriteLine("=================================================\n");

        // Example 1: ByteProvider - Core binary data handling
        DemonstrateByteProvider();

        // Example 2: PlatformColor - Cross-platform color parsing
        DemonstratePlatformColor();

        // Example 3: KeyValidator - Platform-agnostic key validation
        DemonstrateKeyValidator();

        Console.WriteLine("\n=================================================");
        Console.WriteLine("  ✓ All examples completed successfully!");
        Console.WriteLine("  This demonstrates that Core library works");
        Console.WriteLine("  without any WPF/Avalonia dependencies.");
        Console.WriteLine("=================================================");
    }

    static void DemonstrateByteProvider()
    {
        Console.WriteLine("1️⃣  ByteProvider Example");
        Console.WriteLine("   -------------------");

        // Create a byte array with some data
        byte[] data = Encoding.UTF8.GetBytes("Hello, WpfHexaEditor!");
        Console.WriteLine($"   Original data: {Encoding.UTF8.GetString(data)}");
        Console.WriteLine($"   Byte array: {BitConverter.ToString(data).Replace("-", " ")}");

        // Create a ByteProvider (platform-agnostic way to handle binary data)
        var byteProvider = new ByteProvider();
        byteProvider.OpenMemory(data);
        Console.WriteLine($"   ByteProvider Length: {byteProvider.Length} bytes");

        // Read a byte at position 0
        var (firstByte, success) = byteProvider.GetByte(0);
        if (success)
        {
            Console.WriteLine($"   First byte at position 0: 0x{firstByte:X2} ('{(char)firstByte}')");
        }

        // Modify a byte (change 'H' to 'h')
        byteProvider.ModifyByte(0, (byte)'h');
        var (modifiedByte, _) = byteProvider.GetByte(0);
        Console.WriteLine($"   After modification: 0x{modifiedByte:X2} ('{(char)modifiedByte}')");

        // Find all occurrences of 'e' (need to pass as byte array)
        var positions = byteProvider.FindAll(new byte[] { (byte)'e' }).ToList();
        Console.WriteLine($"   Found '{(char)'e'}' at positions: {string.Join(", ", positions)}");

        Console.WriteLine();
    }

    static void DemonstratePlatformColor()
    {
        Console.WriteLine("2️⃣  PlatformColor Example");
        Console.WriteLine("   ---------------------");

        // Parse hex colors (works on any platform)
        var red = PlatformColor.Parse("#FF0000");
        Console.WriteLine($"   Parsed '#FF0000': R={red.R}, G={red.G}, B={red.B}, A={red.A}");

        var transparentBlue = PlatformColor.Parse("#8000FF00");
        Console.WriteLine($"   Parsed '#8000FF00': R={transparentBlue.R}, G={transparentBlue.G}, B={transparentBlue.B}, A={transparentBlue.A}");

        // Create colors from components
        var customColor = PlatformColor.FromArgb(255, 100, 150, 200);
        Console.WriteLine($"   FromArgb(255,100,150,200): R={customColor.R}, G={customColor.G}, B={customColor.B}, A={customColor.A}");

        // Predefined colors
        Console.WriteLine($"   Black: R={PlatformColor.Black.R}, G={PlatformColor.Black.G}, B={PlatformColor.Black.B}");
        Console.WriteLine($"   White: R={PlatformColor.White.R}, G={PlatformColor.White.G}, B={PlatformColor.White.B}");

        // Color equality
        var red2 = PlatformColor.FromRgb(255, 0, 0);
        Console.WriteLine($"   Red == Red2: {red == red2}");

        Console.WriteLine();
    }

    static void DemonstrateKeyValidator()
    {
        Console.WriteLine("3️⃣  KeyValidator Example");
        Console.WriteLine("   --------------------");

        // Test numeric keys
        var testKeys = new[]
        {
            PlatformKey.D5,
            PlatformKey.A,
            PlatformKey.F,
            PlatformKey.G,
            PlatformKey.NumPad3,
            PlatformKey.Up,
            PlatformKey.Enter
        };

        foreach (var key in testKeys)
        {
            var isNumeric = KeyValidator.IsNumericKey(key);
            var isHex = KeyValidator.IsHexKey(key);
            var isArrow = KeyValidator.IsArrowKey(key);

            Console.Write($"   {key,-12}: ");

            if (isNumeric)
            {
                var digit = KeyValidator.GetDigitFromKey(key);
                Console.Write($"Numeric (value: {digit}) ");
            }

            if (isHex)
                Console.Write("Hex ");

            if (isArrow)
                Console.Write("Arrow ");

            if (!isNumeric && !isHex && !isArrow)
                Console.Write("Other");

            Console.WriteLine();
        }

        Console.WriteLine();
    }
}

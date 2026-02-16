using WpfHexaEditor.Core.Platform.Media;

namespace WpfHexaEditor.Core.Tests;

public class PlatformColorTests
{
    [Fact]
    public void Parse_ValidHexColor6Digits_ReturnsCorrectColor()
    {
        // Arrange
        var hexColor = "#FF0000";

        // Act
        var result = PlatformColor.Parse(hexColor);

        // Assert
        Assert.Equal(255, result.R);
        Assert.Equal(0, result.G);
        Assert.Equal(0, result.B);
        Assert.Equal(255, result.A);
    }

    [Fact]
    public void Parse_ValidHexColor8Digits_ReturnsCorrectColor()
    {
        // Arrange
        var hexColor = "#80FF0000"; // 50% transparent red

        // Act
        var result = PlatformColor.Parse(hexColor);

        // Assert
        Assert.Equal(128, result.A); // 0x80 = 128
        Assert.Equal(255, result.R);
        Assert.Equal(0, result.G);
        Assert.Equal(0, result.B);
    }

    [Fact]
    public void Parse_InvalidHexColor_ReturnsBlack()
    {
        // Arrange
        var hexColor = "invalid";

        // Act
        var result = PlatformColor.Parse(hexColor);

        // Assert
        Assert.Equal(PlatformColor.Black, result);
    }

    [Fact]
    public void FromArgb_CreatesCorrectColor()
    {
        // Act
        var result = PlatformColor.FromArgb(128, 255, 128, 64);

        // Assert
        Assert.Equal(128, result.A);
        Assert.Equal(255, result.R);
        Assert.Equal(128, result.G);
        Assert.Equal(64, result.B);
    }

    [Fact]
    public void FromRgb_CreatesOpaqueColor()
    {
        // Act
        var result = PlatformColor.FromRgb(100, 150, 200);

        // Assert
        Assert.Equal(255, result.A); // Opaque
        Assert.Equal(100, result.R);
        Assert.Equal(150, result.G);
        Assert.Equal(200, result.B);
    }

    [Fact]
    public void Equality_SameColors_ReturnsTrue()
    {
        // Arrange
        var color1 = PlatformColor.FromArgb(255, 100, 150, 200);
        var color2 = PlatformColor.FromArgb(255, 100, 150, 200);

        // Act & Assert
        Assert.Equal(color1, color2);
        Assert.True(color1 == color2);
    }

    [Fact]
    public void Equality_DifferentColors_ReturnsFalse()
    {
        // Arrange
        var color1 = PlatformColor.FromRgb(255, 0, 0);
        var color2 = PlatformColor.FromRgb(0, 255, 0);

        // Act & Assert
        Assert.NotEqual(color1, color2);
        Assert.True(color1 != color2);
    }

    [Theory]
    [InlineData("#FFFFFF", 255, 255, 255, 255)] // White
    [InlineData("#000000", 0, 0, 0, 255)]       // Black
    [InlineData("#00FF00", 0, 255, 0, 255)]     // Green
    public void Parse_VariousColors_ParsesCorrectly(string hex, byte r, byte g, byte b, byte a)
    {
        // Act
        var result = PlatformColor.Parse(hex);

        // Assert
        Assert.Equal(r, result.R);
        Assert.Equal(g, result.G);
        Assert.Equal(b, result.B);
        Assert.Equal(a, result.A);
    }
}

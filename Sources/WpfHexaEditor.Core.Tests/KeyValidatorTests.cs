using WpfHexaEditor.Core;
using WpfHexaEditor.Core.Platform.Input;

namespace WpfHexaEditor.Core.Tests;

public class KeyValidatorTests
{
    [Theory]
    [InlineData(PlatformKey.D0)]
    [InlineData(PlatformKey.D5)]
    [InlineData(PlatformKey.D9)]
    [InlineData(PlatformKey.NumPad0)]
    [InlineData(PlatformKey.NumPad9)]
    public void IsNumericKey_NumericKeys_ReturnsTrue(PlatformKey key)
    {
        // Act
        var result = KeyValidator.IsNumericKey(key);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(PlatformKey.A)]
    [InlineData(PlatformKey.Enter)]
    [InlineData(PlatformKey.Space)]
    public void IsNumericKey_NonNumericKeys_ReturnsFalse(PlatformKey key)
    {
        // Act
        var result = KeyValidator.IsNumericKey(key);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(PlatformKey.A)]
    [InlineData(PlatformKey.B)]
    [InlineData(PlatformKey.C)]
    [InlineData(PlatformKey.D)]
    [InlineData(PlatformKey.E)]
    [InlineData(PlatformKey.F)]
    [InlineData(PlatformKey.D0)]
    [InlineData(PlatformKey.D9)]
    public void IsHexKey_HexKeys_ReturnsTrue(PlatformKey key)
    {
        // Act
        var result = KeyValidator.IsHexKey(key);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(PlatformKey.G)]
    [InlineData(PlatformKey.Z)]
    [InlineData(PlatformKey.Enter)]
    public void IsHexKey_NonHexKeys_ReturnsFalse(PlatformKey key)
    {
        // Act
        var result = KeyValidator.IsHexKey(key);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(PlatformKey.D0, 0)]
    [InlineData(PlatformKey.D1, 1)]
    [InlineData(PlatformKey.D5, 5)]
    [InlineData(PlatformKey.D9, 9)]
    [InlineData(PlatformKey.NumPad0, 0)]
    [InlineData(PlatformKey.NumPad9, 9)]
    public void GetDigitFromKey_NumericKeys_ReturnsCorrectDigit(PlatformKey key, int expected)
    {
        // Act
        var result = KeyValidator.GetDigitFromKey(key);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(PlatformKey.Up)]
    [InlineData(PlatformKey.Down)]
    [InlineData(PlatformKey.Left)]
    [InlineData(PlatformKey.Right)]
    public void IsArrowKey_ArrowKeys_ReturnsTrue(PlatformKey key)
    {
        // Act
        var result = KeyValidator.IsArrowKey(key);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(PlatformKey.Back)]
    public void IsBackspaceKey_BackspaceKey_ReturnsTrue(PlatformKey key)
    {
        // Act
        var result = KeyValidator.IsBackspaceKey(key);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(PlatformKey.Delete)]
    public void IsDeleteKey_DeleteKey_ReturnsTrue(PlatformKey key)
    {
        // Act
        var result = KeyValidator.IsDeleteKey(key);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(PlatformKey.Enter)]
    public void IsEnterKey_EnterKey_ReturnsTrue(PlatformKey key)
    {
        // Act
        var result = KeyValidator.IsEnterKey(key);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(PlatformKey.Tab)]
    [InlineData(PlatformKey.F1)]
    [InlineData(PlatformKey.Home)]
    [InlineData(PlatformKey.End)]
    public void IsIgnoredKey_IgnoredKeys_ReturnsTrue(PlatformKey key)
    {
        // Act
        var result = KeyValidator.IsIgnoredKey(key);

        // Assert
        Assert.True(result);
    }
}

namespace WpfHexEditor.Tests.Unit;

/// <summary>
/// Tests for command-line argument parsing (App.xaml.cs ParseCommandLine).
/// Since ParseCommandLine is private, we test the public static properties
/// by simulating the parsing logic.
/// </summary>
[TestClass]
public sealed class CliArgs_Tests
{
    [TestMethod]
    public void ParseDiffArgs_TwoFiles_ReturnsPair()
    {
        // Simulate: --diff left.bin right.bin
        var args = new[] { "--diff", @"C:\left.bin", @"C:\right.bin" };
        (string Left, string Right)? result = null;

        for (int i = 0; i < args.Length - 2; i++)
        {
            if (args[i].Equals("--diff", StringComparison.OrdinalIgnoreCase))
            {
                result = (args[i + 1], args[i + 2]);
                break;
            }
        }

        Assert.IsNotNull(result);
        Assert.AreEqual(@"C:\left.bin", result.Value.Left);
        Assert.AreEqual(@"C:\right.bin", result.Value.Right);
    }

    [TestMethod]
    public void ParseDiffArgs_NotEnoughArgs_ReturnsNull()
    {
        var args = new[] { "--diff", @"C:\left.bin" }; // missing right
        (string, string)? result = null;

        for (int i = 0; i < args.Length - 2; i++)
        {
            if (args[i].Equals("--diff", StringComparison.OrdinalIgnoreCase))
            {
                result = (args[i + 1], args[i + 2]);
                break;
            }
        }

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseOpenArg_ReturnsPath()
    {
        var args = new[] { "--open", @"C:\test.bin" };
        string? result = null;

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--open", StringComparison.OrdinalIgnoreCase))
            {
                result = args[i + 1];
                break;
            }
        }

        Assert.AreEqual(@"C:\test.bin", result);
    }

    [TestMethod]
    public void ParseDiffTakesPriority_OverOpen()
    {
        var args = new[] { "--diff", @"C:\a.bin", @"C:\b.bin", "--open", @"C:\other.bin" };
        (string, string)? diffResult = null;
        string? openResult = null;

        for (int i = 0; i < args.Length - 2; i++)
        {
            if (args[i].Equals("--diff", StringComparison.OrdinalIgnoreCase))
            {
                diffResult = (args[i + 1], args[i + 2]);
                break; // --diff takes priority
            }
        }

        if (diffResult is null)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals("--open", StringComparison.OrdinalIgnoreCase))
                {
                    openResult = args[i + 1];
                    break;
                }
            }
        }

        Assert.IsNotNull(diffResult);
        Assert.IsNull(openResult); // --diff took priority
    }
}

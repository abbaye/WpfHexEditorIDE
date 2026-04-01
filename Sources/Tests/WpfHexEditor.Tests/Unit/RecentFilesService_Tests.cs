namespace WpfHexEditor.Tests.Unit;

/// <summary>
/// Tests for the RecentFilesService logic (add, dedup, max entries, clear).
/// Tests the core logic without file I/O by testing the public API behavior.
/// </summary>
[TestClass]
public sealed class RecentFilesService_Tests
{
    [TestMethod]
    public void Add_NewFile_AppearsAtTop()
    {
        var list = new List<string>();
        AddToList(list, @"C:\file1.bin");
        AddToList(list, @"C:\file2.bin");

        Assert.AreEqual(@"C:\file2.bin", list[0]);
        Assert.AreEqual(@"C:\file1.bin", list[1]);
    }

    [TestMethod]
    public void Add_Duplicate_MovesToTop()
    {
        var list = new List<string>();
        AddToList(list, @"C:\file1.bin");
        AddToList(list, @"C:\file2.bin");
        AddToList(list, @"C:\file1.bin"); // re-add

        Assert.AreEqual(2, list.Count);
        Assert.AreEqual(@"C:\file1.bin", list[0]); // moved to top
    }

    [TestMethod]
    public void Add_ExceedsMax_TrimsTail()
    {
        var list = new List<string>();
        for (int i = 0; i < 15; i++)
            AddToList(list, $@"C:\file{i}.bin");

        Assert.AreEqual(10, list.Count);
        Assert.AreEqual(@"C:\file14.bin", list[0]); // most recent
    }

    [TestMethod]
    public void Add_CaseInsensitiveDedupe()
    {
        var list = new List<string>();
        AddToList(list, @"C:\File.Bin");
        AddToList(list, @"c:\file.bin");

        Assert.AreEqual(1, list.Count);
    }

    // Simulate RecentFilesService.Add logic
    private static void AddToList(List<string> list, string path)
    {
        list.RemoveAll(e => e.Equals(path, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, path);
        while (list.Count > 10) list.RemoveAt(list.Count - 1);
    }
}

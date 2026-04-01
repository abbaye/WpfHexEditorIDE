using WpfHexEditor.Core.Diff.Algorithms;
using WpfHexEditor.Core.Diff.Models;

namespace WpfHexEditor.Core.Diff.Tests;

[TestClass]
public sealed class BinaryDiffAlgorithm_Tests
{
    private readonly BinaryDiffAlgorithm _algo = new();

    [TestMethod]
    public void IdenticalBytes_ReturnsNoRegions()
    {
        byte[] data = [0x00, 0x01, 0x02, 0x03];
        var result = _algo.ComputeBytes(data, data);

        Assert.AreEqual(0, result.Regions.Count);
        Assert.AreEqual(0, result.Stats.TotalRegions);
        Assert.AreEqual(1.0, result.Stats.Similarity, 0.001);
    }

    [TestMethod]
    public void EmptyInputs_ReturnsEmpty()
    {
        var result = _algo.ComputeBytes([], []);

        Assert.AreEqual(0, result.Regions.Count);
        Assert.AreEqual(1.0, result.Stats.Similarity, 0.001);
    }

    [TestMethod]
    public void SingleByteDifference_ReturnsOneModifiedRegion()
    {
        byte[] left  = [0x00, 0x01, 0x02];
        byte[] right = [0x00, 0xFF, 0x02];

        var result = _algo.ComputeBytes(left, right);

        Assert.AreEqual(1, result.Regions.Count);
        Assert.AreEqual(BinaryRegionKind.Modified, result.Regions[0].Kind);
        Assert.AreEqual(1, result.Regions[0].LeftOffset);
        Assert.AreEqual(1, result.Regions[0].Length);
    }

    [TestMethod]
    public void ContiguousDifferences_GroupedIntoOneRegion()
    {
        byte[] left  = [0x00, 0x01, 0x02, 0x03];
        byte[] right = [0x00, 0xAA, 0xBB, 0x03];

        var result = _algo.ComputeBytes(left, right);

        Assert.AreEqual(1, result.Regions.Count);
        Assert.AreEqual(2, result.Regions[0].Length);
        Assert.AreEqual(1, result.Regions[0].LeftOffset);
    }

    [TestMethod]
    public void RightLonger_ReturnsInsertedInRightRegion()
    {
        byte[] left  = [0x00, 0x01];
        byte[] right = [0x00, 0x01, 0x02, 0x03];

        var result = _algo.ComputeBytes(left, right);

        Assert.AreEqual(1, result.Regions.Count);
        Assert.AreEqual(BinaryRegionKind.InsertedInRight, result.Regions[0].Kind);
        Assert.AreEqual(2, result.Regions[0].Length);
        Assert.AreEqual(2, result.Stats.InsertedBytes);
    }

    [TestMethod]
    public void LeftLonger_ReturnsDeletedInRightRegion()
    {
        byte[] left  = [0x00, 0x01, 0x02, 0x03];
        byte[] right = [0x00, 0x01];

        var result = _algo.ComputeBytes(left, right);

        Assert.AreEqual(1, result.Regions.Count);
        Assert.AreEqual(BinaryRegionKind.DeletedInRight, result.Regions[0].Kind);
        Assert.AreEqual(2, result.Regions[0].Length);
        Assert.AreEqual(2, result.Stats.DeletedBytes);
    }

    [TestMethod]
    public void MultipleDisjointDifferences_ReturnsMultipleRegions()
    {
        byte[] left  = [0x00, 0x01, 0x02, 0x03, 0x04];
        byte[] right = [0xFF, 0x01, 0x02, 0xEE, 0x04];

        var result = _algo.ComputeBytes(left, right);

        Assert.AreEqual(2, result.Regions.Count);
        Assert.AreEqual(0, result.Regions[0].LeftOffset);
        Assert.AreEqual(3, result.Regions[1].LeftOffset);
    }

    [TestMethod]
    public void StatsAreAccurate()
    {
        byte[] left  = [0x00, 0x01, 0x02, 0x03];
        byte[] right = [0xFF, 0x01, 0xEE, 0x03, 0xAA];

        var result = _algo.ComputeBytes(left, right);

        Assert.AreEqual(4, result.Stats.LeftFileSize);
        Assert.AreEqual(5, result.Stats.RightFileSize);
        Assert.AreEqual(2, result.Stats.ModifiedCount);
        Assert.AreEqual(2, result.Stats.ModifiedBytes);
        Assert.AreEqual(1, result.Stats.InsertedCount);
        Assert.AreEqual(1, result.Stats.InsertedBytes);
    }

    [TestMethod]
    public void RegionBytes_ContainActualDifference()
    {
        byte[] left  = [0x00, 0xAA, 0x02];
        byte[] right = [0x00, 0xBB, 0x02];

        var result = _algo.ComputeBytes(left, right);

        CollectionAssert.AreEqual(new byte[] { 0xAA }, result.Regions[0].LeftBytes);
        CollectionAssert.AreEqual(new byte[] { 0xBB }, result.Regions[0].RightBytes);
    }
}

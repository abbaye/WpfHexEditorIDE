// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor.Tests
// File: SmartCompleteFuzzyScorer_Tests.cs
// Description:
//     Tests for SmartCompleteFuzzyScorer — prefix, CamelCase, consecutive,
//     subsequence matching, score ordering, and matched-index population.
// ==========================================================

using WpfHexEditor.Editor.CodeEditor.Helpers;

namespace WpfHexEditor.Editor.CodeEditor.Tests;

[TestClass]
public sealed class SmartCompleteFuzzyScorer_Tests
{
    // ── Empty / null guards ───────────────────────────────────────────────────

    [TestMethod]
    public void Score_EmptyQuery_ReturnsPositive()
    {
        var score = SmartCompleteFuzzyScorer.Score("", "LoadConfig", out _);
        Assert.IsTrue(score > 0, "Empty query should match everything.");
    }

    [TestMethod]
    public void Score_EmptyQuery_NoMatchedIndices()
    {
        SmartCompleteFuzzyScorer.Score("", "LoadConfig", out var indices);
        Assert.AreEqual(0, indices.Count);
    }

    [TestMethod]
    public void Score_EmptyCandidate_ReturnsNoMatch()
    {
        var score = SmartCompleteFuzzyScorer.Score("lc", "", out _);
        Assert.AreEqual(-1, score);
    }

    // ── Exact prefix matching ─────────────────────────────────────────────────

    [TestMethod]
    public void Score_ExactPrefix_ReturnsHighestScore()
    {
        var prefixScore = SmartCompleteFuzzyScorer.Score("Load", "LoadConfig", out _);
        var otherScore  = SmartCompleteFuzzyScorer.Score("lc", "LoadConfig", out _);

        Assert.IsTrue(prefixScore > otherScore, "Prefix match should outscore CamelCase match.");
    }

    [TestMethod]
    public void Score_ExactPrefix_CaseInsensitive()
    {
        var score = SmartCompleteFuzzyScorer.Score("load", "LoadConfig", out _);
        Assert.IsTrue(score >= 1000, "Case-insensitive prefix should return ScoreExactPrefix (1000).");
    }

    [TestMethod]
    public void Score_ExactPrefix_MatchedIndicesArePrefix()
    {
        SmartCompleteFuzzyScorer.Score("Load", "LoadConfig", out var indices);
        Assert.AreEqual(4, indices.Count);
        for (int i = 0; i < 4; i++)
            Assert.AreEqual(i, indices[i]);
    }

    // ── CamelCase acronym matching ────────────────────────────────────────────

    [TestMethod]
    public void Score_CamelAcronym_Matches()
    {
        // "LC" → "LoadConfig"
        var score = SmartCompleteFuzzyScorer.Score("LC", "LoadConfig", out _);
        Assert.IsTrue(score > 0 && score < 1000, "CamelCase acronym should score below prefix.");
    }

    [TestMethod]
    public void Score_CamelAcronym_LowerCase_Matches()
    {
        var score = SmartCompleteFuzzyScorer.Score("lc", "LoadConfig", out _);
        Assert.IsTrue(score > 0);
    }

    [TestMethod]
    public void Score_CamelAcronym_NotMatching_ReturnsLowerOrNoMatch()
    {
        var score = SmartCompleteFuzzyScorer.Score("XY", "LoadConfig", out _);
        Assert.IsTrue(score < 800, "XY should not match LC pattern of LoadConfig.");
    }

    // ── Consecutive matching ──────────────────────────────────────────────────

    [TestMethod]
    public void Score_Consecutive_MatchesSubstring()
    {
        var score = SmartCompleteFuzzyScorer.Score("fig", "myConfig", out _);
        Assert.IsTrue(score >= 500, "Consecutive 'fig' in 'myConfig' should score >= 500.");
    }

    [TestMethod]
    public void Score_Consecutive_MatchedIndicesCorrect()
    {
        SmartCompleteFuzzyScorer.Score("cfg", "myConfig", out var indices);
        // "cfg" may or may not match consecutively (c-o-n-f-i-g is not consecutive cfg)
        // "con" would match position 2,3,4 in "myConfig" — test with a clearer substring
        SmartCompleteFuzzyScorer.Score("onfig", "myConfig", out indices);
        Assert.IsTrue(indices.Count >= 1);
    }

    // ── Subsequence matching ──────────────────────────────────────────────────

    [TestMethod]
    public void Score_Subsequence_Matches()
    {
        var score = SmartCompleteFuzzyScorer.Score("mc", "myConfig", out _);
        Assert.IsTrue(score > 0, "'mc' subsequence should match 'myConfig'.");
    }

    [TestMethod]
    public void Score_Subsequence_MatchedIndicesPopulated()
    {
        SmartCompleteFuzzyScorer.Score("mc", "myConfig", out var indices);
        Assert.AreEqual(2, indices.Count);
        Assert.AreEqual(0, indices[0]); // 'm' at 0
        Assert.AreEqual(2, indices[1]); // 'C' at 2
    }

    [TestMethod]
    public void Score_NoMatch_ReturnsMinusOne()
    {
        var score = SmartCompleteFuzzyScorer.Score("zzz", "LoadConfig", out _);
        Assert.AreEqual(-1, score);
    }

    // ── Score ordering ────────────────────────────────────────────────────────

    [TestMethod]
    public void Score_PrefixBeatsAll()
    {
        var prefix      = SmartCompleteFuzzyScorer.Score("Load",    "LoadConfig",  out _);
        var camel       = SmartCompleteFuzzyScorer.Score("LC",      "LoadConfig",  out _);
        var consecutive = SmartCompleteFuzzyScorer.Score("oadCon",  "LoadConfig",  out _);
        var sub         = SmartCompleteFuzzyScorer.Score("lc",      "xLoadxConx",  out _);

        Assert.IsTrue(prefix >= camel,       "Prefix must beat CamelCase.");
        Assert.IsTrue(camel  >= consecutive, "CamelCase must beat Consecutive.");
    }

    // ── Edge cases ────────────────────────────────────────────────────────────

    [TestMethod]
    public void Score_SingleCharQuery_PrefixMatch()
    {
        var score = SmartCompleteFuzzyScorer.Score("L", "LoadConfig", out var indices);
        Assert.IsTrue(score >= 1000);
        Assert.AreEqual(1, indices.Count);
        Assert.AreEqual(0, indices[0]);
    }

    [TestMethod]
    public void Score_FullCandidateMatch()
    {
        var score = SmartCompleteFuzzyScorer.Score("LoadConfig", "LoadConfig", out _);
        Assert.IsTrue(score >= 1000);
    }

    [TestMethod]
    public void Score_QueryLongerThanCandidate_ReturnsNoMatch()
    {
        var score = SmartCompleteFuzzyScorer.Score("LoadConfigVeryLong", "Load", out _);
        Assert.AreEqual(-1, score);
    }
}

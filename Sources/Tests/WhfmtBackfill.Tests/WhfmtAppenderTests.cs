using System.Text.Json;
using WhfmtBackfill.Emit;

namespace WhfmtBackfill.Tests;

[TestClass]
public sealed class WhfmtAppenderTests
{
    [TestMethod]
    public void Append_inserts_before_closing_brace_with_trailing_comma()
    {
        const string input = """
{
  "formatId": "PNG",
  "version": "1.0"
}
""";
        string updated = WhfmtAppender.Append(input, ["  \"diff\": { \"keyFields\": [\"width\"] }"]);

        // Result must be valid JSON
        using var doc = JsonDocument.Parse(updated);
        Assert.IsTrue(doc.RootElement.TryGetProperty("formatId", out _));
        Assert.IsTrue(doc.RootElement.TryGetProperty("version",  out _));
        Assert.IsTrue(doc.RootElement.TryGetProperty("diff",     out var diffEl));
        Assert.IsTrue(diffEl.TryGetProperty("keyFields", out _));
    }

    [TestMethod]
    public void Append_multiple_fragments_remain_valid_json()
    {
        const string input = """
{
  "formatId": "X"
}
""";
        string updated = WhfmtAppender.Append(input, [
            "  \"diff\":   { \"keyFields\": [\"a\"] }",
            "  \"repair\": [{ \"name\": \"R1\" }]",
            "  \"fuzz\":   { \"strategies\": [] }",
        ]);

        using var doc = JsonDocument.Parse(updated);
        Assert.IsTrue(doc.RootElement.TryGetProperty("diff",   out _));
        Assert.IsTrue(doc.RootElement.TryGetProperty("repair", out _));
        Assert.IsTrue(doc.RootElement.TryGetProperty("fuzz",   out _));
    }

    [TestMethod]
    public void Append_preserves_existing_text_byte_for_byte()
    {
        const string input = "{\n  \"formatId\": \"X\"\n}";
        string updated = WhfmtAppender.Append(input, ["  \"diff\": {}"]);

        // Original prefix must still appear verbatim in the output
        Assert.IsTrue(updated.StartsWith("{\n  \"formatId\": \"X\""), $"Got: {updated}");
    }

    [TestMethod]
    public void Append_with_empty_fragments_returns_original()
    {
        const string input = "{\"a\":1}";
        Assert.AreEqual(input, WhfmtAppender.Append(input, Array.Empty<string>()));
    }

    [TestMethod]
    public void FindFinalClosingBrace_handles_trailing_whitespace()
    {
        Assert.AreEqual(7,  WhfmtAppender.FindFinalClosingBrace("{ \"a\":1}"));
        Assert.AreEqual(7,  WhfmtAppender.FindFinalClosingBrace("{ \"a\":1}    \n"));
        Assert.AreEqual(-1, WhfmtAppender.FindFinalClosingBrace("not-json"));
    }
}

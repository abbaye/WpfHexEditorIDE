// ==========================================================
// Project: whfmt.Analysis.Tests
// File: DiffRendererTests.cs
// Description: Unit tests for DiffRenderer — all output formats.
// ==========================================================

namespace WhfmtAnalysis.Tests;

[TestClass]
public sealed class DiffRendererTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static DiffResult BuildIdenticalResult() => new()
    {
        FileA       = "a.png",
        FileB       = "b.png",
        SizeA       = 20,
        SizeB       = 20,
        FormatName  = "Test PNG-like",
        FormatsMatch = true,
        IsIdentical = true,
        KeyFields   = ["magic", "image_width"],
        IgnoreFields = ["padding"],
    };

    private static DiffResult BuildChangedResult()
    {
        var r = new DiffResult
        {
            FileA        = "old.png",
            FileB        = "new.png",
            SizeA        = 20,
            SizeB        = 20,
            FormatName   = "Test PNG-like",
            FormatsMatch = true,
            IsIdentical  = false,
            RawByteDelta = 0,
            KeyFields    = ["image_width", "image_height"],
            IgnoreFields = ["padding"],
        };
        r.FieldChanges.Add(new FieldChange { FieldName = "image_width",  ValueA = "640",  ValueB = "1920", IsChanged = true  });
        r.FieldChanges.Add(new FieldChange { FieldName = "image_height", ValueA = "480",  ValueB = "1080", IsChanged = true  });
        r.FieldChanges.Add(new FieldChange { FieldName = "padding",      ValueA = "000000", ValueB = "FFFFFF", IsIgnored = true });
        return r;
    }

    private static DiffResult BuildErrorResult() => new()
    {
        FileA = "a.bin",
        FileB = "b.bin",
        Error = "Could not detect format for either file.",
    };

    // ── RenderText ────────────────────────────────────────────────────────────

    [TestMethod]
    public void RenderText_identical_contains_identical_keyword()
    {
        var text = DiffRenderer.RenderText(BuildIdenticalResult());
        StringAssert.Contains(text, "IDENTICAL");
    }

    [TestMethod]
    public void RenderText_changed_result_lists_field_names()
    {
        var text = DiffRenderer.RenderText(BuildChangedResult());
        StringAssert.Contains(text, "image_width");
        StringAssert.Contains(text, "1920");
    }

    [TestMethod]
    public void RenderText_error_result_contains_error_keyword()
    {
        var text = DiffRenderer.RenderText(BuildErrorResult());
        StringAssert.Contains(text, "ERROR");
    }

    [TestMethod]
    public void RenderText_shows_format_name()
    {
        var text = DiffRenderer.RenderText(BuildChangedResult());
        StringAssert.Contains(text, "Test PNG-like");
    }

    // ── RenderJson ────────────────────────────────────────────────────────────

    [TestMethod]
    public void RenderJson_produces_valid_json()
    {
        var json = DiffRenderer.RenderJson(BuildChangedResult());
        // Should not throw
        var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.IsNotNull(doc);
    }

    [TestMethod]
    public void RenderJson_contains_field_changes_array()
    {
        var json = DiffRenderer.RenderJson(BuildChangedResult());
        StringAssert.Contains(json, "fields");
    }

    [TestMethod]
    public void RenderJson_identical_result_is_valid_json()
    {
        var json = DiffRenderer.RenderJson(BuildIdenticalResult());
        var doc  = System.Text.Json.JsonDocument.Parse(json);
        Assert.IsNotNull(doc);
    }

    // ── ToCsv ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public void ToCsv_contains_header_row()
    {
        var csv = DiffRenderer.ToCsv(BuildChangedResult());
        StringAssert.Contains(csv, "Field");
    }

    [TestMethod]
    public void ToCsv_contains_field_data()
    {
        var csv = DiffRenderer.ToCsv(BuildChangedResult());
        StringAssert.Contains(csv, "image_width");
    }

    [TestMethod]
    public void ToCsv_identical_result_still_has_header()
    {
        var csv = DiffRenderer.ToCsv(BuildIdenticalResult());
        Assert.IsFalse(string.IsNullOrWhiteSpace(csv));
    }

    // ── ToMarkdown ────────────────────────────────────────────────────────────

    [TestMethod]
    public void ToMarkdown_contains_table_pipes()
    {
        var md = DiffRenderer.ToMarkdown(BuildChangedResult());
        StringAssert.Contains(md, "|");
    }

    [TestMethod]
    public void ToMarkdown_contains_field_names()
    {
        var md = DiffRenderer.ToMarkdown(BuildChangedResult());
        StringAssert.Contains(md, "image_width");
    }

    [TestMethod]
    public void ToMarkdown_identical_result_non_empty()
    {
        var md = DiffRenderer.ToMarkdown(BuildIdenticalResult());
        Assert.IsFalse(string.IsNullOrWhiteSpace(md));
    }

    // ── RenderHtml ────────────────────────────────────────────────────────────

    [TestMethod]
    public void RenderHtml_produces_html_document()
    {
        var html = DiffRenderer.RenderHtml(BuildChangedResult());
        StringAssert.Contains(html, "<html");
        StringAssert.Contains(html, "</html>");
    }

    [TestMethod]
    public void RenderHtml_contains_field_names()
    {
        var html = DiffRenderer.RenderHtml(BuildChangedResult());
        StringAssert.Contains(html, "image_width");
    }

    [TestMethod]
    public void RenderHtml_identical_contains_identical_marker()
    {
        var html = DiffRenderer.RenderHtml(BuildIdenticalResult());
        StringAssert.Contains(html, "IDENTICAL");
    }

    [TestMethod]
    public void RenderHtml_error_result_contains_error_section()
    {
        var html = DiffRenderer.RenderHtml(BuildErrorResult());
        StringAssert.Contains(html, "error");
    }

    // ── ChecksumStatus section ────────────────────────────────────────────────

    [TestMethod]
    public void RenderText_with_checksums_contains_algorithm_name()
    {
        var r = BuildChangedResult();
        r.ChecksumsA.Add(new ChecksumStatus { Algorithm = "CRC32", StoredOffset = 16, StoredHex = "AABBCCDD", ComputedHex = "AABBCCDD", IsValid = true });
        var text = DiffRenderer.RenderText(r);
        StringAssert.Contains(text, "CRC32");
    }

    [TestMethod]
    public void RenderText_invalid_checksum_contains_invalid_marker()
    {
        var r = BuildChangedResult();
        r.ChecksumsB.Add(new ChecksumStatus { Algorithm = "CRC32", StoredOffset = 16, StoredHex = "DEADBEEF", ComputedHex = "AABBCCDD", IsValid = false });
        var text = DiffRenderer.RenderText(r);
        // Renderer uses ✗ for invalid checksums
        StringAssert.Contains(text, "✗");
    }
}

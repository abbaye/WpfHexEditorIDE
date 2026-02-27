using System.IO;
using System.Text;
using WpfHexaEditor.Core.CharacterTable;
using WpfHexEditor.TblEditor.Models;

namespace WpfHexEditor.TblEditor.Services;

/// <summary>Service for managing TBL templates (built-in and user-defined)</summary>
public class TblTemplateService
{
    private readonly string _userTemplatesPath;
    private List<TblTemplate>? _cachedTemplates;

    public TblTemplateService()
    {
        _userTemplatesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WpfHexEditor", "TblTemplates");
        EnsureUserTemplatesDirectory();
    }

    public List<TblTemplate> GetAllTemplates()
    {
        if (_cachedTemplates != null) return _cachedTemplates;
        var templates = new List<TblTemplate>();
        templates.AddRange(GetBuiltInTemplates());
        templates.AddRange(LoadUserTemplates());
        _cachedTemplates = templates;
        return templates;
    }

    public List<TblTemplate> GetTemplatesByCategory(string category) =>
        GetAllTemplates().Where(t => t.Category?.Equals(category, StringComparison.OrdinalIgnoreCase) == true).ToList();

    public List<string> GetCategories() =>
        GetAllTemplates().Select(t => t.Category ?? "").Distinct().OrderBy(c => c).ToList();

    public void SaveAsTemplate(TblStream tbl, string name, string? description, string? category)
    {
        var template = new TblTemplate
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Description = description,
            Category = category,
            Author = Environment.UserName,
            IsBuiltIn = false,
            CreatedDate = DateTime.Now,
            TblContent = ExportTblToString(tbl)
        };
        SaveUserTemplate(template);
        _cachedTemplates = null;
    }

    public bool DeleteTemplate(string templateId)
    {
        var filePath = Path.Combine(_userTemplatesPath, $"{templateId}.tbltemplate");
        if (!File.Exists(filePath)) return false;
        File.Delete(filePath);
        _cachedTemplates = null;
        return true;
    }

    public void InvalidateCache() => _cachedTemplates = null;

    private List<TblTemplate> GetBuiltInTemplates() =>
    [
        new() { Id = "ascii-standard",       Name = "ASCII Standard",                    Description = "Standard ASCII table (0x20-0x7E)",                  Category = "Standard",      DefaultType = DefaultCharacterTableType.Ascii,                IsBuiltIn = true },
        new() { Id = "ebcdic",               Name = "EBCDIC",                            Description = "Extended Binary Coded Decimal Interchange Code",     Category = "Standard",      DefaultType = DefaultCharacterTableType.EbcdicWithSpecialChar, IsBuiltIn = true },
        new() { Id = "nes-default",          Name = "NES Default",                       Description = "Standard NES character encoding (shifted ASCII)",    Category = "Game Systems",  TblContent = GenerateNesDefaultTbl(),                        IsBuiltIn = true },
        new() { Id = "snes-default",         Name = "SNES Default",                      Description = "Standard SNES character encoding (ASCII direct)",   Category = "Game Systems",  TblContent = GenerateSnesDefaultTbl(),                       IsBuiltIn = true },
        new() { Id = "latin1",               Name = "Latin-1 (ISO-8859-1)",              Description = "Western European character set",                    Category = "Unicode",       TblContent = GenerateLatin1Tbl(),                            IsBuiltIn = true },
        new() { Id = "katakana-halfwidth",   Name = "Japanese Katakana (Half-width)",     Description = "Half-width Katakana characters (U+FF61-U+FF9F)",   Category = "Unicode",       TblContent = GenerateKatakanaTbl(),                          IsBuiltIn = true },
    ];

    private string GenerateNesDefaultTbl()
    {
        var sb = new StringBuilder("# NES Default Character Table\n\n");
        for (int i = 0; i < 26; i++) sb.AppendLine($"{i:X2}={(char)('A' + i)}");
        for (int i = 0; i < 26; i++) sb.AppendLine($"{(i + 0x1A):X2}={(char)('a' + i)}");
        for (int i = 0; i < 10; i++) sb.AppendLine($"{(i + 0x34):X2}={(char)('0' + i)}");
        sb.AppendLine("3E= "); sb.AppendLine("3F=!"); sb.AppendLine("40=?"); sb.AppendLine("41=."); sb.AppendLine("42=,");
        sb.AppendLine("/50=<END>"); sb.AppendLine("*51=<NL>");
        return sb.ToString();
    }

    private string GenerateSnesDefaultTbl()
    {
        var sb = new StringBuilder("# SNES Default Character Table\n\n");
        for (int i = 0x20; i <= 0x7E; i++) sb.AppendLine($"{i:X2}={(char)i}");
        sb.AppendLine("/00=<END>"); sb.AppendLine("*0A=<NL>");
        return sb.ToString();
    }

    private string GenerateLatin1Tbl()
    {
        var sb = new StringBuilder("# Latin-1 Character Table\n\n");
        for (int i = 0x20; i <= 0x7E; i++) sb.AppendLine($"{i:X2}={(char)i}");
        for (int i = 0xA0; i <= 0xFF; i++) sb.AppendLine($"{i:X2}={(char)i}");
        return sb.ToString();
    }

    private string GenerateKatakanaTbl()
    {
        var sb = new StringBuilder("# Japanese Katakana (Half-width) Character Table\n\n");
        for (int i = 0; i < 63; i++) sb.AppendLine($"{(0xA1 + i):X2}={(char)(0xFF61 + i)}");
        return sb.ToString();
    }

    private List<TblTemplate> LoadUserTemplates()
    {
        var templates = new List<TblTemplate>();
        if (!Directory.Exists(_userTemplatesPath)) return templates;
        foreach (var file in Directory.GetFiles(_userTemplatesPath, "*.tbltemplate"))
        {
            try { var t = LoadTemplateFromFile(file); if (t != null) templates.Add(t); }
            catch { /* Skip invalid */ }
        }
        return templates;
    }

    private void SaveUserTemplate(TblTemplate template)
    {
        EnsureUserTemplatesDirectory();
        SaveTemplateToFile(template, Path.Combine(_userTemplatesPath, $"{template.Id}.tbltemplate"));
    }

    private TblTemplate? LoadTemplateFromFile(string filePath)
    {
        var template = new TblTemplate();
        var tblContent = new StringBuilder();
        bool inTblSection = false;
        foreach (var line in File.ReadAllLines(filePath))
        {
            if (line.StartsWith("[TEMPLATE]")) inTblSection = false;
            else if (line.StartsWith("[TBL]")) inTblSection = true;
            else if (inTblSection) tblContent.AppendLine(line);
            else if (line.StartsWith("Id=")) template.Id = line[3..];
            else if (line.StartsWith("Name=")) template.Name = line[5..];
            else if (line.StartsWith("Description=")) template.Description = line[12..];
            else if (line.StartsWith("Author=")) template.Author = line[7..];
            else if (line.StartsWith("Category=")) template.Category = line[9..];
        }
        template.TblContent = tblContent.ToString();
        template.IsBuiltIn = false;
        return template;
    }

    private void SaveTemplateToFile(TblTemplate template, string filePath)
    {
        var sb = new StringBuilder("[TEMPLATE]\n");
        sb.AppendLine($"Id={template.Id}");
        sb.AppendLine($"Name={template.Name}");
        sb.AppendLine($"Description={template.Description ?? ""}");
        sb.AppendLine($"Author={template.Author}");
        sb.AppendLine($"Category={template.Category ?? "Custom"}");
        sb.AppendLine(); sb.AppendLine("[TBL]"); sb.Append(template.TblContent);
        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    private string ExportTblToString(TblStream tbl)
    {
        var sb = new StringBuilder();
        foreach (var dte in tbl.GetAllEntries().OrderBy(d => d.Entry))
        {
            var v = dte.Value.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
            if (dte.Type == DteType.EndBlock) sb.AppendLine($"/{dte.Entry}={v}");
            else if (dte.Type == DteType.EndLine) sb.AppendLine($"*{dte.Entry}={v}");
            else sb.AppendLine($"{dte.Entry}={v}");
        }
        return sb.ToString();
    }

    private void EnsureUserTemplatesDirectory()
    {
        if (!Directory.Exists(_userTemplatesPath)) Directory.CreateDirectory(_userTemplatesPath);
    }
}

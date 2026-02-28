namespace WpfHexEditor.Editor.TblEditor.Models;

/// <summary>Result of TBL entry validation</summary>
public class TblValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public string? WarningMessage { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];

    public static TblValidationResult Success() => new() { IsValid = true };
    public static TblValidationResult Error(string message) => new() { IsValid = false, ErrorMessage = message };
    public static TblValidationResult Warning(string message) => new() { IsValid = true, WarningMessage = message };
}

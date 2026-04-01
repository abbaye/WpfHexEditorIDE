//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.TblEditor.Models;

/// <summary>
/// Result of TBL entry validation
/// </summary>
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

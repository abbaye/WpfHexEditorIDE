// ==========================================================
// Project: WpfHexEditor.Plugins.ParsedFields
// File: Properties/ParsedFieldsResources.Designer.cs
// Description: Strongly-typed resource class for the Parsed Fields plugin.
// Architecture: Standard ResX pattern — do not edit manually.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Plugins.ParsedFields.Properties;

internal static class ParsedFieldsResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Plugins.ParsedFields.Properties.ParsedFieldsResources",
                typeof(ParsedFieldsResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    /// <summary>Looks up a localized string similar to "Invalid offset. Use hex (0x1A2B) or decimal (6699).".</summary>
    internal static string ParsedFields_Error_InvalidOffset
        => ResourceManager.GetString("ParsedFields_Error_InvalidOffset", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Auto".</summary>
    internal static string ParsedFields_Label_AutoMode
        => ResourceManager.GetString("ParsedFields_Label_AutoMode", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "This field cannot be edited.".</summary>
    internal static string ParsedFields_Error_FieldNotEditable
        => ResourceManager.GetString("ParsedFields_Error_FieldNotEditable", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Invalid value for this field type.".</summary>
    internal static string ParsedFields_Error_InvalidValue
        => ResourceManager.GetString("ParsedFields_Error_InvalidValue", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Export Error".</summary>
    internal static string ParsedFields_Dialog_ExportError
        => ResourceManager.GetString("ParsedFields_Dialog_ExportError", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Export Complete".</summary>
    internal static string ParsedFields_Dialog_ExportComplete
        => ResourceManager.GetString("ParsedFields_Dialog_ExportComplete", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Copied".</summary>
    internal static string ParsedFields_Dialog_Copied
        => ResourceManager.GetString("ParsedFields_Dialog_Copied", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Field Not Found".</summary>
    internal static string ParsedFields_Dialog_FieldNotFound
        => ResourceManager.GetString("ParsedFields_Dialog_FieldNotFound", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Find Similar".</summary>
    internal static string ParsedFields_Dialog_FindSimilar
        => ResourceManager.GetString("ParsedFields_Dialog_FindSimilar", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Feature Unavailable".</summary>
    internal static string ParsedFields_Dialog_FeatureUnavailable
        => ResourceManager.GetString("ParsedFields_Dialog_FeatureUnavailable", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Delete this field?".</summary>
    internal static string ParsedFields_Confirm_DeleteField
        => ResourceManager.GetString("ParsedFields_Confirm_DeleteField", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Delete all fields?".</summary>
    internal static string ParsedFields_Confirm_DeleteAll
        => ResourceManager.GetString("ParsedFields_Confirm_DeleteAll", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Data Inspector feature not yet connected to this panel.".</summary>
    internal static string ParsedFields_DataInspectorNotConnected
        => ResourceManager.GetString("ParsedFields_DataInspectorNotConnected", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Parsed Fields".</summary>
    internal static string ParsedFields_PluginName
        => ResourceManager.GetString("ParsedFields_PluginName", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Jump to Offset".</summary>
    internal static string ParsedFields_JumpToOffset
        => ResourceManager.GetString("ParsedFields_JumpToOffset", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Offset (hex 0x… or decimal):".</summary>
    internal static string ParsedFields_JumpToOffset_Label
        => ResourceManager.GetString("ParsedFields_JumpToOffset_Label", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "0x".</summary>
    internal static string ParsedFields_JumpToOffset_Placeholder
        => ResourceManager.GetString("ParsedFields_JumpToOffset_Placeholder", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "OK".</summary>
    internal static string ParsedFields_Dialog_OK
        => ResourceManager.GetString("ParsedFields_Dialog_OK", _resourceCulture)!;

    /// <summary>Looks up a localized string similar to "Invalid Input".</summary>
    internal static string ParsedFields_Error_InvalidInput
        => ResourceManager.GetString("ParsedFields_Error_InvalidInput", _resourceCulture)!;
}

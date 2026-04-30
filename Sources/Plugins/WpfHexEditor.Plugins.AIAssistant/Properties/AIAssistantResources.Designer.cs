// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: Properties/AIAssistantResources.Designer.cs
// Description: Auto-generated strongly-typed resource class for AI Assistant plugin.
// Architecture: Standard ResX pattern — do not edit manually.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Plugins.AIAssistant.Properties;

internal static class AIAssistantResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Plugins.AIAssistant.Properties.AIAssistantResources",
                typeof(AIAssistantResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    /// <summary>Localized: "Connection Test"</summary>
    internal static string AIAssistant_ConnectionTest_Title
        => ResourceManager.GetString("AIAssistant_ConnectionTest_Title", _resourceCulture)!;

    /// <summary>Localized: "Testing..."</summary>
    internal static string AIAssistant_ConnectionTest_Testing
        => ResourceManager.GetString("AIAssistant_ConnectionTest_Testing", _resourceCulture)!;

    /// <summary>Localized: "Provider '{0}' is not registered."</summary>
    internal static string AIAssistant_Error_ProviderNotRegistered
        => ResourceManager.GetString("AIAssistant_Error_ProviderNotRegistered", _resourceCulture)!;

    /// <summary>Localized: "Connection to {0} successful!"</summary>
    internal static string AIAssistant_ConnectionTest_Success
        => ResourceManager.GetString("AIAssistant_ConnectionTest_Success", _resourceCulture)!;

    /// <summary>Localized: "Connection to {0} failed. Check your API key and settings."</summary>
    internal static string AIAssistant_ConnectionTest_Failure
        => ResourceManager.GetString("AIAssistant_ConnectionTest_Failure", _resourceCulture)!;

    /// <summary>Localized: "Connection test timed out after 10 seconds."</summary>
    internal static string AIAssistant_Error_Timeout
        => ResourceManager.GetString("AIAssistant_Error_Timeout", _resourceCulture)!;

    /// <summary>Localized: "Network error: {0}"</summary>
    internal static string AIAssistant_Error_NetworkError
        => ResourceManager.GetString("AIAssistant_Error_NetworkError", _resourceCulture)!;

    /// <summary>Localized: "Error: {0}"</summary>
    internal static string AIAssistant_Error_Generic
        => ResourceManager.GetString("AIAssistant_Error_Generic", _resourceCulture)!;

    /// <summary>Localized: "Enable Thinking (Anthropic only)"</summary>
    internal static string AIAssistant_EnableThinking
        => ResourceManager.GetString("AIAssistant_EnableThinking", _resourceCulture)!;

    /// <summary>Localized: "Copied!"</summary>
    internal static string AIAssistant_Copied
        => ResourceManager.GetString("AIAssistant_Copied", _resourceCulture)!;

    /// <summary>Localized: "Copy code"</summary>
    internal static string AIAssistant_CopyCode
        => ResourceManager.GetString("AIAssistant_CopyCode", _resourceCulture)!;

    /// <summary>Localized: "Testing..."</summary>
    internal static string AIAssistant_Testing
        => ResourceManager.GetString("AIAssistant_Testing", _resourceCulture)!;

    /// <summary>Localized: "Connected"</summary>
    internal static string AIAssistant_Connected
        => ResourceManager.GetString("AIAssistant_Connected", _resourceCulture)!;

    /// <summary>Localized: "Connection failed"</summary>
    internal static string AIAssistant_ConnectionFailed
        => ResourceManager.GetString("AIAssistant_ConnectionFailed", _resourceCulture)!;

    /// <summary>Localized: "AI Assistant"</summary>
    internal static string AIAssistant_PluginName
        => ResourceManager.GetString("AIAssistant_PluginName", _resourceCulture)!;

    /// <summary>Localized: "Rename Conversation"</summary>
    internal static string AIAssistant_RenameConversation
        => ResourceManager.GetString("AIAssistant_RenameConversation", _resourceCulture)!;
}

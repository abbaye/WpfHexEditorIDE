// ==========================================================
// Project: WpfHexEditor.Core.Debugger
// File: Protocol/DapMessage.cs
// Description:
//     Base message types for the Debug Adapter Protocol (DAP).
//     DAP uses JSON-RPC-like framing over stdio:
//     "Content-Length: N\r\n\r\n{json}"
// Architecture: Pure domain — no WPF, no UI.
// ==========================================================

using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfHexEditor.Core.Debugger.Protocol;

/// <summary>DAP message discriminator.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DapMessageType { request, response, @event }

/// <summary>Root DAP message. All messages share this envelope.</summary>
public class DapMessage
{
    [JsonPropertyName("seq")]  public int            Seq  { get; set; }
    [JsonPropertyName("type")] public DapMessageType Type { get; set; }
}

/// <summary>A DAP request sent from client to adapter.</summary>
public class DapRequest : DapMessage
{
    public DapRequest() => Type = DapMessageType.request;

    [JsonPropertyName("command")]   public string          Command   { get; set; } = string.Empty;
    [JsonPropertyName("arguments")] public JsonElement?    Arguments { get; set; }
}

/// <summary>A DAP response returned from adapter to client.</summary>
public class DapResponse : DapMessage
{
    public DapResponse() => Type = DapMessageType.response;

    [JsonPropertyName("request_seq")] public int         RequestSeq { get; set; }
    [JsonPropertyName("success")]     public bool        Success    { get; set; }
    [JsonPropertyName("command")]     public string      Command    { get; set; } = string.Empty;
    [JsonPropertyName("message")]     public string?     Message    { get; set; }
    [JsonPropertyName("body")]        public JsonElement Body       { get; set; }
}

/// <summary>A DAP event pushed from adapter to client.</summary>
public class DapEvent : DapMessage
{
    public DapEvent() => Type = DapMessageType.@event;

    [JsonPropertyName("event")] public string       Event { get; set; } = string.Empty;
    [JsonPropertyName("body")]  public JsonElement? Body  { get; set; }
}

/// <summary>DAP stop reason values (used in StoppedEvent).</summary>
public static class StopReason
{
    public const string Step          = "step";
    public const string Breakpoint    = "breakpoint";
    public const string Exception     = "exception";
    public const string Pause         = "pause";
    public const string Entry         = "entry";
    public const string Goto          = "goto";
    public const string FunctionBreakpoint = "function breakpoint";
}

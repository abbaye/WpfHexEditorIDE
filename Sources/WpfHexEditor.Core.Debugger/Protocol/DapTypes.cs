// ==========================================================
// Project: WpfHexEditor.Core.Debugger
// File: Protocol/DapTypes.cs
// Description:
//     DAP DTO records for all protocol arguments and response bodies.
//     Names match the DAP specification exactly (camelCase for JSON).
// ==========================================================

using System.Text.Json.Serialization;

namespace WpfHexEditor.Core.Debugger.Protocol;

// ── Request arguments ────────────────────────────────────────────────────────

public record InitializeRequestArgs(
    [property: JsonPropertyName("adapterID")]          string  AdapterId,
    [property: JsonPropertyName("clientID")]           string  ClientId          = "WpfHexEditor",
    [property: JsonPropertyName("clientName")]         string  ClientName        = "WpfHexEditor Studio",
    [property: JsonPropertyName("linesStartAt1")]      bool    LinesStartAt1     = true,
    [property: JsonPropertyName("columnsStartAt1")]    bool    ColumnsStartAt1   = true,
    [property: JsonPropertyName("pathFormat")]         string  PathFormat        = "path",
    [property: JsonPropertyName("supportsRunInTerminalRequest")] bool SupportsRunInTerminal = false
);

public record LaunchRequestArgs(
    [property: JsonPropertyName("program")]            string  Program,
    [property: JsonPropertyName("args")]               string[]? Args           = null,
    [property: JsonPropertyName("cwd")]                string?   Cwd            = null,
    [property: JsonPropertyName("env")]                Dictionary<string,string>? Env = null,
    [property: JsonPropertyName("stopAtEntry")]        bool      StopAtEntry    = false,
    [property: JsonPropertyName("noDebug")]            bool      NoDebug        = false
);

public record AttachRequestArgs(
    [property: JsonPropertyName("processId")]          int     ProcessId
);

public record SetBreakpointsArgs(
    [property: JsonPropertyName("source")]             SourceDto Source,
    [property: JsonPropertyName("breakpoints")]        SourceBreakpointDto[] Breakpoints
);

public record SourceBreakpointDto(
    [property: JsonPropertyName("line")]               int     Line,
    [property: JsonPropertyName("condition")]          string? Condition = null
);

public record ContinueArgs(
    [property: JsonPropertyName("threadId")]           int ThreadId,
    [property: JsonPropertyName("singleThread")]       bool SingleThread = false
);

public record StepArgs(
    [property: JsonPropertyName("threadId")]           int    ThreadId,
    [property: JsonPropertyName("granularity")]        string Granularity = "statement"
);

public record StackTraceArgs(
    [property: JsonPropertyName("threadId")]           int     ThreadId,
    [property: JsonPropertyName("startFrame")]         int?    StartFrame = null,
    [property: JsonPropertyName("levels")]             int?    Levels     = null
);

public record ScopesArgs(
    [property: JsonPropertyName("frameId")]            int FrameId
);

public record VariablesArgs(
    [property: JsonPropertyName("variablesReference")] int VariablesReference,
    [property: JsonPropertyName("start")]              int? Start  = null,
    [property: JsonPropertyName("count")]              int? Count  = null
);

public record EvaluateArgs(
    [property: JsonPropertyName("expression")]         string  Expression,
    [property: JsonPropertyName("frameId")]            int?    FrameId = null,
    [property: JsonPropertyName("context")]            string  Context = "watch"
);

public record DisconnectArgs(
    [property: JsonPropertyName("restart")]            bool Restart        = false,
    [property: JsonPropertyName("terminateDebuggee")] bool TerminateDebuggee = true
);

// ── Response bodies ──────────────────────────────────────────────────────────

public record CapabilitiesBody(
    [property: JsonPropertyName("supportsConfigurationDoneRequest")] bool SupportsConfigDone,
    [property: JsonPropertyName("supportsEvaluateForHovers")]        bool SupportsEvalHover,
    [property: JsonPropertyName("supportsConditionalBreakpoints")]   bool SupportsConditional,
    [property: JsonPropertyName("supportsExceptionOptions")]         bool SupportsExceptions
);

public record SetBreakpointsBody(
    [property: JsonPropertyName("breakpoints")]        BreakpointDto[] Breakpoints
);

public record BreakpointDto(
    [property: JsonPropertyName("id")]                 int?    Id,
    [property: JsonPropertyName("verified")]           bool    Verified,
    [property: JsonPropertyName("line")]               int?    Line,
    [property: JsonPropertyName("message")]            string? Message = null
);

public record StackTraceBody(
    [property: JsonPropertyName("stackFrames")]        StackFrameDto[] StackFrames,
    [property: JsonPropertyName("totalFrames")]        int? TotalFrames = null
);

public record StackFrameDto(
    [property: JsonPropertyName("id")]                 int       Id,
    [property: JsonPropertyName("name")]               string    Name,
    [property: JsonPropertyName("source")]             SourceDto? Source,
    [property: JsonPropertyName("line")]               int       Line,
    [property: JsonPropertyName("column")]             int       Column
);

public record SourceDto(
    [property: JsonPropertyName("name")]               string? Name,
    [property: JsonPropertyName("path")]               string? Path
);

public record ScopesBody(
    [property: JsonPropertyName("scopes")]             ScopeDto[] Scopes
);

public record ScopeDto(
    [property: JsonPropertyName("name")]               string  Name,
    [property: JsonPropertyName("variablesReference")] int     VariablesReference,
    [property: JsonPropertyName("expensive")]          bool    Expensive,
    [property: JsonPropertyName("namedVariables")]     int?    NamedVariables = null
);

public record VariablesBody(
    [property: JsonPropertyName("variables")]          VariableDto[] Variables
);

public record VariableDto(
    [property: JsonPropertyName("name")]               string  Name,
    [property: JsonPropertyName("value")]              string  Value,
    [property: JsonPropertyName("type")]               string? Type,
    [property: JsonPropertyName("variablesReference")] int     VariablesReference,
    [property: JsonPropertyName("namedVariables")]     int?    NamedVariables = null
);

public record EvaluateBody(
    [property: JsonPropertyName("result")]             string  Result,
    [property: JsonPropertyName("type")]               string? Type,
    [property: JsonPropertyName("variablesReference")] int     VariablesReference
);

// ── Event bodies ─────────────────────────────────────────────────────────────

public record StoppedEventBody(
    [property: JsonPropertyName("reason")]             string  Reason,
    [property: JsonPropertyName("threadId")]           int?    ThreadId,
    [property: JsonPropertyName("description")]        string? Description  = null,
    [property: JsonPropertyName("text")]               string? Text         = null,
    [property: JsonPropertyName("allThreadsStopped")] bool    AllThreadsStopped = true
);

public record OutputEventBody(
    [property: JsonPropertyName("category")]           string  Category,
    [property: JsonPropertyName("output")]             string  Output,
    [property: JsonPropertyName("source")]             SourceDto? Source    = null,
    [property: JsonPropertyName("line")]               int?    Line         = null
);

public record ExitedEventBody(
    [property: JsonPropertyName("exitCode")]           int ExitCode
);

public record ThreadEventBody(
    [property: JsonPropertyName("threadId")]           int    ThreadId,
    [property: JsonPropertyName("reason")]             string Reason
);

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
    [property: JsonPropertyName("noDebug")]            bool      NoDebug        = false,
    [property: JsonPropertyName("justMyCode")]         bool      JustMyCode     = true
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

public record PauseArgs(
    [property: JsonPropertyName("threadId")]           int ThreadId
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

public record SetVariableArgs(
    [property: JsonPropertyName("variablesReference")] int    VariablesReference,
    [property: JsonPropertyName("name")]               string Name,
    [property: JsonPropertyName("value")]              string Value
);

public record GotoTargetsArgs(
    [property: JsonPropertyName("source")]             SourceDto Source,
    [property: JsonPropertyName("line")]               int       Line
);

public record GotoTargetsBody(
    [property: JsonPropertyName("targets")]            GotoTargetDto[] Targets
);

public record GotoTargetDto(
    [property: JsonPropertyName("id")]                 int    Id,
    [property: JsonPropertyName("label")]              string Label,
    [property: JsonPropertyName("line")]               int    Line
);

public record GotoArgs(
    [property: JsonPropertyName("threadId")]           int ThreadId,
    [property: JsonPropertyName("targetId")]           int TargetId
);

public record SetExceptionBreakpointsArgs(
    [property: JsonPropertyName("filters")]            string[]                  Filters,
    [property: JsonPropertyName("filterOptions")]      ExceptionFilterOptionsDto[]? FilterOptions = null
);

public record ExceptionFilterOptionsDto(
    [property: JsonPropertyName("filterId")]           string  FilterId,
    [property: JsonPropertyName("condition")]          string? Condition = null
);

public record ExceptionBreakpointsBody(
    [property: JsonPropertyName("exceptionBreakpointFilters")] ExceptionFilterDto[] Filters
);

public record ExceptionFilterDto(
    [property: JsonPropertyName("filter")]             string  Filter,
    [property: JsonPropertyName("label")]              string  Label,
    [property: JsonPropertyName("description")]        string? Description = null,
    [property: JsonPropertyName("default")]            bool    Default     = false,
    [property: JsonPropertyName("supportsCondition")]  bool    SupportsCondition = false
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

public record SetVariableBody(
    [property: JsonPropertyName("value")]              string  Value,
    [property: JsonPropertyName("type")]               string? Type = null,
    [property: JsonPropertyName("variablesReference")] int     VariablesReference = 0
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

// ── Threads request / response ────────────────────────────────────────────────

public record ThreadsBody(
    [property: JsonPropertyName("threads")]            ThreadDto[] Threads
);

public record ThreadDto(
    [property: JsonPropertyName("id")]                 int    Id,
    [property: JsonPropertyName("name")]               string Name
);

// ── Modules request / response ────────────────────────────────────────────────

public record ModulesArgs(
    [property: JsonPropertyName("startModule")] int StartModule = 0,
    [property: JsonPropertyName("moduleCount")] int ModuleCount = 100
);

public record ModulesBody(
    [property: JsonPropertyName("modules")]     ModuleDto[] Modules,
    [property: JsonPropertyName("totalModules")] int?       TotalModules = null
);

public record ModuleDto(
    [property: JsonPropertyName("id")]              object  Id,
    [property: JsonPropertyName("name")]            string  Name,
    [property: JsonPropertyName("path")]            string? Path          = null,
    [property: JsonPropertyName("isOptimized")]     bool?   IsOptimized   = null,
    [property: JsonPropertyName("isUserCode")]      bool?   IsUserCode    = null,
    [property: JsonPropertyName("version")]         string? Version       = null,
    [property: JsonPropertyName("symbolStatus")]    string? SymbolStatus  = null,
    [property: JsonPropertyName("symbolFilePath")]  string? SymbolFilePath= null,
    [property: JsonPropertyName("addressRange")]    string? AddressRange  = null
);

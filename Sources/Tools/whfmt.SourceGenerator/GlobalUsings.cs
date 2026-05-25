// Explicit global usings required for netstandard2.0 — the linked Generator source files
// were authored for net8.0 where these are provided by implicit global usings.
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;

// Polyfills for C# 9+ / .NET 5+ features unavailable in netstandard2.0.
// Only ParserGenerator and SpanGenerator are linked (C#-only targets).

namespace System.Runtime.CompilerServices
{
    // init-only properties (C# 9) — used by ParserGenerator.BlockDef / ChecksumDef
    internal static class IsExternalInit { }
}

// Convert.FromHexString shim is in GeneratorPolyfills.cs (namespace WhfmtCodeGen.Generator).

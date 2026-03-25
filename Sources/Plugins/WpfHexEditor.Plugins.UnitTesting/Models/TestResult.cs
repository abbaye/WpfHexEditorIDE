// ==========================================================
// Project: WpfHexEditor.Plugins.UnitTesting
// File: Models/TestResult.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Immutable data model for a single test case result parsed from TRX.
// ==========================================================

namespace WpfHexEditor.Plugins.UnitTesting.Models;

/// <summary>Outcome of a single test case.</summary>
public enum TestOutcome { Passed, Failed, Skipped, NotRun }

/// <summary>
/// Immutable result for one test case as parsed from a TRX report.
/// </summary>
public sealed record TestResult(
    string      TestName,
    string      ClassName,
    string      AssemblyName,
    TestOutcome Outcome,
    TimeSpan    Duration,
    string?     ErrorMessage,
    string?     StackTrace,
    string      ProjectName  = "",
    string?     SourceFile   = null,   // absolute path — from stack trace or project dir search
    int         SourceLine   = 0);     // 1-based; 0 = open at top

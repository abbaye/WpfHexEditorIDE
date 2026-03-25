// ==========================================================
// Project: WpfHexEditor.Plugins.UnitTesting
// File: Models/DiscoveredTest.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-24
// Description:
//     A test case discovered via dotnet test --list-tests (no outcome yet).
// ==========================================================

namespace WpfHexEditor.Plugins.UnitTesting.Models;

/// <summary>
/// A test case discovered via <c>dotnet test --list-tests</c>.
/// Has no outcome until the test is actually run.
/// </summary>
public sealed record DiscoveredTest(
    string ProjectName,
    string ClassName,
    string TestName);

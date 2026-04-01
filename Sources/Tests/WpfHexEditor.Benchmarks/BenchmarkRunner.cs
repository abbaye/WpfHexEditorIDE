// ==========================================================
// Project: WpfHexEditor.Benchmarks
// File: BenchmarkRunner.cs
// Description:
//     Entry point for all WpfHexEditor performance benchmarks.
//     Run in Release mode: dotnet run -c Release --project WpfHexEditor.Benchmarks
//
//     To run a specific benchmark class:
//       dotnet run -c Release -- --filter *ChecksumBenchmarks*
// ==========================================================

using BenchmarkDotNet.Running;

BenchmarkSwitcher
    .FromAssembly(typeof(BenchmarkRunner).Assembly)
    .RunAll();

internal static class BenchmarkRunner { }

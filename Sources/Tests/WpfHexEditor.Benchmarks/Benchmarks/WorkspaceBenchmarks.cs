// ==========================================================
// Project: WpfHexEditor.Benchmarks
// File: Benchmarks/WorkspaceBenchmarks.cs
// Description:
//     Benchmarks for workspace ZIP round-trip: WriteAsync + ReadAsync.
//     Uses a synthetic WorkspaceCapture with 20 open file paths.
//
// Baseline target: Save (20 files) < 300 ms, Load < 300 ms
// ==========================================================

using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using WpfHexEditor.Core.Workspaces;

namespace WpfHexEditor.Benchmarks.Benchmarks;

[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[BenchmarkCategory("Workspace")]
public class WorkspaceBenchmarks
{
    private WorkspaceCapture _capture = null!;
    private string           _tempPath = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"bench-ws-{Guid.NewGuid():N}.whidews");

        _capture = new WorkspaceCapture(
            LayoutJson:     "{}",
            SolutionPath:   null,
            OpenFiles:      Enumerable.Range(1, 20).Select(i => new OpenFileEntry($"C:/Projects/Benchmark/File{i:D2}.bin")).ToList(),
            ThemeName:      "Dark");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (File.Exists(_tempPath)) File.Delete(_tempPath);
    }

    [Benchmark]
    public Task SaveWorkspace() => WorkspaceSerializer.WriteAsync(_tempPath, new WorkspaceState
    {
        Manifest = new WorkspaceManifest("BenchmarkWorkspace"),
        Settings = new WorkspaceSettingsOverride(_capture.ThemeName),
        Solution = new WorkspaceSolutionState(_capture.SolutionPath),
        Files    = _capture.OpenFiles.ToList(),
    });

    [Benchmark]
    public Task<WorkspaceState> LoadWorkspace() => WorkspaceSerializer.ReadAsync(_tempPath);
}

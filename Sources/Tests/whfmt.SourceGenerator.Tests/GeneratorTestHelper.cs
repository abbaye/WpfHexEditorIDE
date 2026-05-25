// Project      : whfmt.SourceGenerator.Tests
// File         : GeneratorTestHelper.cs
// Description  : Helpers for running WhfmtIncrementalGenerator via CSharpGeneratorDriver
//                in unit tests without a full MSBuild invocation.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using WhfmtSourceGenerator;

namespace whfmt.SourceGenerator.Tests;

internal static class GeneratorTestHelper
{
    /// <summary>
    /// Runs the WhfmtIncrementalGenerator against the given additional files and returns
    /// the driver result for inspection.
    /// </summary>
    internal static GeneratorDriverRunResult Run(
        IEnumerable<(string path, string content, Dictionary<string, string>? metadata)> additionalFiles)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var texts = additionalFiles
            .Select(f => AdditionalText(f.path, f.content))
            .ToArray();

        var optionsProvider = new TestAnalyzerConfigOptionsProvider(
            additionalFiles.ToDictionary(
                f => f.path,
                f => f.metadata ?? new Dictionary<string, string>()));

        var generator = new WhfmtIncrementalGenerator();
        var driver = CSharpGeneratorDriver
            .Create(generator)
            .AddAdditionalTexts(ImmutableArray.CreateRange<AdditionalText>(texts))
            .WithUpdatedAnalyzerConfigOptions(optionsProvider);

        // RunGeneratorsAndUpdateCompilation returns the updated driver — GetRunResult must be
        // called on the returned instance, not the original (original has no run state).
        var updatedDriver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        return updatedDriver.GetRunResult();
    }

    /// <summary>Convenience overload for a single file with no metadata.</summary>
    internal static GeneratorDriverRunResult Run(string filePath, string content,
        Dictionary<string, string>? metadata = null)
        => Run(new[] { (filePath, content, metadata) });

    private static Microsoft.CodeAnalysis.AdditionalText AdditionalText(string path, string content)
        => new InMemoryAdditionalText(path, content);

    // ── Minimal AdditionalText implementation ──────────────────────────────────

    private sealed class InMemoryAdditionalText : Microsoft.CodeAnalysis.AdditionalText
    {
        private readonly string _path;
        private readonly string _content;
        public InMemoryAdditionalText(string path, string content) { _path = path; _content = content; }
        public override string Path => _path;
        public override Microsoft.CodeAnalysis.Text.SourceText? GetText(
            System.Threading.CancellationToken ct = default)
            => Microsoft.CodeAnalysis.Text.SourceText.From(_content, System.Text.Encoding.UTF8);
    }

    // ── Minimal AnalyzerConfigOptionsProvider implementation ───────────────────

    private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly Dictionary<string, Dictionary<string, string>> _fileOptions;
        public TestAnalyzerConfigOptionsProvider(Dictionary<string, Dictionary<string, string>> fileOptions)
            => _fileOptions = fileOptions;

        public override AnalyzerConfigOptions GlobalOptions => EmptyOptions.Instance;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => EmptyOptions.Instance;

        public override AnalyzerConfigOptions GetOptions(Microsoft.CodeAnalysis.AdditionalText textFile)
        {
            _fileOptions.TryGetValue(textFile.Path, out var dict);
            return new DictOptions(dict ?? new Dictionary<string, string>());
        }

        private sealed class EmptyOptions : AnalyzerConfigOptions
        {
            internal static readonly EmptyOptions Instance = new();
            public override bool TryGetValue(string key, out string value)
            { value = string.Empty; return false; }
        }

        private sealed class DictOptions : AnalyzerConfigOptions
        {
            private readonly Dictionary<string, string> _dict;
            public DictOptions(Dictionary<string, string> dict) => _dict = dict;
            public override bool TryGetValue(string key, out string value)
                => _dict.TryGetValue(key, out value!);
        }
    }
}

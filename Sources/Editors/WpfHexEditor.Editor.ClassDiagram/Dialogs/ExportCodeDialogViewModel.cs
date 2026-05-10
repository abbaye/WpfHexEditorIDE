// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Dialogs/ExportCodeDialogViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     ViewModel for the ExportCodeDialog. Wraps a CodeGenOptions
//     instance with INotifyPropertyChanged surface and exposes
//     a live preview of the generator output for a sample document.
//
// Architecture Notes:
//     Live preview uses a small synthetic DiagramDocument so the
//     preview pane is independent of the user's diagram size and
//     re-evaluation stays fast.
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Abstractions;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Options;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;
using WpfHexEditor.Editor.ClassDiagram.Services;
using SystemRuntimeHelpers = System.Runtime.CompilerServices.RuntimeHelpers;

namespace WpfHexEditor.Editor.ClassDiagram.Dialogs;

/// <summary>ViewModel for the export-code dialog.</summary>
public sealed class ExportCodeDialogViewModel : INotifyPropertyChanged
{
    private string _languageId;
    private string _rootNamespace;
    private IndentStyle _indentStyle;
    private int _indentSize;
    private CSharpLanguageVersion _csharpVersion;
    private bool _useFileScopedNamespace;
    private bool _nullableContextEnabled;
    private bool _emitXmlDocs;
    private bool _emitAttributes;
    private bool _emitHeader;
    private bool _preferRecords;
    private bool _emitAsyncSignatures;

    /// <summary>Hydrates the VM from the supplied <paramref name="settings"/>.</summary>
    public ExportCodeDialogViewModel(CodeGenSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _languageId = settings.LanguageId;
        var o = settings.Options;
        _rootNamespace = o.RootNamespace;
        _indentStyle = o.IndentStyle;
        _indentSize = o.IndentSize;
        _csharpVersion = o.CSharpVersion;
        _useFileScopedNamespace = o.UseFileScopedNamespace;
        _nullableContextEnabled = o.NullableContextEnabled;
        _emitXmlDocs = o.EmitXmlDocs;
        _emitAttributes = o.EmitAttributes;
        _emitHeader = o.EmitHeader;
        _preferRecords = o.PreferRecords;
        _emitAsyncSignatures = o.EmitAsyncSignatures;
    }

    /// <summary>Available languages registered in <see cref="CodeGenLanguageRegistry"/>.</summary>
    public IReadOnlyList<ILanguageGenerator> AvailableLanguages => _availableLanguages;

    private static readonly IReadOnlyList<ILanguageGenerator> _availableLanguages = LoadAvailableLanguages();

    private static IReadOnlyList<ILanguageGenerator> LoadAvailableLanguages()
    {
        // Trigger the registry's static cctor without invoking the generator.
        SystemRuntimeHelpers.RunClassConstructor(typeof(CodeGenerationPipeline).TypeHandle);
        return CodeGenLanguageRegistry.All.OrderBy(g => g.DisplayName).ToList();
    }

    /// <summary>Selected language id.</summary>
    public string LanguageId
    {
        get => _languageId;
        set => SetAndRefreshPreview(ref _languageId, value);
    }

    /// <summary>Root namespace.</summary>
    public string RootNamespace
    {
        get => _rootNamespace;
        set => SetAndRefreshPreview(ref _rootNamespace, value);
    }

    /// <summary>Indent style (spaces / tabs).</summary>
    public IndentStyle IndentStyle
    {
        get => _indentStyle;
        set => SetAndRefreshPreview(ref _indentStyle, value);
    }

    /// <summary>Indent size in units (1-8).</summary>
    public int IndentSize
    {
        get => _indentSize;
        set => SetAndRefreshPreview(ref _indentSize, Math.Clamp(value, 1, 8));
    }

    /// <summary>Target C# version.</summary>
    public CSharpLanguageVersion CSharpVersion
    {
        get => _csharpVersion;
        set => SetAndRefreshPreview(ref _csharpVersion, value);
    }

    /// <summary>Whether to use file-scoped namespaces (C# 10+).</summary>
    public bool UseFileScopedNamespace
    {
        get => _useFileScopedNamespace;
        set => SetAndRefreshPreview(ref _useFileScopedNamespace, value);
    }

    /// <summary>Whether to emit <c>#nullable enable</c>.</summary>
    public bool NullableContextEnabled
    {
        get => _nullableContextEnabled;
        set => SetAndRefreshPreview(ref _nullableContextEnabled, value);
    }

    /// <summary>Whether to emit XML doc summaries.</summary>
    public bool EmitXmlDocs
    {
        get => _emitXmlDocs;
        set => SetAndRefreshPreview(ref _emitXmlDocs, value);
    }

    /// <summary>Whether to emit attribute decorations.</summary>
    public bool EmitAttributes
    {
        get => _emitAttributes;
        set => SetAndRefreshPreview(ref _emitAttributes, value);
    }

    /// <summary>Whether to emit the auto-generated banner.</summary>
    public bool EmitHeader
    {
        get => _emitHeader;
        set => SetAndRefreshPreview(ref _emitHeader, value);
    }

    /// <summary>Whether to use record syntax for record types (C# 9+).</summary>
    public bool PreferRecords
    {
        get => _preferRecords;
        set => SetAndRefreshPreview(ref _preferRecords, value);
    }

    /// <summary>Whether to wrap async return types in <c>Task&lt;T&gt;</c>.</summary>
    public bool EmitAsyncSignatures
    {
        get => _emitAsyncSignatures;
        set => SetAndRefreshPreview(ref _emitAsyncSignatures, value);
    }

    /// <summary>True when the C#-only options should be visible.</summary>
    public bool IsCSharpSelected => string.Equals(_languageId, LanguageIds.CSharp, StringComparison.OrdinalIgnoreCase);

    /// <summary>Live preview of the current options applied to a sample document.</summary>
    public string Preview => SafeGenerate(SampleDocument, LanguageId, BuildOptions());

    /// <summary>Builds the immutable <see cref="CodeGenOptions"/> from the current VM state.</summary>
    public CodeGenOptions BuildOptions() => new()
    {
        RootNamespace = RootNamespace,
        IndentStyle = IndentStyle,
        IndentSize = IndentSize,
        CSharpVersion = CSharpVersion,
        UseFileScopedNamespace = UseFileScopedNamespace,
        NullableContextEnabled = NullableContextEnabled,
        EmitXmlDocs = EmitXmlDocs,
        EmitAttributes = EmitAttributes,
        EmitHeader = EmitHeader,
        PreferRecords = PreferRecords,
        EmitAsyncSignatures = EmitAsyncSignatures
    };

    /// <summary>Builds the persisted settings record from the current VM state.</summary>
    public CodeGenSettings BuildSettings() => new()
    {
        LanguageId = LanguageId,
        Options = BuildOptions()
    };

    /// <summary>Applies the supplied preset and refreshes the preview.</summary>
    public void ApplyPreset(CodeGenOptions preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        RootNamespace = preset.RootNamespace;
        IndentStyle = preset.IndentStyle;
        IndentSize = preset.IndentSize;
        CSharpVersion = preset.CSharpVersion;
        UseFileScopedNamespace = preset.UseFileScopedNamespace;
        NullableContextEnabled = preset.NullableContextEnabled;
        EmitXmlDocs = preset.EmitXmlDocs;
        EmitAttributes = preset.EmitAttributes;
        EmitHeader = preset.EmitHeader;
        PreferRecords = preset.PreferRecords;
        EmitAsyncSignatures = preset.EmitAsyncSignatures;
    }

    private static readonly DiagramDocument SampleDocument = BuildSampleDocument();

    private static DiagramDocument BuildSampleDocument()
    {
        var sample = ClassNode.Create("Sample");
        sample.XmlDocSummary = "A sample type used for live preview.";
        sample.Members.Add(new ClassMember
        {
            Name = "Id",
            Kind = MemberKind.Property,
            TypeName = "Integer"
        });
        sample.Members.Add(new ClassMember
        {
            Name = "DoWork",
            Kind = MemberKind.Method,
            TypeName = string.Empty
        });
        return new DiagramDocument { Classes = { sample } };
    }

    private static string SafeGenerate(DiagramDocument doc, string languageId, CodeGenOptions options)
    {
        try
        {
            return CodeGenerationPipeline.Generate(doc, languageId, options);
        }
        catch (Exception ex)
        {
            return "// Preview unavailable: " + ex.Message;
        }
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetAndRefreshPreview<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        OnPropertyChanged(propertyName);
        OnPropertyChanged(nameof(Preview));
        if (propertyName == nameof(LanguageId))
            OnPropertyChanged(nameof(IsCSharpSelected));
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));
}

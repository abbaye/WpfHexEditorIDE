//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.ProjectSystem.Templates;

// ============================================================================
// Base helper
// ============================================================================

internal abstract class BaseProjectTemplate : IProjectTemplate
{
    public abstract string Id          { get; }
    public abstract string DisplayName { get; }
    public abstract string Description { get; }
    public abstract string Category    { get; }

    public virtual Task<ProjectScaffold> ScaffoldAsync(
        string projectDirectory, string projectName, CancellationToken ct = default)
        => Task.FromResult(BuildScaffold(projectDirectory, projectName));

    protected abstract ProjectScaffold BuildScaffold(string dir, string name);

    // Helpers -------------------------------------------------------------------

    protected static ScaffoldFile Text(string path, string content, bool open = false,
        ProjectItemType type = ProjectItemType.Text)
        => new() { RelativePath = path, Content = Encoding.UTF8.GetBytes(content), OpenOnCreate = open, ItemType = type };

    protected static ScaffoldFile EmptyBin(string path)
        => new() { RelativePath = path, Content = [], OpenOnCreate = true, ItemType = ProjectItemType.Binary };

    protected static string Notes(string projectName) =>
        $"# {projectName}\n\nProject created on {DateTime.Now:yyyy-MM-dd}.\n\n## Notes\n\n";
}

// ============================================================================
// General (2)
// ============================================================================

internal sealed class EmptyProjectTemplate : BaseProjectTemplate
{
    public override string Id          => "empty";
    public override string DisplayName => "Empty Project";
    public override string Description => "A blank project with no initial files. Start from scratch.";
    public override string Category    => "General";

    protected override ProjectScaffold BuildScaffold(string dir, string name)
        => new() { ProjectType = Id };
}

internal sealed class ScratchProjectTemplate : BaseProjectTemplate
{
    public override string Id          => "scratch";
    public override string DisplayName => "Sandbox";
    public override string Description => "Quick scratch pad with a sample binary and a notes file.";
    public override string Category    => "General";

    protected override ProjectScaffold BuildScaffold(string dir, string name) => new()
    {
        ProjectType    = Id,
        Files          = [EmptyBin("scratch.bin"), Text("notes.txt", Notes(name))],
    };
}

// ============================================================================
// Analysis (6)
// ============================================================================

internal sealed class BinaryAnalysisTemplate : BaseProjectTemplate
{
    public override string Id          => "binary-analysis";
    public override string DisplayName => "Binary Analysis";
    public override string Description => "Analyse unknown binary files. Includes entropy viewer and parsed fields.";
    public override string Category    => "Analysis";

    protected override ProjectScaffold BuildScaffold(string dir, string name) => new()
    {
        ProjectType = Id,
        Files       = [EmptyBin("target.bin"), Text("notes.txt", Notes(name))],
    };
}

internal sealed class ForensicsTemplate : BaseProjectTemplate
{
    public override string Id          => "forensics";
    public override string DisplayName => "Forensic Investigation";
    public override string Description => "Investigate evidence files. Organises artefacts in an evidence folder.";
    public override string Category    => "Analysis";

    protected override ProjectScaffold BuildScaffold(string dir, string name) => new()
    {
        ProjectType    = Id,
        VirtualFolders = ["evidence"],
        Files          = [Text("notes.txt", Notes(name))],
    };
}

internal sealed class FirmwareAnalysisTemplate : BaseProjectTemplate
{
    public override string Id          => "firmware-analysis";
    public override string DisplayName => "Firmware Analysis";
    public override string Description => "Analyse embedded-system firmware images. Entropy + disassembly ready.";
    public override string Category    => "Analysis";

    protected override ProjectScaffold BuildScaffold(string dir, string name) => new()
    {
        ProjectType = Id,
        Files       = [EmptyBin("firmware.bin"), Text("notes.txt", Notes(name))],
    };
}

internal sealed class NetworkCaptureTemplate : BaseProjectTemplate
{
    public override string Id          => "network-capture";
    public override string DisplayName => "Network Capture";
    public override string Description => "Inspect raw network packet captures (PCAP/PCAPNG).";
    public override string Category    => "Analysis";

    protected override ProjectScaffold BuildScaffold(string dir, string name) => new()
    {
        ProjectType = Id,
        Files       = [EmptyBin("capture.pcap"), Text("notes.txt", Notes(name))],
    };
}

internal sealed class ScientificDataTemplate : BaseProjectTemplate
{
    public override string Id          => "scientific-data";
    public override string DisplayName => "Scientific Data";
    public override string Description => "Parse and inspect scientific binary formats with structure definitions.";
    public override string Category    => "Analysis";

    protected override ProjectScaffold BuildScaffold(string dir, string name) => new()
    {
        ProjectType = Id,
        Files       = [EmptyBin("data.bin"), Text("notes.txt", Notes(name))],
    };
}

internal sealed class MediaInspectionTemplate : BaseProjectTemplate
{
    public override string Id          => "media-inspection";
    public override string DisplayName => "Media Inspection";
    public override string Description => "Inspect images and audio files embedded in binaries or archives.";
    public override string Category    => "Analysis";

    protected override ProjectScaffold BuildScaffold(string dir, string name) => new()
    {
        ProjectType    = Id,
        VirtualFolders = ["media"],
        Files          = [Text("notes.txt", Notes(name))],
    };
}

// ============================================================================
// ReverseEngineering (3)
// ============================================================================

internal sealed class ReverseEngineeringTemplate : BaseProjectTemplate
{
    public override string Id          => "reverse-engineering";
    public override string DisplayName => "Reverse Engineering";
    public override string Description => "Reverse-engineer executables. Hex, disassembly and entropy panels ready.";
    public override string Category    => "ReverseEngineering";

    protected override ProjectScaffold BuildScaffold(string dir, string name) => new()
    {
        ProjectType = Id,
        Files       = [EmptyBin("target.exe"), Text("notes.txt", Notes(name))],
    };
}

internal sealed class DecompilationTemplate : BaseProjectTemplate
{
    public override string Id          => "decompilation";
    public override string DisplayName => "Decompilation";
    public override string Description => "Disassemble and decompile a binary. Output folder pre-created for source.";
    public override string Category    => "ReverseEngineering";

    protected override ProjectScaffold BuildScaffold(string dir, string name) => new()
    {
        ProjectType    = Id,
        VirtualFolders = ["output"],
        Files          = [EmptyBin("target.bin"), Text("notes.txt", Notes(name))],
    };
}

internal sealed class CryptoAnalysisTemplate : BaseProjectTemplate
{
    public override string Id          => "crypto-analysis";
    public override string DisplayName => "Cryptography / Security";
    public override string Description => "Analyse encryption, obfuscation and key material in binaries.";
    public override string Category    => "ReverseEngineering";

    protected override ProjectScaffold BuildScaffold(string dir, string name) => new()
    {
        ProjectType = Id,
        Files       = [EmptyBin("ciphertext.bin"), Text("notes.txt", Notes(name))],
    };
}

// ============================================================================
// Development (2)
// ============================================================================

internal sealed class FormatDefinitionTemplate : BaseProjectTemplate
{
    public override string Id          => "format-definition";
    public override string DisplayName => "Format Definition";
    public override string Description => "Author a new .whjson format definition alongside sample binaries.";
    public override string Category    => "Development";

    protected override ProjectScaffold BuildScaffold(string dir, string name) => new()
    {
        ProjectType    = Id,
        VirtualFolders = ["samples"],
        Files          =
        [
            Text($"{name}.whjson",
                "{\n  \"formatName\": \"" + name + "\",\n  \"version\": 1,\n  \"fields\": []\n}\n",
                open: true, type: ProjectItemType.FormatDefinition),
        ],
    };
}

internal sealed class TextScriptTemplate : BaseProjectTemplate
{
    public override string Id          => "text-script";
    public override string DisplayName => "Scripts / Source Code";
    public override string Description => "Edit scripts and source files (Lua, Python, ASM, C, …).";
    public override string Category    => "Development";

    protected override ProjectScaffold BuildScaffold(string dir, string name) => new()
    {
        ProjectType = Id,
        Files       = [Text("main.lua", "-- " + name + "\n\n", open: true, type: ProjectItemType.Script),
                       Text("notes.txt", Notes(name))],
    };
}

// ============================================================================
// RomHacking (3)
// ============================================================================

internal sealed class RomHackingTemplate : BaseProjectTemplate
{
    public override string Id          => "rom-hacking";
    public override string DisplayName => "ROM Hacking";
    public override string Description => "Full ROM hacking setup: ROM, character table, tile editor, patcher.";
    public override string Category    => "RomHacking";

    protected override ProjectScaffold BuildScaffold(string dir, string name) => new()
    {
        ProjectType    = Id,
        VirtualFolders = ["chars"],
        Files          =
        [
            EmptyBin("original.rom"),
            Text("chars/characters.tbl", "; Character table for " + name + "\n", type: ProjectItemType.Tbl),
            Text("notes.txt", Notes(name)),
        ],
    };
}

internal sealed class PatchDevelopmentTemplate : BaseProjectTemplate
{
    public override string Id          => "patch-development";
    public override string DisplayName => "Patch Development";
    public override string Description => "Develop IPS/BPS patches. Side-by-side diff viewer pre-configured.";
    public override string Category    => "RomHacking";

    protected override ProjectScaffold BuildScaffold(string dir, string name) => new()
    {
        ProjectType = Id,
        Files       =
        [
            EmptyBin("original.bin"),
            EmptyBin("patched.bin"),
            Text("patch.ips", "", type: ProjectItemType.Patch),
            Text("notes.txt", Notes(name)),
        ],
    };
}

internal sealed class TranslationTemplate : BaseProjectTemplate
{
    public override string Id          => "translation";
    public override string DisplayName => "Translation / Localisation";
    public override string Description => "Translate game dialogue. ROM, TBL table and script folder included.";
    public override string Category    => "RomHacking";

    protected override ProjectScaffold BuildScaffold(string dir, string name) => new()
    {
        ProjectType    = Id,
        VirtualFolders = ["script"],
        Files          =
        [
            EmptyBin("original.rom"),
            Text("characters.tbl", "; Character table\n", type: ProjectItemType.Tbl),
            Text("script/dialogue.txt", "// Dialogue script for " + name + "\n", type: ProjectItemType.Text),
        ],
    };
}

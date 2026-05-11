// ==========================================================
// Project: WpfHexEditor.Plugins.ClassDiagram
// File: Analysis/DiagramCodeEditService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6, Claude Opus 4.7
// Created: 2026-04-07
// Last modified: 2026-05-10 — refactored into a thin facade over
// the RoundTripEditorRegistry / ILanguageRoundTripEditor pipeline
// (ADR-022 Phase 1B-3).
// Description:
//     Bidirectional code editing facade. The legacy static helpers
//     (RenameMemberAsync / AddMemberAsync / DeleteMemberAsync) are
//     preserved for the two production call-sites that depend on them,
//     but now route their work through ILanguageRoundTripEditor so the
//     same code path is shared with the future SDK surface.
//     The new ApplyEditAsync(...) entry point accepts a strongly-typed
//     MemberEdit and returns a RoundTripResult ready for preview/undo.
//
// Architecture Notes:
//     Facade pattern. State-free. Calls into a process-wide registry
//     resolved at startup by ClassDiagramPlugin.InitializeAsync.
//     Watcher cycle-prevention is the caller's responsibility — pass
//     a non-null DiagramLiveSyncService and the facade will push the
//     suppression window itself before writing.
// ==========================================================

using System.IO;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;
using WpfHexEditor.Editor.ClassDiagram.Core.RoundTrip.Abstractions;

namespace WpfHexEditor.Plugins.ClassDiagram.Analysis;

/// <summary>
/// Facade over <see cref="ILanguageRoundTripEditor"/> implementations registered
/// in <see cref="RoundTripEditorRegistry"/>. Provides both the legacy convenience
/// helpers (used by existing production code) and the new strongly-typed pipeline.
/// </summary>
public static class DiagramCodeEditService
{
    // ── New strongly-typed pipeline (ADR-022 Phase 1B-3) ─────────────────────

    /// <summary>
    /// Resolves the appropriate <see cref="ILanguageRoundTripEditor"/> for
    /// <paramref name="filePath"/> and applies <paramref name="edit"/>.
    /// When <paramref name="liveSync"/> is provided, the file path is registered
    /// for FSW cycle-prevention before the write occurs.
    /// </summary>
    public static async Task<RoundTripResult> ApplyEditAsync(
        string                            filePath,
        MemberEdit                        edit,
        DiagramLiveSyncService?           liveSync           = null,
        Func<RoundTripResult, bool>?      confirmBeforeWrite = null,
        CancellationToken                 ct                 = default)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return RoundTripResult.Fail(filePath, "File does not exist.");

        var editor = RoundTripEditorRegistry.TryGetByFilePath(filePath);
        if (editor is null)
            return RoundTripResult.Fail(filePath, $"No round-trip editor registered for '{Path.GetExtension(filePath)}'.");

        string source;
        try
        {
            source = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return RoundTripResult.Fail(filePath, $"Cannot read source file: {ex.Message}");
        }

        var result = await editor.ApplyAsync(filePath, source, edit, ct).ConfigureAwait(false);
        if (!result.Success) return result;

        // Optional gatekeeper: callers (UI) supply a preview/confirmation hook.
        // Returning false aborts without writing — emitted as a Fail result so
        // downstream code can distinguish "edit succeeded but user cancelled".
        if (confirmBeforeWrite is { } gate && !gate(result))
            return RoundTripResult.Fail(filePath, "Cancelled by user.");

        // Cycle prevention BEFORE the write — every FSW event arriving within
        // the suppression window will be dropped by DiagramLiveSyncService.
        liveSync?.SuppressNextChange(filePath);

        try
        {
            await File.WriteAllTextAsync(filePath, result.ContentAfter, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return RoundTripResult.Fail(filePath, $"Cannot write source file: {ex.Message}");
        }

        return result;
    }

    // ── Legacy convenience helpers (kept for existing call-sites) ────────────

    /// <summary>Legacy helper — renames a member by routing through ApplyEditAsync.</summary>
    public static async Task<bool> RenameMemberAsync(
        ClassNode node, ClassMember member, string newName,
        CancellationToken ct = default)
    {
        string? filePath = member.SourceFilePath ?? node.SourceFilePath;
        if (string.IsNullOrEmpty(filePath))         return false;
        if (string.IsNullOrWhiteSpace(newName))     return false;

        var edit = new RenameMember(member.Name, newName)
        {
            TargetTypeFullName = node.Name
        };
        var res = await ApplyEditAsync(filePath, edit, liveSync: null, ct).ConfigureAwait(false);
        return res.Success;
    }

    /// <summary>Legacy helper — appends a raw member snippet to a class.</summary>
    public static async Task<bool> AddMemberAsync(
        ClassNode node, string memberSnippet,
        CancellationToken ct = default)
    {
        string? filePath = node.SourceFilePath;
        if (string.IsNullOrEmpty(filePath))           return false;
        if (string.IsNullOrWhiteSpace(memberSnippet)) return false;

        var edit = new AddMember(memberSnippet)
        {
            TargetTypeFullName = node.Name
        };
        var res = await ApplyEditAsync(filePath, edit, liveSync: null, ct).ConfigureAwait(false);
        return res.Success;
    }

    /// <summary>Legacy helper — removes a member by name.</summary>
    public static async Task<bool> DeleteMemberAsync(
        ClassNode node, ClassMember member,
        CancellationToken ct = default)
    {
        string? filePath = member.SourceFilePath ?? node.SourceFilePath;
        if (string.IsNullOrEmpty(filePath)) return false;

        var edit = new RemoveMember(member.Name)
        {
            TargetTypeFullName = node.Name
        };
        var res = await ApplyEditAsync(filePath, edit, liveSync: null, ct).ConfigureAwait(false);
        return res.Success;
    }
}

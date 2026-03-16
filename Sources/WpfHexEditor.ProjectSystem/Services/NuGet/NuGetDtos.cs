// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: Services/NuGet/NuGetDtos.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     DTOs for deserializing NuGet V3 REST API responses.
//     Uses System.Text.Json property-name attributes — no reflection overhead.
//
// Architecture Notes:
//     All types are sealed records (immutable, value semantics).
//     Only the fields required by the NuGet Manager UI are mapped.
// ==========================================================

using System.Text.Json.Serialization;

namespace WpfHexEditor.ProjectSystem.Services.NuGet;

// ── Service Index ────────────────────────────────────────────────────────────

internal sealed record NuGetServiceIndex(
    [property: JsonPropertyName("resources")] IReadOnlyList<NuGetResource> Resources);

internal sealed record NuGetResource(
    [property: JsonPropertyName("@type")] string Type,
    [property: JsonPropertyName("@id")]   string Id);

// ── Search ───────────────────────────────────────────────────────────────────

internal sealed record NuGetSearchResponse(
    [property: JsonPropertyName("totalHits")] int TotalHits,
    [property: JsonPropertyName("data")]      IReadOnlyList<NuGetSearchResult> Data);

/// <summary>A single package entry returned by the NuGet search endpoint.</summary>
public sealed record NuGetSearchResult(
    [property: JsonPropertyName("id")]             string Id,
    [property: JsonPropertyName("version")]        string Version,
    [property: JsonPropertyName("description")]    string? Description,
    [property: JsonPropertyName("authors")]        IReadOnlyList<string>? Authors,
    [property: JsonPropertyName("iconUrl")]        string? IconUrl,
    [property: JsonPropertyName("totalDownloads")] long TotalDownloads,
    [property: JsonPropertyName("versions")]       IReadOnlyList<NuGetSearchVersion>? Versions);

public sealed record NuGetSearchVersion(
    [property: JsonPropertyName("version")]   string Version,
    [property: JsonPropertyName("downloads")] long Downloads);

// ── Registration (version list) ──────────────────────────────────────────────

internal sealed record NuGetRegistrationIndex(
    [property: JsonPropertyName("items")] IReadOnlyList<NuGetRegistrationPage> Items);

internal sealed record NuGetRegistrationPage(
    [property: JsonPropertyName("@id")]   string Id,
    [property: JsonPropertyName("items")] IReadOnlyList<NuGetRegistrationLeaf>? Items);

internal sealed record NuGetRegistrationLeaf(
    [property: JsonPropertyName("catalogEntry")] NuGetCatalogEntry CatalogEntry);

internal sealed record NuGetCatalogEntry(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("listed")]  bool Listed);

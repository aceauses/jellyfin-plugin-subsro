# Subs.ro Subtitle Provider Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Jellyfin plugin that finds and downloads Romanian subtitles from subs.ro through its official API, working on Jellyfin 10.11 and 12 from one build.

**Architecture:** A thin API client wraps the four subs.ro endpoints. Three pure, I/O-free components — identifier encoding, archive entry scoring, and character-encoding conversion — hold all the logic worth testing. A provider class implements `ISubtitleProvider` and does nothing but orchestrate them.

**Tech Stack:** C#, net9.0, xUnit, `System.IO.Compression` (ZIP is in the BCL — no external archive dependency), `Microsoft.Extensions.Caching.Memory`.

**Spec:** `docs/superpowers/specs/2026-09-01-subsro-subtitle-provider-design.md`

## Global Constraints

- **TargetFramework:** `net9.0`. Not net10.0 — that would drop Jellyfin 10.11 users.
- **targetAbi:** `10.11.0.0`.
- **Language is fixed to Romanian.** Every search sends `language=ro`. There is no language setting.
- **The provider must never throw into Jellyfin.** Every failure path returns an empty result and logs once.
- **The API key never enters the repository** — not in source, not in tests, not in CI, not in examples.
- **Selection must be deterministic.** The same archive and request always yield the same file.
- **Licence:** GPL-3.0.
- **Namespace root:** `Jellyfin.Plugin.SubsRo`.
- **Commits carry no AI attribution trailers.**

### Jellyfin interface surface (verified against release-10.11.z)

```csharp
namespace MediaBrowser.Controller.Subtitles;

public interface ISubtitleProvider
{
    string Name { get; }
    IEnumerable<VideoContentType> SupportedMediaTypes { get; }
    Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken);
    Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken);
}
```

`SubtitleResponse` (`MediaBrowser.Controller.Subtitles`): `Language` string, `Format` string, `IsForced` bool, `IsHearingImpaired` bool, `Stream` Stream.

`RemoteSubtitleInfo` (`MediaBrowser.Model.Providers`): `Id`, `ProviderName`, `Name`, `Format`, `Author`, `Comment`, `ThreeLetterISOLanguageName` strings; `DateCreated` DateTime?; `DownloadCount` int?; `CommunityRating`, `FrameRate` float?; `IsHashMatch`, `AiTranslated`, `MachineTranslated`, `Forced`, `HearingImpaired` bool?.

`SubtitleSearchRequest` (`MediaBrowser.Controller.Subtitles`): `Language`, `TwoLetterISOLanguageName`, `MediaPath`, `SeriesName`, `Name` strings; `ContentType` VideoContentType; `IndexNumber`, `IndexNumberEnd`, `ParentIndexNumber`, `ProductionYear` int?; `RuntimeTicks` long?; `IsPerfectMatch`, `SearchAllProviders`, `IsAutomated` bool; `ProviderIds` Dictionary&lt;string,string&gt;.

---

## File Structure

| File | Responsibility |
|---|---|
| `Jellyfin.Plugin.SubsRo/SubtitleId.cs` | Encode/decode the opaque id handed to Jellyfin |
| `Jellyfin.Plugin.SubsRo/Matching/ArchiveEntrySelector.cs` | Score archive entries against a request. No I/O |
| `Jellyfin.Plugin.SubsRo/Matching/MatchContext.cs` | Input record for the selector |
| `Jellyfin.Plugin.SubsRo/Text/SubtitleEncodingConverter.cs` | Detect byte encoding, convert to UTF-8 |
| `Jellyfin.Plugin.SubsRo/Api/Models/*.cs` | JSON DTOs |
| `Jellyfin.Plugin.SubsRo/Api/SubsRoApiClient.cs` | HTTP over the four endpoints |
| `Jellyfin.Plugin.SubsRo/Configuration/PluginConfiguration.cs` | ApiKey, EnableSeries |
| `Jellyfin.Plugin.SubsRo/Configuration/configPage.html` | Config UI, Romanian labels |
| `Jellyfin.Plugin.SubsRo/Plugin.cs` | Plugin registration, config page wiring |
| `Jellyfin.Plugin.SubsRo/SubsRoSubtitleProvider.cs` | ISubtitleProvider orchestration |
| `Jellyfin.Plugin.SubsRo/ArchiveCache.cs` | Disk cache for downloaded archives |

Tests mirror the source tree under `tests/Jellyfin.Plugin.SubsRo.Tests/`.

---

### Task 1: Solution scaffolding and interface verification

**Files:**
- Create: `Jellyfin.Plugin.SubsRo/Jellyfin.Plugin.SubsRo.csproj`
- Create: `tests/Jellyfin.Plugin.SubsRo.Tests/Jellyfin.Plugin.SubsRo.Tests.csproj`
- Create: `jellyfin-plugin-subsro.sln`

**Interfaces:**
- Consumes: nothing
- Produces: a solution that builds and runs an empty test suite

- [ ] **Step 1: Create the solution and both projects**

```bash
cd /c/Users/ANONIM/Projects/jellyfin-plugin-subsro
dotnet new sln -n jellyfin-plugin-subsro
dotnet new classlib -n Jellyfin.Plugin.SubsRo -f net9.0 -o Jellyfin.Plugin.SubsRo
dotnet new xunit -n Jellyfin.Plugin.SubsRo.Tests -f net9.0 -o tests/Jellyfin.Plugin.SubsRo.Tests
dotnet sln add Jellyfin.Plugin.SubsRo/Jellyfin.Plugin.SubsRo.csproj
dotnet sln add tests/Jellyfin.Plugin.SubsRo.Tests/Jellyfin.Plugin.SubsRo.Tests.csproj
dotnet add tests/Jellyfin.Plugin.SubsRo.Tests reference Jellyfin.Plugin.SubsRo
```

- [ ] **Step 2: Add the Jellyfin reference**

Edit `Jellyfin.Plugin.SubsRo/Jellyfin.Plugin.SubsRo.csproj` so the `PropertyGroup` and package reference read:

```xml
<PropertyGroup>
  <TargetFramework>net9.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <AssemblyVersion>1.0.0.0</AssemblyVersion>
  <FileVersion>1.0.0.0</FileVersion>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Jellyfin.Controller" Version="10.11.*" />
</ItemGroup>
```

- [ ] **Step 3: Verify the interface matches the installed server**

The spec names this the first implementation task: confirm `ISubtitleProvider` really is identical on both server generations before building on it.

```bash
dotnet build -c Release
```

Then confirm the resolved package version and that the four members exist:

```bash
dotnet list Jellyfin.Plugin.SubsRo package | grep -i controller
```

Expected: a 10.11.x version resolves. If `Jellyfin.Controller` 10.11.* does not exist on NuGet, fall back to the newest 10.* available and record the actual version in the README's compatibility section.

- [ ] **Step 4: Run the empty test suite**

Run: `dotnet test`
Expected: build succeeds, 0 tests pass, no errors.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Scaffold solution targeting net9.0 with Jellyfin.Controller 10.11"
```

---

### Task 2: SubtitleId encoding

**Files:**
- Create: `Jellyfin.Plugin.SubsRo/SubtitleId.cs`
- Test: `tests/Jellyfin.Plugin.SubsRo.Tests/SubtitleIdTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces: `SubtitleId` record with `static string Encode(int subtitleId, string? entryPath)`, `static bool TryDecode(string value, out int subtitleId, out string? entryPath)`

- [ ] **Step 1: Write the failing tests**

```csharp
using Jellyfin.Plugin.SubsRo;
using Xunit;

namespace Jellyfin.Plugin.SubsRo.Tests;

public class SubtitleIdTests
{
    [Fact]
    public void Encode_WithoutEntry_ReturnsIdOnly()
    {
        Assert.Equal("130042", SubtitleId.Encode(130042, null));
    }

    [Fact]
    public void RoundTrip_WithEntryPath_PreservesBothParts()
    {
        var encoded = SubtitleId.Encode(130042, "Season 2/Show.S02E05.srt");

        Assert.True(SubtitleId.TryDecode(encoded, out var id, out var entry));
        Assert.Equal(130042, id);
        Assert.Equal("Season 2/Show.S02E05.srt", entry);
    }

    [Fact]
    public void RoundTrip_EntryContainingSeparator_PreservesEntry()
    {
        var encoded = SubtitleId.Encode(7, "weird|name.srt");

        Assert.True(SubtitleId.TryDecode(encoded, out var id, out var entry));
        Assert.Equal(7, id);
        Assert.Equal("weird|name.srt", entry);
    }

    [Theory]
    [InlineData("")]
    [InlineData("notanumber")]
    [InlineData("|orphan.srt")]
    public void TryDecode_Malformed_ReturnsFalse(string value)
    {
        Assert.False(SubtitleId.TryDecode(value, out _, out _));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter SubtitleIdTests`
Expected: FAIL — `SubtitleId` does not exist.

- [ ] **Step 3: Write the implementation**

The separator splits on the **first** `|` only, so entry paths may themselves contain the separator.

```csharp
using System.Globalization;

namespace Jellyfin.Plugin.SubsRo;

/// <summary>
/// Encodes and decodes the opaque identifier handed to Jellyfin.
/// Format: {subtitleId} or {subtitleId}|{entryPath}.
/// </summary>
public static class SubtitleId
{
    private const char Separator = '|';

    public static string Encode(int subtitleId, string? entryPath)
        => string.IsNullOrEmpty(entryPath)
            ? subtitleId.ToString(CultureInfo.InvariantCulture)
            : string.Concat(subtitleId.ToString(CultureInfo.InvariantCulture), Separator, entryPath);

    public static bool TryDecode(string value, out int subtitleId, out string? entryPath)
    {
        subtitleId = 0;
        entryPath = null;

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var index = value.IndexOf(Separator);
        var idPart = index < 0 ? value : value[..index];

        if (!int.TryParse(idPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out subtitleId))
        {
            return false;
        }

        if (index >= 0 && index + 1 < value.Length)
        {
            entryPath = value[(index + 1)..];
        }

        return true;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter SubtitleIdTests`
Expected: PASS, 6 tests.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add opaque subtitle identifier encoding"
```

---

### Task 3: Character encoding conversion

**Files:**
- Create: `Jellyfin.Plugin.SubsRo/Text/SubtitleEncodingConverter.cs`
- Test: `tests/Jellyfin.Plugin.SubsRo.Tests/Text/SubtitleEncodingConverterTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces: `static byte[] ToUtf8(byte[] input)` and `static void RegisterProviders()` on `SubtitleEncodingConverter`

Romanian community subtitles frequently arrive in Windows-1250. Serving them unconverted mangles diacritics, so this runs on every download.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Text;
using Jellyfin.Plugin.SubsRo.Text;
using Xunit;

namespace Jellyfin.Plugin.SubsRo.Tests.Text;

public class SubtitleEncodingConverterTests
{
    private const string Sample = "Ce faci, șefu'? Ține-te bine în această după-amiază.";

    public SubtitleEncodingConverterTests() => SubtitleEncodingConverter.RegisterProviders();

    [Fact]
    public void ToUtf8_AlreadyUtf8_RoundTrips()
    {
        var input = new UTF8Encoding(false).GetBytes(Sample);

        var result = Encoding.UTF8.GetString(SubtitleEncodingConverter.ToUtf8(input));

        Assert.Equal(Sample, result);
    }

    [Fact]
    public void ToUtf8_Utf8WithBom_StripsBomAndRoundTrips()
    {
        var input = new UTF8Encoding(true).GetPreamble()
            .Concat(new UTF8Encoding(false).GetBytes(Sample)).ToArray();

        var output = SubtitleEncodingConverter.ToUtf8(input);

        Assert.NotEqual(0xEF, output[0]);
        Assert.Equal(Sample, Encoding.UTF8.GetString(output));
    }

    [Fact]
    public void ToUtf8_Windows1250_ProducesCorrectDiacritics()
    {
        var input = Encoding.GetEncoding(1250).GetBytes(Sample);

        var result = Encoding.UTF8.GetString(SubtitleEncodingConverter.ToUtf8(input));

        Assert.Contains("ș", result, StringComparison.Ordinal);
        Assert.Contains("ț", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ToUtf8_Empty_ReturnsEmpty()
    {
        Assert.Empty(SubtitleEncodingConverter.ToUtf8(Array.Empty<byte>()));
    }
}
```

Note: Windows-1250 lacks Romanian comma-below characters and maps them to
cedilla forms. The assertion checks the converter produces *valid decoded
text*, not byte-identical round-trip. If the round trip proves lossy for
`ș`/`ț`, assert on the cedilla variants `ş`/`ţ` instead and record the
finding in a code comment — do not weaken the test to `Assert.NotNull`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter SubtitleEncodingConverterTests`
Expected: FAIL — type does not exist.

- [ ] **Step 3: Write the implementation**

```csharp
using System.Text;

namespace Jellyfin.Plugin.SubsRo.Text;

/// <summary>Detects the encoding of a subtitle payload and converts it to UTF-8.</summary>
public static class SubtitleEncodingConverter
{
    private static bool _registered;

    /// <summary>Registers legacy code pages. .NET omits them by default.</summary>
    public static void RegisterProviders()
    {
        if (_registered)
        {
            return;
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _registered = true;
    }

    public static byte[] ToUtf8(byte[] input)
    {
        if (input.Length == 0)
        {
            return input;
        }

        RegisterProviders();

        // 1. Byte-order mark wins outright.
        if (input.Length >= 3 && input[0] == 0xEF && input[1] == 0xBB && input[2] == 0xBF)
        {
            return input[3..];
        }

        // 2. Strict UTF-8 decode: succeeds only if the bytes really are UTF-8.
        var strict = new UTF8Encoding(false, throwOnInvalidBytes: true);
        try
        {
            _ = strict.GetString(input);
            return input;
        }
        catch (DecoderFallbackException)
        {
            // Not UTF-8; fall through.
        }

        // 3. Windows-1250 is the common fallback for Romanian subtitles.
        var text = Encoding.GetEncoding(1250).GetString(input);
        return new UTF8Encoding(false).GetBytes(text);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter SubtitleEncodingConverterTests`
Expected: PASS, 4 tests.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Convert subtitle payloads to UTF-8 with legacy encoding fallback"
```

---

### Task 4: Archive entry selector

**Files:**
- Create: `Jellyfin.Plugin.SubsRo/Matching/MatchContext.cs`
- Create: `Jellyfin.Plugin.SubsRo/Matching/ArchiveEntrySelector.cs`
- Test: `tests/Jellyfin.Plugin.SubsRo.Tests/Matching/ArchiveEntrySelectorTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces: `MatchContext(string? ReleaseName, int? Season, int? Episode)` record; `ArchiveEntrySelector.Rank(IEnumerable<string> entries, MatchContext context)` returning `IReadOnlyList<ScoredEntry>`, where `ScoredEntry(string Path, int Score)`

This is the most bug-prone logic in the project, which is exactly why it touches no network and no disk.

- [ ] **Step 1: Write the failing tests**

```csharp
using Jellyfin.Plugin.SubsRo.Matching;
using Xunit;

namespace Jellyfin.Plugin.SubsRo.Tests.Matching;

public class ArchiveEntrySelectorTests
{
    [Theory]
    [InlineData("Show.S02E05.WEB.srt")]
    [InlineData("Show 2x05 romanian.srt")]
    [InlineData("Show.205.srt")]
    [InlineData("Show - Ep05 - Sezonul 2.srt")]
    public void Rank_SeriesPatterns_PutsMatchingEpisodeFirst(string wanted)
    {
        var entries = new[] { "Show.S02E04.WEB.srt", wanted, "Show.S02E06.WEB.srt" };

        var ranked = ArchiveEntrySelector.Rank(entries, new MatchContext(null, 2, 5));

        Assert.Equal(wanted, ranked[0].Path);
    }

    [Fact]
    public void Rank_IgnoresNonSubtitleFiles()
    {
        var entries = new[] { "readme.txt", "poster.jpg", "Show.S01E01.srt" };

        var ranked = ArchiveEntrySelector.Rank(entries, new MatchContext(null, 1, 1));

        Assert.Single(ranked);
        Assert.Equal("Show.S01E01.srt", ranked[0].Path);
    }

    [Fact]
    public void Rank_MovieReleaseName_PrefersMatchingRelease()
    {
        var entries = new[] { "Obsession.2025.BluRay.srt", "Obsession.2025.WEB-DL.srt" };

        var ranked = ArchiveEntrySelector.Rank(
            entries,
            new MatchContext("Obsession.2025.2160p.MA.WEB-DL.DDP5.1.H.265-BYNDR", null, null));

        Assert.Equal("Obsession.2025.WEB-DL.srt", ranked[0].Path);
    }

    [Fact]
    public void Rank_SingleEntry_ReturnsItRegardlessOfName()
    {
        var ranked = ArchiveEntrySelector.Rank(
            new[] { "whatever.srt" }, new MatchContext("Nothing.Alike", null, null));

        Assert.Single(ranked);
    }

    [Fact]
    public void Rank_EqualScores_IsDeterministicByOrdinalName()
    {
        var entries = new[] { "b.srt", "a.srt" };

        var first = ArchiveEntrySelector.Rank(entries, new MatchContext(null, null, null));
        var second = ArchiveEntrySelector.Rank(entries, new MatchContext(null, null, null));

        Assert.Equal("a.srt", first[0].Path);
        Assert.Equal(first[0].Path, second[0].Path);
    }

    [Fact]
    public void Rank_NoSubtitles_ReturnsEmpty()
    {
        Assert.Empty(ArchiveEntrySelector.Rank(new[] { "a.nfo" }, new MatchContext(null, 1, 1)));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter ArchiveEntrySelectorTests`
Expected: FAIL — types do not exist.

- [ ] **Step 3: Write the implementation**

```csharp
namespace Jellyfin.Plugin.SubsRo.Matching;

/// <summary>What the caller is looking for. Release name for movies, season/episode for series.</summary>
public sealed record MatchContext(string? ReleaseName, int? Season, int? Episode);

/// <summary>An archive entry with its match score.</summary>
public sealed record ScoredEntry(string Path, int Score);
```

```csharp
using System.Globalization;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.SubsRo.Matching;

/// <summary>
/// Ranks archive entries against a match context. Pure: no network, no disk.
/// </summary>
public static class ArchiveEntrySelector
{
    private static readonly string[] SubtitleExtensions = [".srt", ".ass", ".ssa", ".sub", ".vtt"];

    private static readonly char[] ReleaseSeparators = ['.', ' ', '-', '_', '[', ']', '(', ')'];

    // Noise tokens carry no discriminating power between releases of the same film.
    private static readonly HashSet<string> IgnoredTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "srt", "ro", "rom", "romana", "romanian", "sub", "subs"
    };

    private const int EpisodeMatchScore = 100;
    private const int TokenMatchScore = 5;

    public static IReadOnlyList<ScoredEntry> Rank(IEnumerable<string> entries, MatchContext context)
    {
        var subtitles = entries
            .Where(e => SubtitleExtensions.Contains(Path.GetExtension(e), StringComparer.OrdinalIgnoreCase))
            .ToList();

        return subtitles
            .Select(e => new ScoredEntry(e, Score(e, context)))
            .OrderByDescending(e => e.Score)
            .ThenBy(e => e.Path, StringComparer.Ordinal) // deterministic tie-break
            .ToList();
    }

    private static int Score(string entryPath, MatchContext context)
    {
        var name = Path.GetFileNameWithoutExtension(entryPath);

        if (context is { Season: not null, Episode: not null })
        {
            return MatchesEpisode(name, context.Season.Value, context.Episode.Value) ? EpisodeMatchScore : 0;
        }

        if (string.IsNullOrEmpty(context.ReleaseName))
        {
            return 0;
        }

        var wanted = Tokenize(context.ReleaseName);
        var found = Tokenize(name);
        return found.Intersect(wanted, StringComparer.OrdinalIgnoreCase).Count() * TokenMatchScore;
    }

    private static bool MatchesEpisode(string name, int season, int episode)
    {
        var s = season.ToString(CultureInfo.InvariantCulture);
        var e = episode.ToString(CultureInfo.InvariantCulture);
        var e2 = episode.ToString("00", CultureInfo.InvariantCulture);
        var s2 = season.ToString("00", CultureInfo.InvariantCulture);

        // S02E05 / s2e5, 2x05, 205 or 0205, Ep05
        var patterns = new[]
        {
            $@"s0*{s}\s*e0*{e}\b",
            $@"\b0*{s}x0*{e}\b",
            $@"\b{s}{e2}\b",
            $@"\b{s2}{e2}\b",
            $@"\bep\.?\s*0*{e}\b"
        };

        return patterns.Any(p => Regex.IsMatch(name, p, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)));
    }

    private static HashSet<string> Tokenize(string value)
        => value.Split(ReleaseSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1 && !IgnoredTokens.Contains(t))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter ArchiveEntrySelectorTests`
Expected: PASS, 10 tests. If the `Show.205.srt` case fails, the `\b{s}{e2}\b` pattern is the one to inspect — do not relax the assertion.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add deterministic archive entry scoring for movies and episodes"
```

---

### Task 5: API models and client

**Files:**
- Create: `Jellyfin.Plugin.SubsRo/Api/Models/SubsRoSearchResponse.cs`
- Create: `Jellyfin.Plugin.SubsRo/Api/Models/SubsRoSubtitleItem.cs`
- Create: `Jellyfin.Plugin.SubsRo/Api/Models/SubsRoQuota.cs`
- Create: `Jellyfin.Plugin.SubsRo/Api/SubsRoApiClient.cs`
- Test: `tests/Jellyfin.Plugin.SubsRo.Tests/Api/SubsRoApiClientTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces: `SubsRoApiClient(HttpClient client, ILogger<SubsRoApiClient> logger)` with `Task<IReadOnlyList<SubsRoSubtitleItem>> SearchAsync(string field, string value, string apiKey, CancellationToken ct)`, `Task<byte[]?> DownloadAsync(string downloadLink, string apiKey, CancellationToken ct)`, `Task<SubsRoQuota?> GetQuotaAsync(string apiKey, CancellationToken ct)`

- [ ] **Step 1: Write the failing tests**

`StubHandler` returns a canned response so **CI never calls the real API**.

```csharp
using System.Net;
using System.Text;
using Jellyfin.Plugin.SubsRo.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.SubsRo.Tests.Api;

internal sealed class StubHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _body;
    public HttpRequestMessage? LastRequest { get; private set; }

    public StubHandler(HttpStatusCode status, string body) => (_status, _body) = (status, body);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        LastRequest = request;
        return Task.FromResult(new HttpResponseMessage(_status)
        {
            Content = new StringContent(_body, Encoding.UTF8, "application/json")
        });
    }
}

public class SubsRoApiClientTests
{
    private const string SearchBody = """
    {"status":200,"count":1,"items":[{"id":130042,"title":"Obsession","year":2025,
    "imdbid":"tt37287335","tmdbid":"1339713","language":"ro","type":"movie",
    "translator":"MEOO Team","description":"pentru WEB-DL",
    "downloadLink":"https://subs.ro/api/v1.0/subtitle/130042/download"}]}
    """;

    private static SubsRoApiClient Build(StubHandler handler)
        => new(new HttpClient(handler), NullLogger<SubsRoApiClient>.Instance);

    [Fact]
    public async Task SearchAsync_ParsesItems()
    {
        var client = Build(new StubHandler(HttpStatusCode.OK, SearchBody));

        var items = await client.SearchAsync("imdbid", "tt37287335", "key", CancellationToken.None);

        Assert.Single(items);
        Assert.Equal(130042, items[0].Id);
        Assert.Equal("movie", items[0].Type);
        Assert.Equal("https://subs.ro/api/v1.0/subtitle/130042/download", items[0].DownloadLink);
    }

    [Fact]
    public async Task SearchAsync_SendsApiKeyHeaderAndRomanianLanguage()
    {
        var handler = new StubHandler(HttpStatusCode.OK, SearchBody);

        await Build(handler).SearchAsync("imdbid", "tt1", "secret", CancellationToken.None);

        Assert.Equal("secret", handler.LastRequest!.Headers.GetValues("X-Subs-Api-Key").Single());
        Assert.Contains("language=ro", handler.LastRequest.RequestUri!.Query, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task SearchAsync_ErrorStatus_ReturnsEmptyWithoutThrowing(HttpStatusCode status)
    {
        var client = Build(new StubHandler(status, "{}"));

        var items = await client.SearchAsync("imdbid", "tt1", "key", CancellationToken.None);

        Assert.Empty(items);
    }

    [Fact]
    public async Task SearchAsync_MalformedJson_ReturnsEmptyWithoutThrowing()
    {
        var client = Build(new StubHandler(HttpStatusCode.OK, "not json at all"));

        Assert.Empty(await client.SearchAsync("imdbid", "tt1", "key", CancellationToken.None));
    }

    [Fact]
    public async Task GetQuotaAsync_ParsesRemaining()
    {
        const string body = """
        {"status":200,"quota":{"total_quota":300,"used_quota":12,"remaining_quota":288}}
        """;
        var client = Build(new StubHandler(HttpStatusCode.OK, body));

        var quota = await client.GetQuotaAsync("key", CancellationToken.None);

        Assert.Equal(288, quota!.Remaining);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter SubsRoApiClientTests`
Expected: FAIL — types do not exist.

- [ ] **Step 3: Write the models**

```csharp
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.SubsRo.Api.Models;

public sealed class SubsRoSubtitleItem
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("year")] public int? Year { get; set; }
    [JsonPropertyName("imdbid")] public string? ImdbId { get; set; }
    [JsonPropertyName("tmdbid")] public string? TmdbId { get; set; }
    [JsonPropertyName("language")] public string? Language { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("translator")] public string? Translator { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("downloadLink")] public string? DownloadLink { get; set; }
}

public sealed class SubsRoSearchResponse
{
    [JsonPropertyName("status")] public int Status { get; set; }
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("items")] public List<SubsRoSubtitleItem>? Items { get; set; }
}

public sealed class SubsRoQuota
{
    [JsonPropertyName("total_quota")] public int Total { get; set; }
    [JsonPropertyName("used_quota")] public int Used { get; set; }
    [JsonPropertyName("remaining_quota")] public int Remaining { get; set; }
}

public sealed class SubsRoQuotaResponse
{
    [JsonPropertyName("quota")] public SubsRoQuota? Quota { get; set; }
}
```

- [ ] **Step 4: Write the client**

Note `DownloadAsync` takes the **link from the response**, never a rebuilt URL — the download host differs from the API host.

```csharp
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Jellyfin.Plugin.SubsRo.Api.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SubsRo.Api;

/// <summary>Thin wrapper over the subs.ro API. No business logic; never throws.</summary>
public sealed class SubsRoApiClient
{
    private const string BaseUrl = "https://api.subs.ro/v1.0";
    private const string ApiKeyHeader = "X-Subs-Api-Key";

    private readonly HttpClient _client;
    private readonly ILogger<SubsRoApiClient> _logger;

    public SubsRoApiClient(HttpClient client, ILogger<SubsRoApiClient> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SubsRoSubtitleItem>> SearchAsync(
        string field, string value, string apiKey, CancellationToken ct)
    {
        var url = string.Create(CultureInfo.InvariantCulture,
            $"{BaseUrl}/search/{field}/{Uri.EscapeDataString(value)}?language=ro");

        var body = await SendAsync(url, apiKey, ct).ConfigureAwait(false);
        if (body is null)
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<SubsRoSearchResponse>(body);
            return parsed?.Items ?? [];
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "subs.ro returned a response that could not be parsed");
            return [];
        }
    }

    public async Task<SubsRoQuota?> GetQuotaAsync(string apiKey, CancellationToken ct)
    {
        var body = await SendAsync($"{BaseUrl}/quota", apiKey, ct).ConfigureAwait(false);
        if (body is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SubsRoQuotaResponse>(body)?.Quota;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task<byte[]?> DownloadAsync(string downloadLink, string apiKey, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, downloadLink);
        request.Headers.Add(ApiKeyHeader, apiKey);

        try
        {
            using var response = await _client.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("subs.ro download failed with status {Status}", (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "subs.ro download could not be completed");
            return null;
        }
    }

    private async Task<string?> SendAsync(string url, string apiKey, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(ApiKeyHeader, apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await _client.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("subs.ro request failed with status {Status}", (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "subs.ro request could not be completed");
            return null;
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter SubsRoApiClientTests`
Expected: PASS, 8 tests.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "Add subs.ro API client with stubbed-transport tests"
```

---

### Task 6: Plugin registration and configuration page

**Files:**
- Create: `Jellyfin.Plugin.SubsRo/Configuration/PluginConfiguration.cs`
- Create: `Jellyfin.Plugin.SubsRo/Configuration/configPage.html`
- Create: `Jellyfin.Plugin.SubsRo/Plugin.cs`
- Create: `Jellyfin.Plugin.SubsRo/PluginServiceRegistrator.cs`
- Modify: `Jellyfin.Plugin.SubsRo/Jellyfin.Plugin.SubsRo.csproj` (embed the HTML)

**Interfaces:**
- Consumes: `SubsRoApiClient` (Task 5)
- Produces: `Plugin.Instance` static accessor; `PluginConfiguration` with `string ApiKey` and `bool EnableSeries`

- [ ] **Step 1: Write the configuration class**

```csharp
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.SubsRo.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Personal subs.ro API key. Supplied per install; never shipped.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Off by default: series search downloads an archive and spends daily quota.</summary>
    public bool EnableSeries { get; set; }
}
```

- [ ] **Step 2: Write the plugin entry point**

```csharp
using System.Globalization;
using Jellyfin.Plugin.SubsRo.Configuration;
using Jellyfin.Plugin.SubsRo.Text;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.SubsRo;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        SubtitleEncodingConverter.RegisterProviders();
    }

    public static Plugin? Instance { get; private set; }

    public override string Name => "Subs.ro";

    public override Guid Id => Guid.Parse("6f1d5a72-9c34-4e0b-9a55-2f8c1d7b4e90");

    public override string Description => "Subtitrări în română de pe subs.ro.";

    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = string.Format(
                CultureInfo.InvariantCulture,
                "{0}.Configuration.configPage.html",
                GetType().Namespace)
        };
    }
}
```

- [ ] **Step 3: Write the configuration page with Romanian labels**

```html
<!DOCTYPE html>
<html lang="ro">
<head><title>Subs.ro</title></head>
<body>
<div id="SubsRoConfigPage" data-role="page" class="page type-interior pluginConfigurationPage">
  <div data-role="content"><div class="content-primary">
    <form id="SubsRoConfigForm">
      <div class="inputContainer">
        <label class="inputLabel" for="ApiKey">Cheie API subs.ro</label>
        <input is="emby-input" type="text" id="ApiKey" required />
        <div class="fieldDescription">
          Generează cheia din profilul tău de pe subs.ro. Fără ea pluginul nu caută nimic.
        </div>
      </div>
      <label class="checkboxContainer">
        <input is="emby-checkbox" type="checkbox" id="EnableSeries" />
        <span>Caută subtitrări și pentru seriale</span>
      </label>
      <div class="fieldDescription">
        Pentru seriale se descarcă arhiva sezonului, deci se consumă din cota zilnică.
      </div>
      <p id="QuotaDisplay" class="fieldDescription">Cotă rămasă: necunoscută</p>
      <button is="emby-button" type="submit" class="raised button-submit block">
        <span>Salvează</span>
      </button>
    </form>
  </div></div>
  <script type="text/javascript">
    var SubsRoConfig = { pluginUniqueId: '6f1d5a72-9c34-4e0b-9a55-2f8c1d7b4e90' };

    document.querySelector('#SubsRoConfigPage')
      .addEventListener('pageshow', function () {
        Dashboard.showLoadingMsg();
        ApiClient.getPluginConfiguration(SubsRoConfig.pluginUniqueId).then(function (config) {
          document.querySelector('#ApiKey').value = config.ApiKey || '';
          document.querySelector('#EnableSeries').checked = !!config.EnableSeries;
          Dashboard.hideLoadingMsg();
        });
      });

    document.querySelector('#SubsRoConfigForm')
      .addEventListener('submit', function (e) {
        e.preventDefault();
        Dashboard.showLoadingMsg();
        ApiClient.getPluginConfiguration(SubsRoConfig.pluginUniqueId).then(function (config) {
          config.ApiKey = document.querySelector('#ApiKey').value;
          config.EnableSeries = document.querySelector('#EnableSeries').checked;
          ApiClient.updatePluginConfiguration(SubsRoConfig.pluginUniqueId, config)
            .then(Dashboard.processPluginConfigurationUpdateResult);
        });
        return false;
      });
  </script>
</div>
</body>
</html>
```

- [ ] **Step 4: Embed the page and register services**

Add to the csproj:

```xml
<ItemGroup>
  <EmbeddedResource Include="Configuration\configPage.html" />
</ItemGroup>
```

```csharp
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Subtitles;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.SubsRo;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddHttpClient<Api.SubsRoApiClient>();
        serviceCollection.AddSingleton<ISubtitleProvider, SubsRoSubtitleProvider>();
    }
}
```

- [ ] **Step 5: Add the quota endpoint that feeds the third config field**

The config page cannot call subs.ro directly — the key must not reach the
browser, and the API sets no CORS headers. The server proxies it.

Create `Jellyfin.Plugin.SubsRo/Api/SubsRoController.cs`:

```csharp
using Jellyfin.Plugin.SubsRo.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.SubsRo.Api;

[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("SubsRo")]
public class SubsRoController : ControllerBase
{
    private readonly SubsRoApiClient _client;

    public SubsRoController(SubsRoApiClient client) => _client = client;

    [HttpGet("Quota")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> GetQuota(CancellationToken cancellationToken)
    {
        var key = Plugin.Instance?.Configuration.ApiKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            return Ok(new { configured = false });
        }

        var quota = await _client.GetQuotaAsync(key, cancellationToken).ConfigureAwait(false);
        return quota is null
            ? Ok(new { configured = true, reachable = false })
            : Ok(new { configured = true, reachable = true, quota.Remaining, quota.Total });
    }
}
```

Then populate the display in `configPage.html`, inside the existing `pageshow`
handler after the configuration loads:

```javascript
ApiClient.getJSON(ApiClient.getUrl('SubsRo/Quota')).then(function (q) {
  var el = document.querySelector('#QuotaDisplay');
  if (!q.configured) {
    el.textContent = 'Cotă rămasă: introdu mai întâi cheia API';
  } else if (!q.reachable) {
    el.textContent = 'Cotă rămasă: subs.ro nu răspunde sau cheia este invalidă';
  } else {
    el.textContent = 'Cotă rămasă: ' + q.Remaining + ' din ' + q.Total + ' pe zi';
  }
});
```

- [ ] **Step 6: Build**

Run: `dotnet build -c Release`
Expected: succeeds. `SubsRoSubtitleProvider` does not exist yet, so comment out the `AddSingleton` line until Task 7 and leave a `// TODO(Task 7)` marker that Task 7 removes.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "Add plugin registration, Romanian configuration page and quota endpoint"
```

---

### Task 7: Provider search for movies

**Files:**
- Create: `Jellyfin.Plugin.SubsRo/SubsRoSubtitleProvider.cs`
- Modify: `Jellyfin.Plugin.SubsRo/PluginServiceRegistrator.cs` (restore the registration)
- Test: `tests/Jellyfin.Plugin.SubsRo.Tests/SubsRoSubtitleProviderTests.cs`

**Interfaces:**
- Consumes: `SubsRoApiClient.SearchAsync`, `SubtitleId.Encode`
- Produces: `SubsRoSubtitleProvider` implementing `ISubtitleProvider`; `static string? SelectLookup(SubtitleSearchRequest request, out string field)`

- [ ] **Step 1: Write the failing tests for lookup-key precedence**

The spec requires IMDb, then TMDb, then title — because title search returned 22 unrelated results in live testing.

```csharp
using Jellyfin.Plugin.SubsRo;
using MediaBrowser.Controller.Subtitles;
using Xunit;

namespace Jellyfin.Plugin.SubsRo.Tests;

public class LookupSelectionTests
{
    [Fact]
    public void SelectLookup_PrefersImdbOverEverything()
    {
        var request = new SubtitleSearchRequest
        {
            Name = "Obsession",
            ProviderIds = new() { ["Imdb"] = "tt37287335", ["Tmdb"] = "1339713" }
        };

        var value = SubsRoSubtitleProvider.SelectLookup(request, out var field);

        Assert.Equal("imdbid", field);
        Assert.Equal("tt37287335", value);
    }

    [Fact]
    public void SelectLookup_FallsBackToTmdb()
    {
        var request = new SubtitleSearchRequest
        {
            Name = "Obsession",
            ProviderIds = new() { ["Tmdb"] = "1339713" }
        };

        var value = SubsRoSubtitleProvider.SelectLookup(request, out var field);

        Assert.Equal("tmdbid", field);
        Assert.Equal("1339713", value);
    }

    [Fact]
    public void SelectLookup_FallsBackToTitle()
    {
        var request = new SubtitleSearchRequest { Name = "Obsession", ProviderIds = new() };

        var value = SubsRoSubtitleProvider.SelectLookup(request, out var field);

        Assert.Equal("title", field);
        Assert.Equal("Obsession", value);
    }

    [Fact]
    public void SelectLookup_NothingUsable_ReturnsNull()
    {
        var request = new SubtitleSearchRequest { ProviderIds = new() };

        Assert.Null(SubsRoSubtitleProvider.SelectLookup(request, out _));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter LookupSelectionTests`
Expected: FAIL — `SubsRoSubtitleProvider` does not exist.

- [ ] **Step 3: Write the provider with movie search only**

```csharp
using Jellyfin.Plugin.SubsRo.Api;
using Jellyfin.Plugin.SubsRo.Matching;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SubsRo;

public class SubsRoSubtitleProvider : ISubtitleProvider
{
    private readonly SubsRoApiClient _client;
    private readonly ILogger<SubsRoSubtitleProvider> _logger;
    private readonly IMemoryCache _searchCache;
    private readonly ArchiveCache _archives;

    // ApplicationPaths is protected on BasePlugin, so it cannot be reached
    // through Plugin.Instance from here. It is injected instead.
    public SubsRoSubtitleProvider(
        SubsRoApiClient client,
        ILogger<SubsRoSubtitleProvider> logger,
        IMemoryCache searchCache,
        IApplicationPaths applicationPaths)
    {
        _client = client;
        _logger = logger;
        _searchCache = searchCache;
        _archives = new ArchiveCache(Path.Combine(applicationPaths.CachePath, "subsro"));
    }

    public string Name => "Subs.ro";

    public IEnumerable<VideoContentType> SupportedMediaTypes =>
        [VideoContentType.Movie, VideoContentType.Episode];

    public static string? SelectLookup(SubtitleSearchRequest request, out string field)
    {
        if (request.ProviderIds is not null)
        {
            if (request.ProviderIds.TryGetValue("Imdb", out var imdb) && !string.IsNullOrWhiteSpace(imdb))
            {
                field = "imdbid";
                return imdb;
            }

            if (request.ProviderIds.TryGetValue("Tmdb", out var tmdb) && !string.IsNullOrWhiteSpace(tmdb))
            {
                field = "tmdbid";
                return tmdb;
            }
        }

        var title = request.ContentType == VideoContentType.Episode ? request.SeriesName : request.Name;
        if (!string.IsNullOrWhiteSpace(title))
        {
            field = "title";
            return title;
        }

        field = string.Empty;
        return null;
    }

    public async Task<IEnumerable<RemoteSubtitleInfo>> Search(
        SubtitleSearchRequest request, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || string.IsNullOrWhiteSpace(config.ApiKey))
        {
            _logger.LogWarning("Subs.ro API key is not configured; skipping search");
            return [];
        }

        if (request.ContentType == VideoContentType.Episode && !config.EnableSeries)
        {
            return [];
        }

        var value = SelectLookup(request, out var field);
        if (value is null)
        {
            return [];
        }

        var items = await _client
            .SearchAsync(field, value, config.ApiKey, cancellationToken)
            .ConfigureAwait(false);

        var wantedType = request.ContentType == VideoContentType.Episode ? "series" : "movie";

        return items
            .Where(i => string.Equals(i.Type, wantedType, StringComparison.OrdinalIgnoreCase))
            .Select(i => new RemoteSubtitleInfo
            {
                Id = SubtitleId.Encode(i.Id, null),
                ProviderName = Name,
                Name = $"{i.Title} ({i.Year}) — {i.Translator}",
                Format = "srt",
                Author = i.Translator,
                Comment = i.Description,
                ThreeLetterISOLanguageName = "ron"
            })
            .ToList();
    }

    public Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        => throw new NotImplementedException("Task 9");
}
```

Remove the `// TODO(Task 7)` marker in `PluginServiceRegistrator` and restore `AddSingleton<ISubtitleProvider, SubsRoSubtitleProvider>()`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter LookupSelectionTests`
Expected: PASS, 4 tests.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add movie subtitle search with IMDb-first lookup precedence"
```

---

### Task 8: Archive cache and download

**Files:**
- Create: `Jellyfin.Plugin.SubsRo/ArchiveCache.cs`
- Test: `tests/Jellyfin.Plugin.SubsRo.Tests/ArchiveCacheTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces: `ArchiveCache(string rootPath)` with `Task<byte[]?> GetAsync(int subtitleId)`, `Task StoreAsync(int subtitleId, byte[] payload)`, `static IReadOnlyList<string> ListEntries(byte[] zip)`, `static byte[]? ExtractEntry(byte[] zip, string entryPath)`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.IO.Compression;
using System.Text;
using Jellyfin.Plugin.SubsRo;
using Xunit;

namespace Jellyfin.Plugin.SubsRo.Tests;

public class ArchiveCacheTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private static byte[] BuildZip(params (string Name, string Content)[] entries)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach (var (name, content) in entries)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }

        return ms.ToArray();
    }

    [Fact]
    public void ListEntries_ReturnsAllNames()
    {
        var zip = BuildZip(("a.srt", "x"), ("b.srt", "y"));

        Assert.Equal(new[] { "a.srt", "b.srt" }, ArchiveCache.ListEntries(zip).Order());
    }

    [Fact]
    public void ExtractEntry_ReturnsMatchingContent()
    {
        var zip = BuildZip(("a.srt", "hello"), ("b.srt", "world"));

        var bytes = ArchiveCache.ExtractEntry(zip, "b.srt");

        Assert.Equal("world", Encoding.UTF8.GetString(bytes!));
    }

    [Fact]
    public void ExtractEntry_MissingEntry_ReturnsNull()
    {
        Assert.Null(ArchiveCache.ExtractEntry(BuildZip(("a.srt", "x")), "nope.srt"));
    }

    [Fact]
    public void ListEntries_CorruptArchive_ReturnsEmpty()
    {
        Assert.Empty(ArchiveCache.ListEntries(Encoding.UTF8.GetBytes("this is not a zip")));
    }

    [Fact]
    public async Task StoreThenGet_RoundTrips()
    {
        var cache = new ArchiveCache(_root);
        var zip = BuildZip(("a.srt", "x"));

        await cache.StoreAsync(42, zip);

        Assert.Equal(zip, await cache.GetAsync(42));
    }

    [Fact]
    public async Task GetAsync_Missing_ReturnsNull()
    {
        Assert.Null(await new ArchiveCache(_root).GetAsync(999));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter ArchiveCacheTests`
Expected: FAIL — `ArchiveCache` does not exist.

- [ ] **Step 3: Write the implementation**

```csharp
using System.Globalization;
using System.IO.Compression;

namespace Jellyfin.Plugin.SubsRo;

/// <summary>
/// Disk cache for downloaded archives. A season pack is fetched once and then
/// serves every episode in that season.
/// </summary>
public sealed class ArchiveCache
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private readonly string _root;

    public ArchiveCache(string root) => _root = root;

    private string PathFor(int subtitleId)
        => Path.Combine(_root, string.Create(CultureInfo.InvariantCulture, $"{subtitleId}.zip"));

    public async Task<byte[]?> GetAsync(int subtitleId)
    {
        var path = PathFor(subtitleId);
        if (!File.Exists(path))
        {
            return null;
        }

        if (DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > Lifetime)
        {
            TryDelete(path);
            return null;
        }

        try
        {
            return await File.ReadAllBytesAsync(path).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return null;
        }
    }

    public async Task StoreAsync(int subtitleId, byte[] payload)
    {
        try
        {
            Directory.CreateDirectory(_root);
            await File.WriteAllBytesAsync(PathFor(subtitleId), payload).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // A cache miss next time is acceptable; a crash is not.
        }
    }

    public static IReadOnlyList<string> ListEntries(byte[] zip)
    {
        try
        {
            using var ms = new MemoryStream(zip);
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
            return archive.Entries
                .Where(e => !string.IsNullOrEmpty(e.Name))
                .Select(e => e.FullName)
                .ToList();
        }
        catch (InvalidDataException)
        {
            return [];
        }
    }

    public static byte[]? ExtractEntry(byte[] zip, string entryPath)
    {
        try
        {
            using var ms = new MemoryStream(zip);
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = archive.GetEntry(entryPath);
            if (entry is null)
            {
                return null;
            }

            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Ignore.
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter ArchiveCacheTests`
Expected: PASS, 6 tests.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add archive disk cache with ZIP entry listing and extraction"
```

---

### Task 9: GetSubtitles, series expansion, and quota guard

**Files:**
- Modify: `Jellyfin.Plugin.SubsRo/SubsRoSubtitleProvider.cs`
- Test: `tests/Jellyfin.Plugin.SubsRo.Tests/SubsRoSubtitleProviderTests.cs`

**Interfaces:**
- Consumes: `ArchiveCache`, `ArchiveEntrySelector.Rank`, `SubtitleEncodingConverter.ToUtf8`, `SubtitleId.TryDecode`
- Produces: working `GetSubtitles`; series results expanded one per episode entry

- [ ] **Step 1: Write the failing test for the malformed-id path**

```csharp
using Jellyfin.Plugin.SubsRo;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.SubsRo.Tests;

public class GetSubtitlesTests
{
    [Fact]
    public async Task GetSubtitles_MalformedId_ThrowsArgumentException()
    {
        // Jellyfin calls this only with ids we minted, so a malformed id is a
        // programming error, not a user-facing failure. Search is the path that
        // must never throw; this one may.
        var provider = TestProvider.Build();

        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.GetSubtitles("garbage", CancellationToken.None));
    }
}
```

Add a `TestProvider` helper that constructs the provider with a stubbed
`SubsRoApiClient` (reuse `StubHandler` from Task 5) and
`NullLogger<SubsRoSubtitleProvider>.Instance`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter GetSubtitlesTests`
Expected: FAIL — `NotImplementedException` from the Task 7 placeholder.

- [ ] **Step 3: Implement GetSubtitles**

Replace the throwing stub:

```csharp
public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
{
    if (!SubtitleId.TryDecode(id, out var subtitleId, out var entryPath))
    {
        throw new ArgumentException("Malformed subtitle id", nameof(id));
    }

    var config = Plugin.Instance?.Configuration
        ?? throw new InvalidOperationException("Plugin is not initialised");

    var zip = await GetArchiveAsync(subtitleId, config.ApiKey, cancellationToken).ConfigureAwait(false)
        ?? throw new InvalidOperationException("Archive could not be retrieved");

    entryPath ??= ArchiveEntrySelector
        .Rank(ArchiveCache.ListEntries(zip), new MatchContext(null, null, null))
        .FirstOrDefault()?.Path
        ?? throw new InvalidOperationException("Archive contains no subtitle");

    var payload = ArchiveCache.ExtractEntry(zip, entryPath)
        ?? throw new InvalidOperationException("Archive entry could not be extracted");

    return new SubtitleResponse
    {
        Language = "ron",
        Format = "srt",
        Stream = new MemoryStream(SubtitleEncodingConverter.ToUtf8(payload))
    };
}
```

- [ ] **Step 4: Add the quota guard to Search**

Insert immediately after the API-key check in `Search`:

```csharp
var quota = await _client.GetQuotaAsync(config.ApiKey, cancellationToken).ConfigureAwait(false);
if (quota is not null && quota.Remaining < 5)
{
    _logger.LogWarning(
        "Subs.ro daily quota nearly exhausted ({Remaining} of {Total} left); skipping search",
        quota.Remaining, quota.Total);
    return [];
}
```

- [ ] **Step 5: Expand series results one entry per episode**

In `Search`, after building the filtered item list, replace the movie-only
projection for the episode case:

```csharp
if (request.ContentType == VideoContentType.Episode)
{
    var best = items.FirstOrDefault();
    if (best?.DownloadLink is null)
    {
        return [];
    }

    var zip = await GetArchiveAsync(best.Id, config.ApiKey, cancellationToken).ConfigureAwait(false);
    if (zip is null)
    {
        return [];
    }

    var context = new MatchContext(null, request.ParentIndexNumber, request.IndexNumber);
    return ArchiveEntrySelector.Rank(ArchiveCache.ListEntries(zip), context)
        .Where(e => e.Score > 0)
        .Select(e => new RemoteSubtitleInfo
        {
            Id = SubtitleId.Encode(best.Id, e.Path),
            ProviderName = Name,
            Name = Path.GetFileName(e.Path),
            Format = "srt",
            Author = best.Translator,
            Comment = best.Description,
            ThreeLetterISOLanguageName = "ron"
        })
        .ToList();
}
```

Add the shared helper:

```csharp
private async Task<byte[]?> GetArchiveAsync(int subtitleId, string apiKey, CancellationToken ct)
{
    var cached = await _archives.GetAsync(subtitleId).ConfigureAwait(false);
    if (cached is not null)
    {
        return cached;
    }

    var url = $"https://subs.ro/api/v1.0/subtitle/{subtitleId}/download";
    var payload = await _client.DownloadAsync(url, apiKey, ct).ConfigureAwait(false);
    if (payload is not null)
    {
        await _archives.StoreAsync(subtitleId, payload).ConfigureAwait(false);
    }

    return payload;
}
```

Add the imports this task introduces to the top of the file:

```csharp
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Caching.Memory;
```

- [ ] **Step 6: Add the in-memory search cache**

The spec requires search results cached for six hours, keyed by lookup field
and value. Without it, browsing a library re-queries the API for every item and
burns the 300-request daily allowance quickly.

Replace the direct `SearchAsync` call in `Search` with:

```csharp
var cacheKey = $"subsro:{field}:{value}";
if (!_searchCache.TryGetValue(cacheKey, out IReadOnlyList<SubsRoSubtitleItem>? items) || items is null)
{
    items = await _client
        .SearchAsync(field, value, config.ApiKey, cancellationToken)
        .ConfigureAwait(false);

    _searchCache.Set(cacheKey, items, TimeSpan.FromHours(6));
}
```

Register the cache in `PluginServiceRegistrator.RegisterServices`:

```csharp
serviceCollection.AddMemoryCache();
```

- [ ] **Step 7: Run the full suite**

Run: `dotnet test`
Expected: all tests pass.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "Add subtitle download, series expansion, quota guard and search cache"
```

---

### Task 10: Packaging and CI

**Files:**
- Create: `build.yaml`
- Create: `.github/workflows/build.yml`
- Create: `LICENSE` (GPL-3.0 full text)

**Interfaces:**
- Consumes: the built assembly
- Produces: a CI pipeline that builds and tests on every push

- [ ] **Step 1: Write the plugin manifest**

```yaml
name: "Subs.ro"
guid: "6f1d5a72-9c34-4e0b-9a55-2f8c1d7b4e90"
version: "1.0.0.0"
targetAbi: "10.11.0.0"
framework: "net9.0"
overview: "Romanian subtitles from subs.ro"
description: "Fetches Romanian subtitles from subs.ro using its official API."
category: "Subtitles"
owner: "aceauses"
artifacts:
  - "Jellyfin.Plugin.SubsRo.dll"
```

- [ ] **Step 2: Write the CI workflow**

No API key is referenced anywhere: every test uses a stubbed transport.

```yaml
name: build

on:
  push:
    branches: [main]
  pull_request:

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      - run: dotnet restore
      - run: dotnet build --no-restore -c Release
      - run: dotnet test --no-build -c Release --verbosity normal
```

- [ ] **Step 3: Add the GPL-3.0 licence**

```bash
curl -sL https://www.gnu.org/licenses/gpl-3.0.txt -o LICENSE
```

- [ ] **Step 4: Verify the release build produces the artifact**

Run: `dotnet build -c Release && ls Jellyfin.Plugin.SubsRo/bin/Release/net9.0/Jellyfin.Plugin.SubsRo.dll`
Expected: the DLL exists.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add plugin manifest, GPL-3.0 licence and CI workflow"
```

---

### Task 11: Bilingual documentation

**Files:**
- Create: `README.md` (English, leading)
- Create: `README.ro.md` (Romanian, full translation)

**Interfaces:**
- Consumes: the finished plugin
- Produces: user-facing documentation

Both files cover the same ground in the same order, and link to each other on
the first line. Content required by the spec:

- What the plugin does; that a free subs.ro account is needed
- Obtaining an API key, step by step, from registration to generating it
- Installing from a repository URL, and from a manually downloaded release
- Entering the key in the Jellyfin configuration page
- Searching for subtitles on a movie, and what a successful result looks like
- Enabling series support, and that it spends daily quota
- Supported Jellyfin versions: **10.11 and 12**
- Troubleshooting, each with the log line that identifies it: no results,
  invalid key, exhausted quota, broken diacritics
- How to report a mismatched subtitle, including the archive name, so the
  selector's test corpus can grow from real failures

- [ ] **Step 1: Write `README.md`**

Open with a one-line language switcher: `[English](README.md) · [Română](README.ro.md)`.

- [ ] **Step 2: Write `README.ro.md`**

Same structure, same switcher, full Romanian translation. Not a summary — a
Romanian reader must not need the English file.

- [ ] **Step 3: Verify every documented claim**

Walk the install instructions on the real Jellyfin server and correct anything
that does not match. Documentation that has never been followed is a guess.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "Add English and Romanian user documentation"
```

---

### Task 12: End-to-end verification on the live server

**Files:**
- Modify: `README.md`, `README.ro.md` (screenshots, corrections)

- [ ] **Step 1: Install the built plugin**

```bash
mkdir -p "/c/ProgramData/Jellyfin/Server/plugins/Subs.ro_1.0.0.0"
cp Jellyfin.Plugin.SubsRo/bin/Release/net9.0/Jellyfin.Plugin.SubsRo.dll \
   "/c/ProgramData/Jellyfin/Server/plugins/Subs.ro_1.0.0.0/"
cp build.yaml "/c/ProgramData/Jellyfin/Server/plugins/Subs.ro_1.0.0.0/meta.json"
```

The manifest must be valid JSON named `meta.json`; convert the YAML fields by
hand, matching the shape of the existing `Open Subtitles_24.0.0.0/meta.json`.

- [ ] **Step 2: Restart Jellyfin and confirm the plugin loads**

Check `C:\ProgramData\Jellyfin\Server\log\log_*.log` for the plugin name with
no load error.

- [ ] **Step 3: Configure the key and search a real movie**

Use `Minions & Monsters (2026)` — already in the library with a known IMDb id.
Confirm results appear, download one, and confirm the diacritics render.

- [ ] **Step 4: Verify quota consumption**

```bash
curl -s -H "X-Subs-Api-Key: $SUBSRO_KEY" https://api.subs.ro/v1.0/quota
```

Confirm `used_quota` rose by the expected amount and no more.

- [ ] **Step 5: Commit any documentation corrections**

```bash
git add -A
git commit -m "Correct documentation against live server verification"
```

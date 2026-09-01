# Subs.ro Subtitle Provider for Jellyfin — Design

Date: 2026-09-01
Status: Approved

## Purpose

A Jellyfin subtitle provider plugin that fetches Romanian subtitles from
subs.ro through its official public API. No such plugin exists today;
Romanian users currently rely on manually downloading and placing `.srt`
files next to their media.

Scope is Romanian-language subtitles only (`language=ro` is fixed).
Movies are always supported; TV episodes are supported behind a
configuration toggle.

## Non-Goals

- Multi-language search. The API supports ten languages; this plugin
  requests Romanian only. Users needing other languages run the official
  Open Subtitles plugin alongside this one.
- HTML scraping. The plugin talks only to the documented API.
- Automatic subtitle downloading on library scan. Jellyfin's own
  subtitle-download tasks drive that; this plugin only answers queries.

## External API

Verified live on 2026-09-01 against a real key.

```
Base      https://api.subs.ro/v1.0
Auth      X-Subs-Api-Key: <key>        (or ?apiKey=<key>)
Search    GET /search/{field}/{value}?language=ro
          field in { imdbid, tmdbid, title, release }
Detail    GET /subtitle/{id}
Download  GET /subtitle/{id}/download  -> ZIP archive
Quota     GET /quota
```

`/quota` returns `total_quota`, `used_quota`, `remaining_quota`. The test
key had a daily allowance of 300.

Search results carry `id`, `title`, `year`, `imdbid`, `tmdbid`,
`language`, `type` (`movie` or `series`), `translator`, `description`,
and `downloadLink`.

Two observed behaviours shape the design:

1. Title search is fuzzy. Searching `title/Obsession` returned 22 items
   spanning unrelated films from 1935 onward. IMDb and TMDb lookups are
   precise; title search is a last resort.
2. `downloadLink` points at a different host than the API base
   (`subs.ro/api/v1.0/...` rather than `api.subs.ro/v1.0/...`). The
   plugin must use the URL returned in the response rather than
   rebuilding it, so infrastructure changes do not break downloads.

## Target Platform

Jellyfin 12 plugins target **net10.0**. Established by inspecting
installed plugins: Intro Skipper (`targetAbi 12.0.0.0`) is compiled
against `.NETCoreApp,Version=v10.0`. The Open Subtitles plugin
(`targetAbi 10.11.8.0`, net9.0) still loads on Jellyfin 12, so net9.0 is
a viable fallback if net10.0 tooling causes trouble.

Building requires the .NET 10 SDK.

## Architecture

```
Jellyfin core
    |  ISubtitleProvider
    v
SubsRoSubtitleProvider  ---- SubtitleId (encode/decode)
    |                   ---- ArchiveEntrySelector (pure, no I/O)
    |                   ---- SubtitleEncodingConverter (pure)
    v
SubsRoApiClient  ->  cache (memory: searches, disk: archives)
    |
    v
api.subs.ro
```

### Components

**`SubsRoApiClient`** — a thin HTTP wrapper over the four endpoints.
Takes `IHttpClientFactory`. Applies the auth header, deserializes JSON,
maps transport and status failures onto a result type. Holds no business
logic.

**`SubsRoSubtitleProvider`** — implements `ISubtitleProvider`. Exposes
`Name`, `SupportedMediaTypes`, `Search`, and `GetSubtitles`. Orchestrates
the other components; contains no parsing or scoring logic of its own.

**`ArchiveEntrySelector`** — given a list of archive entry names plus a
match context (release name for movies; season and episode numbers for
series), returns entries ranked by score. Performs no I/O whatsoever,
which makes the most bug-prone logic in the project unit-testable
without a server, an API key, or fixture files on disk.

**`SubtitleEncodingConverter`** — detects the byte encoding of a `.srt`
and converts it to UTF-8.

**`SubtitleId`** — encodes and decodes the opaque identifier the plugin
hands to Jellyfin.

## Search Flow

Jellyfin passes a `SubtitleSearchRequest` already carrying IMDb id, TMDb
id, title, production year, and — for episodes — season and episode
numbers.

1. Choose a lookup key in descending order of precision:
   **IMDb id, then TMDb id, then title.**
2. Call `/search/{field}/{value}?language=ro`.
3. Discard items whose `type` does not match the request content type.
4. Build results, differentiated by media type (below).

### Movies

No archive is downloaded during search. Movie archives hold one to three
`.srt` files — typically variants for different releases — and
release-name scoring resolves them reliably at selection time. Each API
item becomes one search result.

When two entries score equally, the selector takes the first in ordinal
name order and logs the tie. Selection must be deterministic: the same
archive and the same request always yield the same file, so a user who
reports a wrong subtitle can be reproduced exactly.

### Series

The archive is downloaded during search, **for the single best-scoring
API item only**. It is expanded and one result is emitted per episode
entry, so the user sees and picks a specific episode rather than
trusting a guess.

This costs one download per search, mitigated by the disk cache: a season
pack is fetched once and then serves every episode in that season without
further requests. The cost is paid per season, not per episode.

## Identifier Encoding

The identifier returned to Jellyfin is opaque and owned by the plugin:

```
{subtitleId}|{archiveEntryPath}
```

The entry path is optional and omitted for movie results, where the entry
is chosen at download time. Encoding the entry lets series results point
at an exact file without a second lookup.

## Caching and Quota

- **Search results** — in memory, keyed by lookup field and value, TTL 6
  hours.
- **Archives** — on disk in the plugin data directory, keyed by subtitle
  id, TTL 7 days. Serves both search-time expansion and download.
- **Quota guard** — `/quota` is consulted before a search round. Below a
  threshold of 5 remaining requests the plugin stops querying and logs,
  rather than burning the remaining allowance.

## Character Encoding

Community subtitles are frequently distributed in **Windows-1250** or
**ISO-8859-2** rather than UTF-8. Serving those bytes unconverted
produces mangled Romanian diacritics — a defect users typically notice
only minutes into playback.

Detection order:

1. Byte-order mark, if present.
2. Strict UTF-8 decode; accept if it succeeds.
3. Fall back to Windows-1250.

Output is always UTF-8. `CodePagesEncodingProvider` must be registered at
plugin startup, since .NET does not ship legacy code pages by default.

## Error Handling

**The provider must never throw into Jellyfin.** A provider that raises
breaks the subtitle UI for every installed provider, including Open
Subtitles.

Each of the following returns an empty result set and logs once — not
once per request:

- Missing or empty API key
- HTTP 401 or 403 (invalid or revoked key)
- HTTP 429, or exhausted quota
- Network or timeout failures
- Corrupt archive, or an archive containing no `.srt`

## Configuration

Three fields, rendered by an embedded `configPage.html`:

| Field | Type | Default |
|---|---|---|
| API key | text, required | empty |
| Search subtitles for series | toggle | off |
| Remaining quota | read-only display | from `/quota` |

The API key is supplied by each user through the Jellyfin UI. It is never
committed, never embedded in source, and never present in tests or
examples.

## Testing

- `ArchiveEntrySelector` — a corpus of real-world naming conventions:
  `S02E05`, `2x05`, `205`, `Ep05`, and release-name variants for movies.
- `SubtitleEncodingConverter` — sample byte arrays in UTF-8 (with and
  without BOM), Windows-1250, and ISO-8859-2, asserting correct Romanian
  diacritics.
- `SubtitleId` — round-trip encode and decode, including entry paths
  containing separators.
- `SubsRoApiClient` — a stubbed `HttpMessageHandler`. **CI makes no live
  API calls**, so no key is ever needed in GitHub Actions.

An optional integration test may exercise the real API, gated behind an
environment variable and excluded from CI.

## Repository

- Name: `jellyfin-plugin-subsro`
- Visibility: public
- License: GPL-3.0, matching the Jellyfin plugin ecosystem
- CI: build and test on push
- `.gitignore` covers `bin/`, `obj/`, and local configuration

## Open Risks

- **Jellyfin 12 plugin API drift.** Version 12 is newer than most public
  plugin documentation. The interface surface will be confirmed against
  the installed `Jellyfin.Controller` assembly before implementation.
- **Season-pack naming variance.** The selector is a heuristic. The test
  corpus is the mitigation, and it should grow whenever a real archive
  defeats it.

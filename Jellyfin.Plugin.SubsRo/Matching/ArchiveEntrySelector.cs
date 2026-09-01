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

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Ranks archive entries against the given match context, keeping only recognized subtitle files.
    /// </summary>
    /// <param name="entries">The archive entry paths to rank.</param>
    /// <param name="context">What the caller is looking for.</param>
    /// <returns>Subtitle entries ordered by descending score, tied entries ordered by ordinal path for determinism.</returns>
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

        // These patterns encode both season and episode unambiguously in one token,
        // so a match is trustworthy on its own: S02E05 / s2e5, 2x05, 205, 0205.
        var seasonAndEpisodePatterns = new[]
        {
            $@"s0*{s}\s*e0*{e}\b",
            $@"\b0*{s}x0*{e}\b",
            $@"\b{s}{e2}\b",
            $@"\b{s2}{e2}\b"
        };

        if (seasonAndEpisodePatterns.Any(p => Regex.IsMatch(name, p, RegexOptions.IgnoreCase, RegexTimeout)))
        {
            return true;
        }

        // "Ep05" alone carries no season information, so it must not be trusted in
        // isolation: a file for a different season using the same "EpNN" convention
        // would otherwise score as a full match. Only accept it when the filename's
        // separately stated season word (if any) agrees with the wanted season.
        if (!Regex.IsMatch(name, $@"\bep\.?\s*0*{e}\b", RegexOptions.IgnoreCase, RegexTimeout))
        {
            return false;
        }

        var statedSeason = Regex.Match(name, @"\b(?:season|sezon(?:ul)?)\.?\s*0*(\d+)\b", RegexOptions.IgnoreCase, RegexTimeout);
        return !statedSeason.Success || statedSeason.Groups[1].Value == s;
    }

    private static HashSet<string> Tokenize(string value)
        => value.Split(ReleaseSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1 && !IgnoredTokens.Contains(t))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}

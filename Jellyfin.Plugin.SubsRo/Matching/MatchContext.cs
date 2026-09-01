namespace Jellyfin.Plugin.SubsRo.Matching;

/// <summary>What the caller is looking for. Release name for movies, season/episode for series.</summary>
/// <param name="ReleaseName">The release name of the movie being matched, if known.</param>
/// <param name="Season">The season number being matched, if known.</param>
/// <param name="Episode">The episode number being matched, if known.</param>
public sealed record MatchContext(string? ReleaseName, int? Season, int? Episode);

/// <summary>An archive entry with its match score.</summary>
/// <param name="Path">The entry's path within the archive.</param>
/// <param name="Score">The computed match score; higher is a better match.</param>
public sealed record ScoredEntry(string Path, int Score);

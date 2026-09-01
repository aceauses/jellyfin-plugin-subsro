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

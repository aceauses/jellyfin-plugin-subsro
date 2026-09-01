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

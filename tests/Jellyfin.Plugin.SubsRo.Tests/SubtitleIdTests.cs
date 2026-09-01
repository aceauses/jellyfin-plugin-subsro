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

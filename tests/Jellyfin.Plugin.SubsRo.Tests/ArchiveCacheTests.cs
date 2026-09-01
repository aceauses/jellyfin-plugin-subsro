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
                using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
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

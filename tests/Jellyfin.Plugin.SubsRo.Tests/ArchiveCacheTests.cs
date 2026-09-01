using System.IO.Compression;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Jellyfin.Plugin.SubsRo;
using Xunit;

#pragma warning disable CA1416 // Validate platform compatibility

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

    [Fact]
    public async Task GetAsync_UnreadableFile_ReturnsNullNotThrow()
    {
        var cache = new ArchiveCache(_root);
        var zip = BuildZip(("a.srt", "x"));
        await cache.StoreAsync(42, zip);

        var filePath = Path.Combine(_root, "42.zip");
        var fileInfo = new FileInfo(filePath);
        var originalAcl = fileInfo.GetAccessControl();

        try
        {
            // Deny read access by clearing all permissions
            var acl = new FileSecurity();
            acl.SetAccessRuleProtection(true, false);
            fileInfo.SetAccessControl(acl);

            // Should return null instead of throwing UnauthorizedAccessException
            var result = await cache.GetAsync(42);
            Assert.Null(result);
        }
        finally
        {
            try
            {
                // Restore original permissions for cleanup
                fileInfo.SetAccessControl(originalAcl);
            }
            catch
            {
                // If restoration fails, the Dispose will clean up
            }
        }
    }

    [Fact]
    public async Task StoreAsync_DeniedWriteAccess_CompletesNotThrow()
    {
        // Use separate temp directory for permission test to avoid cleanup issues
        var restrictedDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            Directory.CreateDirectory(restrictedDir);
            var cache = new ArchiveCache(restrictedDir);

            var dirInfo = new DirectoryInfo(restrictedDir);
            var originalAcl = dirInfo.GetAccessControl();

            try
            {
                // Deny write access by clearing all permissions
                var acl = new DirectorySecurity();
                acl.SetAccessRuleProtection(true, false);
                dirInfo.SetAccessControl(acl);

                var zip = BuildZip(("a.srt", "x"));
                // Should complete without throwing UnauthorizedAccessException
                await cache.StoreAsync(42, zip);
                // Success means no exception was thrown
            }
            finally
            {
                try
                {
                    // Restore permissions for cleanup
                    dirInfo.SetAccessControl(originalAcl);
                }
                catch
                {
                    // Ignore restoration errors, we'll clean up what we can
                }
            }
        }
        finally
        {
            try
            {
                if (Directory.Exists(restrictedDir))
                {
                    Directory.Delete(restrictedDir, true);
                }
            }
            catch
            {
                // Ignore cleanup errors if permissions can't be reset
            }
        }
    }
}

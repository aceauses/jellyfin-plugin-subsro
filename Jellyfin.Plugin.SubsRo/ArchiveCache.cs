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

    /// <summary>
    /// Initializes a new instance of the <see cref="ArchiveCache"/> class with the specified cache root directory.
    /// </summary>
    /// <param name="root">The root directory path where archive files will be cached.</param>
    public ArchiveCache(string root) => _root = root;

    private string PathFor(int subtitleId)
        => Path.Combine(_root, string.Create(CultureInfo.InvariantCulture, $"{subtitleId}.zip"));

    /// <summary>
    /// Retrieves a cached archive from disk if it exists and is fresh enough.
    /// </summary>
    /// <param name="subtitleId">The subtitle ID whose archive to retrieve.</param>
    /// <returns>The archive bytes if found and within the 7-day lifetime, otherwise null.</returns>
    public async Task<byte[]?> GetAsync(int subtitleId)
    {
        var path = PathFor(subtitleId);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            if (DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > Lifetime)
            {
                TryDelete(path);
                return null;
            }

            return await File.ReadAllBytesAsync(path).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Stores an archive to disk cache, suppressing any filesystem errors to prevent crashes.
    /// </summary>
    /// <param name="subtitleId">The subtitle ID to cache the archive under.</param>
    /// <param name="payload">The raw archive bytes to store.</param>
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
        catch (UnauthorizedAccessException)
        {
            // A cache miss next time is acceptable; a crash is not.
        }
        catch (NotSupportedException)
        {
            // A cache miss next time is acceptable; a crash is not.
        }
    }

    /// <summary>
    /// Lists all entry names in a ZIP archive buffer, returning an empty list if the archive is corrupt.
    /// </summary>
    /// <param name="zip">The raw ZIP archive bytes.</param>
    /// <returns>A list of all entry paths in the archive, or an empty list if the archive is invalid.</returns>
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

    /// <summary>
    /// Extracts a single entry from a ZIP archive buffer, returning null if the entry is missing or the archive is corrupt.
    /// </summary>
    /// <param name="zip">The raw ZIP archive bytes.</param>
    /// <param name="entryPath">The path of the entry to extract within the archive.</param>
    /// <returns>The entry's bytes if found, or null if missing or the archive is invalid.</returns>
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
            // Ignore deletion failures to maintain cache consistency
        }
        catch (UnauthorizedAccessException)
        {
            // Ignore deletion failures to maintain cache consistency
        }
    }
}
